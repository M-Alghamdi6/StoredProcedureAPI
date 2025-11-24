using Microsoft.AspNetCore.Mvc;
using StoredProcedureAPI.Repository;
using StoredProcedureAPI.DTOs;
using StoredProcedureAPI.Models;
using System.Net;

namespace StoredProcedureAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ExecutionLogsController : ControllerBase
    {
        private readonly IExecutionLogRepository _repo;

        public ExecutionLogsController(IExecutionLogRepository repo)
        {
            _repo = repo;
        }

        [HttpGet("recent")]
        public async Task<ActionResult<JSONResponseDTO<IEnumerable<ProcedureExecutionLog>>>> GetRecent([FromQuery] int top = 50, CancellationToken ct = default)
        {
            var data = await _repo.GetRecentAsync(top, ct);
            return Ok(new JSONResponseDTO<IEnumerable<ProcedureExecutionLog>>
            {
                StatusCode = HttpStatusCode.OK,
                Data = data
            });
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<JSONResponseDTO<ProcedureExecutionLog>>> GetById(int id, CancellationToken ct = default)
        {
            var log = await _repo.GetByIdAsync(id, ct);
            if (log == null)
                return NotFound(new JSONResponseDTO<ProcedureExecutionLog>
                {
                    StatusCode = HttpStatusCode.NotFound,
                    Message = "Log not found."
                });

            return Ok(new JSONResponseDTO<ProcedureExecutionLog>
            {
                StatusCode = HttpStatusCode.OK,
                Data = log,
                Id = log.Id
            });
        }

        [HttpGet("query")]
        public async Task<ActionResult<JSONResponseDTO<IEnumerable<ProcedureExecutionLog>>>> Query(
            [FromQuery] string? schemaName,
            [FromQuery] string? procedureName,
            [FromQuery] DateTime? fromUtc,
            [FromQuery] DateTime? toUtc,
            [FromQuery] int top = 100,
            CancellationToken ct = default)
        {
            var data = await _repo.QueryAsync(schemaName, procedureName, fromUtc, toUtc, top, ct);
            return Ok(new JSONResponseDTO<IEnumerable<ProcedureExecutionLog>>
            {
                StatusCode = HttpStatusCode.OK,
                Data = data
            });
        }
    
    [HttpGet("latest")]
        public async Task<ActionResult<JSONResponseDTO<ProcedureExecutionLog>>> GetLatest(CancellationToken ct = default)
        {
            var log = await _repo.GetLatestAsync(ct);
            if (log == null)
                return NotFound(new JSONResponseDTO<ProcedureExecutionLog>
                {
                    StatusCode = HttpStatusCode.NotFound,
                    Message = "No logs found."
                });

            return Ok(new JSONResponseDTO<ProcedureExecutionLog>
            {
                StatusCode = HttpStatusCode.OK,
                Data = log,
                Id = log.Id
            });
        }

    } }