using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace BankAccountManagementSystem.API.Middleware
{
    /// <summary>
    /// Global exception handling middleware.
    ///
    /// This middleware wraps the entire request pipeline. If any unhandled exception
    /// occurs in any controller, service, or middleware downstream, it is caught here.
    ///
    /// Why global middleware instead of try-catch in every controller?
    ///   - Consistency: all errors return the same JSON format
    ///   - DRY: no repetitive try-catch blocks in controllers
    ///   - Security: prevents accidental stack trace leaks in production
    ///   - Centralized logging: all exceptions are logged in one place
    ///
    /// Request flow:
    ///   Request → ExceptionMiddleware → ... rest of pipeline ...
    ///   If exception occurs anywhere ↑, it bubbles back to ExceptionMiddleware
    ///   ExceptionMiddleware catches it, logs it, returns clean JSON response
    /// </summary>
    public class ExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionMiddleware> _logger;
        private readonly IHostEnvironment _environment;

        public ExceptionMiddleware(
            RequestDelegate next,
            ILogger<ExceptionMiddleware> logger,
            IHostEnvironment environment)
        {
            _next = next;
            _logger = logger;
            _environment = environment;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                // Pass the request to the next middleware in the pipeline.
                // If no exception occurs, this method returns normally.
                await _next(context);
            }
            catch (Exception ex)
            {
                // Retrieve correlation ID set by CorrelationIdMiddleware (if present)
                var correlationId = context.Items.TryGetValue("CorrelationId", out var cid)
                    ? cid?.ToString()
                    : context.TraceIdentifier;

                // Log the full exception details (safe — this goes to your log files, not the response)
                _logger.LogError(ex,
                    "Unhandled exception on {Method} {Path} | CorrelationId: {CorrelationId}",
                    context.Request.Method,
                    context.Request.Path,
                    correlationId);

                // Return a clean JSON error response to the client
                await HandleExceptionAsync(context, ex, correlationId);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception, string? correlationId)
        {
            context.Response.ContentType = "application/json";

            // Determine the HTTP status code based on exception type.
            // Switch uses pattern matching — most specific types must come BEFORE base types.
            var statusCode = exception switch
            {
                // 409 Conflict: Two concurrent requests tried to update the same account balance.
                // EF Core detected that RowVersion changed between read and write → another request
                // already committed a balance change. The client should retry the operation.
                DbUpdateConcurrencyException => (int)HttpStatusCode.Conflict,              // 409

                // 401 Unauthorized: Identity/JWT failures.
                UnauthorizedAccessException  => (int)HttpStatusCode.Unauthorized,          // 401

                // 404 Not Found: Resource does not exist.
                KeyNotFoundException         => (int)HttpStatusCode.NotFound,              // 404

                // 400 Bad Request: Invalid input from the caller.
                ArgumentException            => (int)HttpStatusCode.BadRequest,            // 400

                // 422 Unprocessable Entity: Input is syntactically valid but violates business rules.
                // Used for domain exceptions such as:
                //   InsufficientBalanceException  → not enough funds
                //   AccountInactiveException      → account is closed
                //   InvalidTransactionException   → rule violation (e.g. Savings cannot transfer)
                //   InvalidAccountTypeException   → wrong account type for the operation
                InvalidOperationException    => 422,                                       // 422

                // 500 Internal Server Error: Unexpected failures.
                _                            => (int)HttpStatusCode.InternalServerError    // 500
            };

            context.Response.StatusCode = statusCode;

            // In development: include the exception message (helpful for debugging)
            // In production:  return a generic message (security best practice)
            // Never expose stack traces, SQL details, passwords, or JWT tokens.
            var message = _environment.IsDevelopment()
                ? exception.Message
                : "Something went wrong. Please try again later.";

            var response = new
            {
                success = false,
                message = message,
                statusCode = statusCode,
                correlationId = correlationId
            };

            // Serialize with camelCase to match JSON conventions
            var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var json = JsonSerializer.Serialize(response, options);

            await context.Response.WriteAsync(json);
        }
    }
}
