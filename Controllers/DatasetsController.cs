using Microsoft.AspNetCore.Mvc;
using StoredProcedureAPI.Repository;
using StoredProcedureAPI.Models;
using StoredProcedureAPI.DTOs;
using System.Net;

namespace StoredProcedureAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DatasetsController : ControllerBase
    {
        private readonly IDatasetRepository _repo;

        public DatasetsController(IDatasetRepository repo)
        {
            _repo = repo;
        }

        // POST api/datasets (unified create)
        [HttpPost]
        public async Task<ActionResult<JSONResponseDTO<Dataset>>> CreateUnified(
            [FromBody] CreateDatasetUnifiedRequest request,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
                return BadRequest(WrapError<Dataset>("Title is required.", HttpStatusCode.BadRequest));

            if (request.SourceType is < 1 or > 3)
                return BadRequest(WrapError<Dataset>("SourceType must be 1=Builder, 2=Inline, 3=Procedure.", HttpStatusCode.BadRequest));

            if (request.Data is null)
                return BadRequest(WrapError<Dataset>("Data object is required.", HttpStatusCode.BadRequest));

            try
            {
                int datasetId;
                switch (request.SourceType)
                {
                    case 1: // Builder
                        if (request.Data.Builder is null)
                            return BadRequest(WrapError<Dataset>("Data.Builder is required for SourceType=1.", HttpStatusCode.BadRequest));
                        if (request.Data.Builder.BuilderId <= 0)
                            return BadRequest(WrapError<Dataset>("BuilderId must be > 0.", HttpStatusCode.BadRequest));

                        datasetId = await _repo.CreateFromBuilderAsync(
                            request.Data.Builder.BuilderId,
                            request.Title,
                            request.Description,
                            ct);

                        if (request.Data.Builder.Columns?.Count > 0)
                        {
                            var tuples = request.Data.Builder.Columns.Select(c => (c.ColumnName, c.DataType));
                            await _repo.ReplaceColumnsAsync(datasetId, tuples, ct);
                        }
                        break;

                    case 2: // Inline
                        if (request.Data.Inline is null)
                            return BadRequest(WrapError<Dataset>("Data.Inline is required for SourceType=2.", HttpStatusCode.BadRequest));
                        if (request.Data.Inline.InlineId <= 0)
                            return BadRequest(WrapError<Dataset>("InlineId must be > 0.", HttpStatusCode.BadRequest));

                        datasetId = await _repo.CreateFromInlineAsync(
                            request.Data.Inline.InlineId,
                            request.Title,
                            request.Description,
                            ct);

                        if (request.Data.Inline.Columns?.Count > 0)
                        {
                            var tuples = request.Data.Inline.Columns.Select(c => (c.ColumnName, c.DataType));
                            await _repo.ReplaceColumnsAsync(datasetId, tuples, ct);
                        }
                        break;

                    case 3: // Procedure
                        if (request.Data.Procedure is null)
                            return BadRequest(WrapError<Dataset>("Data.Procedure is required for SourceType=3.", HttpStatusCode.BadRequest));
                        if (request.Data.Procedure.ProcedureExecutionId <= 0)
                            return BadRequest(WrapError<Dataset>("ProcedureExecutionId must be > 0.", HttpStatusCode.BadRequest));

                        datasetId = await _repo.CreateFromProcedureExecutionAsync(
                            request.Data.Procedure.ProcedureExecutionId,
                            request.Title,
                            request.Description,
                            ct);
                        break;

                    default:
                        return BadRequest(WrapError<Dataset>("Unsupported SourceType.", HttpStatusCode.BadRequest));
                }

                var ds = await _repo.GetDatasetAsync(datasetId, ct);
                return CreatedAtAction(nameof(Get),
                    new { id = datasetId },
                    new JSONResponseDTO<Dataset>
                    {
                        StatusCode = HttpStatusCode.Created,
                        Id = datasetId,
                        Data = ds
                    });
            }
            catch (InvalidOperationException ex)
            {
                var status = ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase)
                    ? HttpStatusCode.NotFound
                    : HttpStatusCode.BadRequest;
                return StatusCode((int)status, WrapError<Dataset>(ex.Message, status));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(WrapError<Dataset>(ex.Message, HttpStatusCode.BadRequest));
            }
            catch (Exception ex)
            {
                return StatusCode(500, WrapError<Dataset>("Failed to create dataset.", HttpStatusCode.InternalServerError, ex.Message));
            }
        }

        // GET api/datasets/{id}
        [HttpGet("{id:int}")]
        public async Task<ActionResult<JSONResponseDTO<Dataset>>> Get(int id, CancellationToken ct)
        {
            var ds = await _repo.GetDatasetAsync(id, ct);
            if (ds is null)
                return NotFound(WrapError<Dataset>($"Dataset {id} not found.", HttpStatusCode.NotFound));

            return Ok(new JSONResponseDTO<Dataset>
            {
                StatusCode = HttpStatusCode.OK,
                Id = ds.DataSetId,
                Data = ds
            });
        }

        // GET api/datasets/recent
        [HttpGet("recent")]
        public async Task<ActionResult<JSONResponseDTO<IEnumerable<Dataset>>>> GetRecent(
            [FromQuery] int top = 50,
            CancellationToken ct = default)
        {
            if (top <= 0) top = 50;
            var list = await _repo.GetRecentAsync(top, ct);
            return Ok(new JSONResponseDTO<IEnumerable<Dataset>>
            {
                StatusCode = HttpStatusCode.OK,
                Data = list
            });
        }

        // GET api/datasets/source-flags
        [HttpGet("source-flags")]
        public async Task<ActionResult<JSONResponseDTO<IEnumerable<SourceFlag>>>> GetSourceFlags(CancellationToken ct)
        {
            var flags = await _repo.GetSourceFlagsAsync(ct);
            return Ok(new JSONResponseDTO<IEnumerable<SourceFlag>>
            {
                StatusCode = HttpStatusCode.OK,
                Data = flags
            });
        }

        // PUT api/datasets/{id}
        [HttpPut("{id:int}")]
        public async Task<ActionResult<JSONResponseDTO<Dataset>>> Update(
            int id,
            [FromBody] UpdateDatasetRequest request,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
                return BadRequest(WrapError<Dataset>("Title is required.", HttpStatusCode.BadRequest));

            bool ok = await _repo.UpdateDatasetAsync(id, request.Title, request.Description, ct);
            if (!ok)
                return NotFound(WrapError<Dataset>($"Dataset {id} not found.", HttpStatusCode.NotFound));

            var ds = await _repo.GetDatasetAsync(id, ct);
            return Ok(new JSONResponseDTO<Dataset>
            {
                StatusCode = HttpStatusCode.OK,
                Id = id,
                Data = ds
            });
        }

        // PUT api/datasets/{id}/columns
        [HttpPut("{id:int}/columns")]
        public async Task<ActionResult<JSONResponseDTO<Dataset>>> ReplaceColumns(
            int id,
            [FromBody] ReplaceColumnsRequest request,
            CancellationToken ct)
        {
            if (request.Columns == null || request.Columns.Count == 0)
                return BadRequest(WrapError<Dataset>("Columns collection is required and cannot be empty.", HttpStatusCode.BadRequest));

            var tuples = request.Columns.Select(c => (c.ColumnName, c.DataType));
            bool ok = await _repo.ReplaceColumnsAsync(id, tuples, ct);
            if (!ok)
                return NotFound(WrapError<Dataset>($"Dataset {id} not found.", HttpStatusCode.NotFound));

            var ds = await _repo.GetDatasetAsync(id, ct);
            return Ok(new JSONResponseDTO<Dataset>
            {
                StatusCode = HttpStatusCode.OK,
                Id = id,
                Data = ds
            });
        }

        private static JSONResponseDTO<T> WrapError<T>(string message, HttpStatusCode code, string? detail = null) =>
            new()
            {
                StatusCode = code,
                Message = detail is null ? message : $"{message} | {detail}"
            };
    }

    // Unified create request
    public record CreateDatasetUnifiedRequest(
        int SourceType,          // 1=Builder, 2=Inline, 3=Procedure
        string Title,
        string? Description,
        DatasetSourceData Data
    );

    public record DatasetSourceData(
        BuilderSourceDto? Builder,
        InlineSourceDto? Inline,
        ProcedureSourceDto? Procedure
    );

    public record BuilderSourceDto(
        int BuilderId,
        ICollection<ColumnSpec>? Columns
    );

    public record InlineSourceDto(
        int InlineId,
        ICollection<ColumnSpec>? Columns
    );

    public record ProcedureSourceDto(int ProcedureExecutionId);

    public record ColumnSpec(string ColumnName, string DataType);

    public record UpdateDatasetRequest(string Title, string? Description);

    public record ReplaceColumnsRequest(ICollection<ColumnSpec> Columns);
}