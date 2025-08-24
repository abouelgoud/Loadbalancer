namespace LoadBalancer.Api.Middleware
{
    public sealed class RequestLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestLoggingMiddleware> _logger;

        public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
        { _next = next; _logger = logger; }

        public async Task InvokeAsync(HttpContext context)
        {
            var ip = context.Connection.RemoteIpAddress?.ToString();
            _logger.LogInformation("Incoming {Method} {Path} from {IP}", context.Request.Method, context.Request.Path + context.Request.QueryString, ip);
            await _next(context);
        }
    }
}
