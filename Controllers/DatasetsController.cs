using Microsoft.AspNetCore.Mvc;
using StoredProcedureAPI.Repository;
using StoredProcedureAPI.Models;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Text.Json;

namespace StoredProcedureAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DatasetsController : ControllerBase
    {
        private readonly IDatasetRepository _repo;
        private readonly IConfiguration _config;

        public DatasetsController(IDatasetRepository repo, IConfiguration config)
        {
            _repo = repo;
            _config = config;
        }

        // POST api/datasets/procedure-execute
        // Executes the procedure, logs execution + columns, then creates a dataset.
        [HttpPost("procedure-execute")]
        public async Task<ActionResult<Dataset>> ExecuteProcedureAndCreateDataset(
            [FromBody] ExecuteAndCreateDatasetRequest request,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.Schema) || string.IsNullOrWhiteSpace(request.Procedure))
                return BadRequest("Schema and Procedure are required.");

            string connectionString = _config.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Missing connection string 'DefaultConnection'.");

            await using SqlConnection conn = new(connectionString);
            await conn.OpenAsync(ct);
            await using var dbTx = await conn.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
            var sqlTx = (SqlTransaction)dbTx;

            try
            {
                string fullyQualified = $"{request.Schema}.{request.Procedure}";
                using SqlCommand cmd = new(fullyQualified, conn, sqlTx)
                {
                    CommandType = CommandType.StoredProcedure,
                    CommandTimeout = 30
                };

                // Normalize and add parameters
                if (request.Parameters is not null)
                {
                    foreach (var kv in request.Parameters)
                    {
                        object? converted = NormalizeParameterValue(kv.Value);
                        cmd.Parameters.AddWithValue("@" + kv.Key, converted ?? DBNull.Value);
                    }
                }

                using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SchemaOnly, ct);
                var schema = reader.GetSchemaTable();
                if (schema == null || schema.Rows.Count == 0)
                    throw new InvalidOperationException("Procedure returned no columns.");

                const string insertExecSql = @"
INSERT INTO dbo.ProcedureExecutionLog (ProcedureName, ExecutedAtUtc)
VALUES (@ProcedureName, SYSUTCDATETIME());
SELECT CAST(SCOPE_IDENTITY() AS INT);";

                int executionId;
                using (SqlCommand execCmd = new(insertExecSql, conn, sqlTx))
                {
                    execCmd.Parameters.AddWithValue("@ProcedureName", fullyQualified);
                    executionId = Convert.ToInt32(await execCmd.ExecuteScalarAsync(ct));
                }

                const string insertColSql = @"
