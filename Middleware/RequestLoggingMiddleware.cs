using System.Diagnostics;
using System.Security.Claims;

namespace CurrencyConverterApi.Middleware
{
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
            var sw = Stopwatch.StartNew();
            var correlationId = context.Request.Headers["X-Correlation-ID"].ToString() ?? Guid.NewGuid().ToString();
            context.Response.Headers.Add("X-Correlation-ID", correlationId);

            try
            {
                var clientId = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anonymous";
                var clientIp = context.Connection.RemoteIpAddress?.ToString();

                _logger.LogInformation(
                    "Request started: {Method} {Path} from {ClientIp} (ClientId: {ClientId}, CorrelationId: {CorrelationId})",
                    context.Request.Method,
                    context.Request.Path,
                    clientIp,
                    clientId,
                    correlationId);

                await _next(context);

                sw.Stop();
                _logger.LogInformation(
                    "Request completed: {Method} {Path} - Status: {StatusCode} - Duration: {ElapsedMilliseconds}ms (CorrelationId: {CorrelationId})",
                    context.Request.Method,
                    context.Request.Path,
                    context.Response.StatusCode,
                    sw.ElapsedMilliseconds,
                    correlationId);
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex,
                    "Request failed: {Method} {Path} - Duration: {ElapsedMilliseconds}ms (CorrelationId: {CorrelationId})",
                    context.Request.Method,
                    context.Request.Path,
                    sw.ElapsedMilliseconds,
                    correlationId);
                throw;
            }
        }
    }
} 