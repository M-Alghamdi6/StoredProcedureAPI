using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;
using StoredProcedureAPI.Configuration;
using StoredProcedureAPI.Models;
using StoredProcedureAPI.Repository;
using StoredProcedureAPI.Utilities;
using StoredProcedureAPI.DTOs;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace StoredProcedureAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProceduresController : ControllerBase
    {
        private readonly ProcedureRepository _repo;
        private readonly ILogger<ProceduresController> _logger;
        private readonly AllowedProceduresOptions _allowed;
        private readonly IMemoryCache _cache;
        private readonly IExecutionLogRepository _auditRepo;
        private readonly int _maxRowLimit = 10000;
        private readonly int _commandTimeoutSeconds = 30;
        private static readonly Regex _identifierRegex = new(@"^[A-Za-z0-9_]+$");
        private readonly bool _isDevelopment;

        public ProceduresController(
            ProcedureRepository repo,
            ILogger<ProceduresController> logger,
            IConfiguration config,
            IMemoryCache cache,
            IExecutionLogRepository auditRepo
        )
        {
            _repo = repo;
            _logger = logger;
            _cache = cache;
            _auditRepo = auditRepo;
            _allowed = config.GetSection("AllowedProcedures")
                             .Get<AllowedProceduresOptions>() ?? new AllowedProceduresOptions();
            _isDevelopment = string.Equals(
                Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
                "Development",
                StringComparison.OrdinalIgnoreCase);
        }

        [HttpGet("schemas")]
        public async Task<ActionResult<JSONResponseDTO<IEnumerable<SchemaModel>>>> GetSchemas()
        {
            var result = await _repo.GetSchemasAsync();
            return Ok(new JSONResponseDTO<IEnumerable<SchemaModel>>
            {
                StatusCode = HttpStatusCode.OK,
                Data = result
            });
        }

        [HttpGet("{schemaName}/procedures")]
        public async Task<ActionResult<JSONResponseDTO<IEnumerable<StoredProcedure>>>> GetProceduresBySchema(string schemaName)
        {
            if (!IsValidIdentifier(schemaName))
                return BadRequest(new JSONResponseDTO<IEnumerable<StoredProcedure>>
                {
                    StatusCode = HttpStatusCode.BadRequest,
                    Message = "Invalid schema name format."
                });

            schemaName = schemaName.Trim();

            if (!await _repo.SchemaExistsAsync(schemaName))
                return BadRequest(new JSONResponseDTO<IEnumerable<StoredProcedure>>
                {
                    StatusCode = HttpStatusCode.BadRequest,
                    Message = $"Schema '{schemaName}' does not exist."
                });

            var result = await _repo.GetProceduresBySchemaAsync(schemaName);
            return Ok(new JSONResponseDTO<IEnumerable<StoredProcedure>>
            {
                StatusCode = HttpStatusCode.OK,
                Data = result
            });
        }

        [HttpGet("{schemaName}/{procedureName}/parameters")]
        public async Task<ActionResult<JSONResponseDTO<IEnumerable<ProcedureParameter>>>> GetParameters(string schemaName, string procedureName)
        {
            if (!IsValidIdentifier(schemaName) || !IsValidIdentifier(procedureName))
                return BadRequest(new JSONResponseDTO<IEnumerable<ProcedureParameter>>
                {
                    StatusCode = HttpStatusCode.BadRequest,
                    Message = "Invalid schema or procedure name format."
                });

            schemaName = schemaName.Trim();

            if (!await _repo.SchemaExistsAsync(schemaName))
                return BadRequest(new JSONResponseDTO<IEnumerable<ProcedureParameter>>
                {
                    StatusCode = HttpStatusCode.BadRequest,
                    Message = $"Schema '{schemaName}' does not exist."
                });
            if (!await _repo.ProcedureExistsAsync(schemaName, procedureName))
                return BadRequest(new JSONResponseDTO<IEnumerable<ProcedureParameter>>
                {
                    StatusCode = HttpStatusCode.BadRequest,
                    Message = $"Procedure '{procedureName}' does not exist."
                });

            var result = await _repo.GetParametersAsync(schemaName, procedureName);
            return Ok(new JSONResponseDTO<IEnumerable<ProcedureParameter>>
            {
                StatusCode = HttpStatusCode.OK,
                Data = result
            });
        }

        [HttpPost("{schemaName}/{procedureName}/execute")]
        public async Task<ActionResult<JSONResponseDTO<ProcedureExecutionResponse>>> ExecuteProcedure(
            string schemaName,
            string procedureName,
            [FromBody] ProcedureExecutionRequest? request,
            CancellationToken cancellationToken)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                if (!IsValidIdentifier(schemaName) || !IsValidIdentifier(procedureName))
                    return BadRequest(new JSONResponseDTO<ProcedureExecutionResponse>
                    {
                        StatusCode = HttpStatusCode.BadRequest,
                        Message = "Invalid schema or procedure name."
                    });

                if (!IsAllowed(schemaName, procedureName))
                    return Unauthorized(new JSONResponseDTO<ProcedureExecutionResponse>
                    {
                        StatusCode = HttpStatusCode.Unauthorized,
                        Message = "Not permitted."
                    });

                var parametersMetadata = (await _repo.GetParametersAsync(schemaName, procedureName)).ToList();
                var input = request?.Parameters ?? new Dictionary<string, object?>();

                foreach (var meta in parametersMetadata.Where(p => !p.IsOutput && !p.IsNullable))
                {
                    var keyName = NormalizeParamKey(meta.ParameterName);
                    bool provided = input.Keys.Any(k => string.Equals(NormalizeParamKey(k), keyName, StringComparison.OrdinalIgnoreCase));
                    if (!provided)
                    {
                        return BadRequest(new JSONResponseDTO<ProcedureExecutionResponse>
                        {
                            StatusCode = HttpStatusCode.BadRequest,
                            Message = _isDevelopment
                                ? $"Missing required parameter: {meta.ParameterName}. Expected parameters: {string.Join(", ", parametersMetadata.Where(x => !x.IsOutput).Select(x => x.ParameterName))}"
                                : $"Missing required parameter: {meta.ParameterName}"
                        });
                    }
                }

                var dapperParams = new DynamicParameters();
                foreach (var meta in parametersMetadata)
                {
                    if (meta.IsOutput) continue;
                    var requestedKey = input.Keys.FirstOrDefault(k =>
                        string.Equals(NormalizeParamKey(k), NormalizeParamKey(meta.ParameterName), StringComparison.OrdinalIgnoreCase));
                    object? rawVal = requestedKey != null ? input[requestedKey] : null;
                    var (ok, normalizedValue, error) = ParameterValidator.Validate(meta, rawVal);
                    if (!ok)
                    {
                        return BadRequest(new JSONResponseDTO<ProcedureExecutionResponse>
                        {
                            StatusCode = HttpStatusCode.BadRequest,
                            Message = _isDevelopment
                                ? $"Validation failed for {meta.ParameterName}: {error}. Sent value: {rawVal ?? "<null>"}"
                                : error
                        });
                    }

                    var addName = meta.ParameterName.StartsWith("@") ? meta.ParameterName[1..] : meta.ParameterName;
                    dapperParams.Add(addName, normalizedValue);
                }

                string cacheKey = $"{schemaName}.{procedureName}:" +
                    JsonSerializer.Serialize(dapperParams.ParameterNames.ToDictionary(n => n, n => dapperParams.Get<object>(n)));

                if (request?.UseCache == true &&
                    _cache.TryGetValue(cacheKey, out var cachedObj) &&
                    cachedObj is ProcedureExecutionResponse cachedResponse)
                {
                    sw.Stop();
                    var cacheLog = BuildExecutionLog(schemaName, procedureName, parametersMetadata, input, cachedResponse.Columns, cachedResponse.RowCount, (int)sw.ElapsedMilliseconds);
                    await _auditRepo.LogAsync(cacheLog, cancellationToken);

                    return Ok(new JSONResponseDTO<ProcedureExecutionResponse>
                    {
                        StatusCode = HttpStatusCode.OK,
                        Data = cachedResponse,
                        Id = cacheLog.Id
                    });
                }

                var rows = await _repo.ExecuteProcedureAsync(
                    schemaName,
                    procedureName,
                    dapperParams,
                    _commandTimeoutSeconds,
                    cancellationToken);

                sw.Stop();

                if (rows.Count > _maxRowLimit)
                    return BadRequest(new JSONResponseDTO<ProcedureExecutionResponse>
                    {
                        StatusCode = HttpStatusCode.BadRequest,
                        Message = $"Too many rows ({rows.Count}). Limit {_maxRowLimit}."
                    });

                var columns = rows.FirstOrDefault()?.Keys ?? Array.Empty<string>();

                // Build materialized response (avoid deferred LINQ chains holding compiler?generated closure types)
                var columnList = columns.ToArray();
                var rowList = rows
                    .Select(r => columnList.Select(c => r.TryGetValue(c, out var v) ? v : null).ToArray())
                    .ToArray();

                var response = new ProcedureExecutionResponse
                {
                    Columns = columnList,
                    Rows = rowList,
                    RowCount = rows.Count
                };

                if (request?.UseCache == true)
                    _cache.Set(cacheKey, response, TimeSpan.FromMinutes(5));

                var execLog = BuildExecutionLog(schemaName, procedureName, parametersMetadata, input, columnList, rows.Count, (int)sw.ElapsedMilliseconds, rows.FirstOrDefault());
                await _auditRepo.LogAsync(execLog, cancellationToken);

                return Ok(new JSONResponseDTO<ProcedureExecutionResponse>
                {
                    StatusCode = HttpStatusCode.OK,
                    Data = response,
                    Id = execLog.Id
                });
            }
            catch (SqlException sqlEx)
            {
                sw.Stop();
                _logger.LogError(sqlEx,
                    "SQL error executing procedure {Schema}.{Proc}. Number={Number} State={State} Line={Line}",
                    schemaName, procedureName, sqlEx.Number, sqlEx.State, sqlEx.LineNumber);

                var devMsg = _isDevelopment
                    ? $"SQL error ({sqlEx.Number}): {sqlEx.Message}"
                    : "SQL error executing stored procedure.";

                return BadRequest(new JSONResponseDTO<ProcedureExecutionResponse>
                {
                    StatusCode = HttpStatusCode.BadRequest,
                    Message = devMsg
                });
            }
            catch (OperationCanceledException)
            {
                sw.Stop();
                _logger.LogWarning("Execution cancelled for {Proc}", $"{schemaName}.{procedureName}");
                return StatusCode(499, new JSONResponseDTO<ProcedureExecutionResponse>
                {
                    StatusCode = (HttpStatusCode)499,
                    Message = "Request cancelled."
                });
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex, "Unhandled error executing procedure {Proc}", $"{schemaName}.{procedureName}");
                return StatusCode(500, new JSONResponseDTO<ProcedureExecutionResponse>
                {
                    StatusCode = HttpStatusCode.InternalServerError,
                    Message = $"Unhandled error: {ex.Message}"
                });
            }
        }

        private ProcedureExecutionLog BuildExecutionLog(
            string schemaName,
            string procedureName,
            IEnumerable<ProcedureParameter> metadata,
            Dictionary<string, object?> input,
            IEnumerable<string> columns,
            int rowCount,
            int durationMs,
            IDictionary<string, object>? sampleRow = null)
        {
            var log = new ProcedureExecutionLog
            {
                ExecutedAt = DateTime.UtcNow,
                SchemaName = schemaName,
                ProcedureName = procedureName,
                RowCount = rowCount,
                DurationMs = durationMs
            };

            foreach (var meta in metadata)
            {
                var keyName = NormalizeParamKey(meta.ParameterName);
                var providedKey = input.Keys.FirstOrDefault(k => string.Equals(NormalizeParamKey(k), keyName, StringComparison.OrdinalIgnoreCase));
                var providedValue = providedKey != null ? input[providedKey] : null;

                log.Parameters.Add(new ProcedureExecutionParameter
                {
                    ParameterName = meta.ParameterName,
                    DataType = meta.DataType,
                    IsOutput = meta.IsOutput,
                    IsNullable = meta.IsNullable,
                    ParameterValue = providedValue?.ToString()
                });
            }

            int ordinal = 0;
            foreach (var col in columns)
            {
                object? sampleValue = null;

                if (sampleRow != null)
                {
                    sampleRow.TryGetValue(col, out sampleValue);
                }

                log.Columns.Add(new ProcedureExecutionColumn
                {
                    ColumnOrdinal = ordinal++,
                    ColumnName = col,
                    DataType = sampleValue?.GetType().Name ?? "Unknown",
                    IsNullable = sampleValue == null || sampleValue == DBNull.Value
                });
            }

            return log;
        }

        private bool IsValidIdentifier(string name) =>
            !string.IsNullOrWhiteSpace(name) && _identifierRegex.IsMatch(name);

        private bool IsAllowed(string schemaName, string procName) => true;

        private string NormalizeParamKey(string key) =>
            key == null ? "" : (key.StartsWith("@") ? key[1..] : key);
    }
}