INSERT INTO dbo.ProcedureExecutionColumn
(ExecutionId, ColumnOrdinal, ColumnName, DataType)
VALUES (@ExecutionId, @ColumnOrdinal, @ColumnName, @DataType);";

                int ordinal = 0;
                foreach (DataRow row in schema.Rows)
                {
                    string colName = row["ColumnName"]?.ToString() ?? $"Column{ordinal}";
                    string dataType =
                        row["DataType"] is Type t
                            ? t.Name
                            : (row.Table.Columns.Contains("DataTypeName") ? row["DataTypeName"]?.ToString() : "Unknown") ?? "Unknown";

                    using SqlCommand colCmd = new(insertColSql, conn, sqlTx);
                    colCmd.Parameters.AddWithValue("@ExecutionId", executionId);
                    colCmd.Parameters.AddWithValue("@ColumnOrdinal", ordinal);
                    colCmd.Parameters.AddWithValue("@ColumnName", colName);
                    colCmd.Parameters.AddWithValue("@DataType", dataType);
                    await colCmd.ExecuteNonQueryAsync(ct);
                    ordinal++;
                }

                await sqlTx.CommitAsync(ct);

                int datasetId = await _repo.CreateFromProcedureExecutionAsync(
                    executionId,
                    request.Title,
                    request.Description,
                    ct);

                var ds = await _repo.GetDatasetAsync(datasetId, ct);
                return CreatedAtAction(nameof(Get), new { id = datasetId }, ds);
            }
            catch (Exception ex)
            {
                try { await sqlTx.RollbackAsync(ct); } catch { }
                return StatusCode(500, $"Failed to execute procedure: {ex.Message}");
            }
        }

        // GET api/datasets/{id}
        [HttpGet("{id:int}")]
        public async Task<ActionResult<Dataset>> Get(int id, CancellationToken ct)
        {
            var ds = await _repo.GetDatasetAsync(id, ct);
            if (ds is null) return NotFound();
            return Ok(ds);
        }

        // GET api/datasets/recent
        [HttpGet("recent")]
        public async Task<ActionResult<IEnumerable<Dataset>>> GetRecent([FromQuery] int top = 50, CancellationToken ct = default)
        {
            if (top <= 0) top = 50;
            var list = await _repo.GetRecentAsync(top, ct);
            return Ok(list);
        }

        // GET api/datasets/source-flags
        [HttpGet("source-flags")]
        public async Task<ActionResult<IEnumerable<SourceFlag>>> GetSourceFlags(CancellationToken ct)
        {
            var flags = await _repo.GetSourceFlagsAsync(ct);
            return Ok(flags);
        }

        // POST api/datasets/procedure
        [HttpPost("procedure")]
        public async Task<ActionResult> CreateFromProcedure([FromBody] CreateFromProcedureRequest request, CancellationToken ct)
        {
            int id = await _repo.CreateFromProcedureExecutionAsync(request.ProcedureExecutionId, request.Title, request.Description, ct);
            var ds = await _repo.GetDatasetAsync(id, ct);
            return CreatedAtAction(nameof(Get), new { id }, ds);
        }

        // POST api/datasets/builder
        [HttpPost("builder")]
        public async Task<ActionResult> CreateFromBuilder([FromBody] CreateFromBuilderRequest request, CancellationToken ct)
        {
            int id = await _repo.CreateFromBuilderAsync(request.BuilderId, request.Title, request.Description, ct);
            var ds = await _repo.GetDatasetAsync(id, ct);
            return CreatedAtAction(nameof(Get), new { id }, ds);
        }

        // POST api/datasets/inline
        [HttpPost("inline")]
        public async Task<ActionResult> CreateFromInline([FromBody] CreateFromInlineRequest request, CancellationToken ct)
        {
            int id = await _repo.CreateFromInlineAsync(request.InlineId, request.Title, request.Description, ct);
            var ds = await _repo.GetDatasetAsync(id, ct);
            return CreatedAtAction(nameof(Get), new { id }, ds);
        }

        // PUT api/datasets/{id}
        [HttpPut("{id:int}")]
        public async Task<ActionResult> Update(int id, [FromBody] UpdateDatasetRequest request, CancellationToken ct)
        {
            bool ok = await _repo.UpdateDatasetAsync(id, request.Title, request.Description, ct);
            if (!ok) return NotFound();
            var ds = await _repo.GetDatasetAsync(id, ct);
            return Ok(ds);
        }

        // PUT api/datasets/{id}/columns
        [HttpPut("{id:int}/columns")]
        public async Task<ActionResult> ReplaceColumns(int id, [FromBody] ReplaceColumnsRequest request, CancellationToken ct)
        {
            var tupleList = request.Columns.Select(c => (c.ColumnName, c.DataType));
            bool ok = await _repo.ReplaceColumnsAsync(id, tupleList, ct);
            if (!ok) return NotFound();
            var ds = await _repo.GetDatasetAsync(id, ct);
            return Ok(ds);
        }

        // Helper to convert JsonElement -> CLR types
        private static object? NormalizeParameterValue(object? value)
        {
            if (value is null) return null;

            if (value is JsonElement je)
            {
                if (je.ValueKind == JsonValueKind.Null) return null;
                switch (je.ValueKind)
                {
                    case JsonValueKind.String:
                        var s = je.GetString();
                        // Try DateTime / Guid parsing
                        if (DateTime.TryParse(s, out var dt)) return dt;
                        if (Guid.TryParse(s, out var g)) return g;
                        return s;
                    case JsonValueKind.Number:
                        if (je.TryGetInt32(out var i32)) return i32;
                        if (je.TryGetInt64(out var i64)) return i64;
                        if (je.TryGetDecimal(out var dec)) return dec;
                        if (je.TryGetDouble(out var dbl)) return dbl;
                        return je.GetRawText(); // fallback
                    case JsonValueKind.True:
                    case JsonValueKind.False:
                        return je.GetBoolean();
                    case JsonValueKind.Array:
                    case JsonValueKind.Object:
                        // Pass JSON text as NVARCHAR
                        return je.GetRawText();
                    default:
                        return je.GetRawText();
                }
            }

            return value; // Already a CLR type (int, string, bool, etc.)
        }
    }

    // Request DTOs
    public record CreateFromProcedureRequest(int ProcedureExecutionId, string Title, string? Description);
    public record CreateFromBuilderRequest(int BuilderId, string Title, string? Description);
    public record CreateFromInlineRequest(int InlineId, string Title, string? Description);
    public record UpdateDatasetRequest(string Title, string? Description);
    public record ReplaceColumnsRequest(ICollection<ReplaceColumnItem> Columns);
    public record ReplaceColumnItem(string ColumnName, string DataType);

    // Composite request DTO
    public record ExecuteAndCreateDatasetRequest(
        string Schema,
        string Procedure,
        Dictionary<string, object?>? Parameters,
        string Title,
        string? Description);
}