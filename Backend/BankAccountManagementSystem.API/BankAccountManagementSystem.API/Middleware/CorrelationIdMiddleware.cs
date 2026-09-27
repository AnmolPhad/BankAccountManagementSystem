namespace BankAccountManagementSystem.API.Middleware
{
    /// <summary>
    /// Middleware that generates or propagates an X-Correlation-ID header for each request.
    /// If the client sends an X-Correlation-ID header, it is reused.
    /// Otherwise a new GUID is generated.
    /// The correlation ID is stored in HttpContext.Items and added to the response header.
    /// </summary>
    public class CorrelationIdMiddleware
    {
        private const string CorrelationIdHeader = "X-Correlation-ID";
        private readonly RequestDelegate _next;

        public CorrelationIdMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Reuse the client's correlation ID, or generate a new one
            var correlationId = context.Request.Headers[CorrelationIdHeader].FirstOrDefault()
                                ?? Guid.NewGuid().ToString();

            // Store in Items so other middleware and the ExceptionMiddleware can access it
            context.Items["CorrelationId"] = correlationId;

            // Echo the correlation ID back to the caller
            context.Response.OnStarting(() =>
            {
                context.Response.Headers[CorrelationIdHeader] = correlationId;
                return Task.CompletedTask;
            });

            await _next(context);
        }
    }
}
