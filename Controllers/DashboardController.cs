using Microsoft.AspNetCore.Mvc;

namespace StoredProcedureAPI.Controllers
{
    [ApiController]
    [Route("api/dashboard")]
    public class DashboardController : ControllerBase
    {
        private readonly DashboardService _service;

        public DashboardController(DashboardService service)
        {
            _service = service;
        }

        [HttpPost("execute")]
        public async Task<IActionResult> Execute([FromBody] ExecRequest req)
        {
            var result = await _service.ExecuteProcedureAsync(
                req.Schema,
                req.Procedure,
                req.Parameters ?? new Dictionary<string, object>()
            );

            return Ok(result);
        }
    }

    public class ExecRequest
    {
        public  required string Schema { get; set; }
        public required string Procedure { get; set; }
        public Dictionary<string, object>? Parameters { get; set; }
    }

}
