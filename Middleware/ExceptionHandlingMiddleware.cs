using System.Net;
using System.Text.Json;

namespace StoredProcedureAPI.Middleware
{
    /// <summary>
    /// Middleware for handling exceptions globally in the application.
    /// Catches unhandled exceptions, logs them, and returns a standardized error response.
    /// </summary>
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExceptionHandlingMiddleware"/> class.
        /// </summary>
        /// <param //name="next">The next middleware in the pipeline.</param>
        /// <param //name="logger">Logger for logging exceptions.</param>
        public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        /// <summary>
        /// Invokes the middleware logic.
        /// Catches exceptions thrown by downstream middleware/components.
        /// </summary>
        /// <param //name="context">The current HTTP context.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                // Call the next middleware in the pipeline
                await _next(context);
            }
            catch (Exception ex)
            {
                // Log the exception
                _logger.LogError(ex, "Unhandled exception occurred.");

                // Set response content type and status code
                context.Response.ContentType = "application/json";
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

                // Create a standardized error response
                var response = new
                {
                    StatusCode = context.Response.StatusCode,
                    Message = ex.Message
                };

                // Write the error response as JSON
                await context.Response.WriteAsync(JsonSerializer.Serialize(response));
            }
        }
    }
}
