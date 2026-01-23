namespace RecoveryService.Security
{
    public class ApiKeyMiddleware
    {
        private readonly RequestDelegate _next;
        private const string HeaderName = "X-Recovery-Key";

        public ApiKeyMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context, IConfiguration config)
        {
            if (!context.Request.Headers.TryGetValue(HeaderName, out var key) ||
                key != config["Recovery:ApiKey"])
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("Unauthorized");
                return;
            }

            await _next(context);
        }
    }

}
