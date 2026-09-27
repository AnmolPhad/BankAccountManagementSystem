using System.Diagnostics;

namespace BankAccountManagementSystem.API.Middleware
{
    /// <summary>
    /// Middleware that logs every HTTP request with method, path, status code,
    /// execution time (ms), and correlation ID.
    /// Does NOT log request bodies or sensitive data.
    /// </summary>
    public class RequestLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestLoggingMiddleware> _logger;

        public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var correlationId = context.Items.TryGetValue("CorrelationId", out var cid)
                ? cid?.ToString()
                : context.TraceIdentifier;

            var sw = Stopwatch.StartNew();

            await _next(context);

            sw.Stop();

            _logger.LogInformation(
                "{Method} {Path} responded {StatusCode} in {ElapsedMs}ms | CorrelationId: {CorrelationId}",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                sw.ElapsedMilliseconds,
                correlationId);
        }
    }
}
