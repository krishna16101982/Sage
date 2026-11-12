using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace SageAdminApi.AppFunctions
{
    /// <summary>
    /// Security response headers (replaces the NWebsec package used by the ixcheck reference).
    /// </summary>
    public class SecurityHeadersMiddleware
    {
        private readonly RequestDelegate _next;

        public SecurityHeadersMiddleware(RequestDelegate __next)
        {
            _next = __next;
        }

        public Task InvokeAsync(HttpContext _context)
        {
            var _headers = _context.Response.Headers;
            _headers["X-Content-Type-Options"] = "nosniff";
            _headers["X-Frame-Options"] = "DENY";
            _headers["Referrer-Policy"] = "no-referrer";
            // Strict CSP for the JSON API. Swagger UI (Development only) needs its own scripts and styles, so it is exempt.
            if (!_context.Request.Path.StartsWithSegments("/swagger"))
            {
                _headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
                _headers["Cache-Control"] = "no-store";
            }
            return _next(_context);
        }
    }
}
