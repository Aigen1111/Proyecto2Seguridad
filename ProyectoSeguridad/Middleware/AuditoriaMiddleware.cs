using System.Security.Claims;
using ProyectoSeguridad.Services;

namespace ProyectoSeguridad.Middleware
{
    public class AuditoriaMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<AuditoriaMiddleware> _logger;

        public AuditoriaMiddleware(RequestDelegate next, ILogger<AuditoriaMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, IAuditoriaService auditoriaService)
        {
            var ipAddress = GetIpAddress(context);
            var usuarioId = GetUsuarioId(context);
            var ruta = context.Request.Path.ToString();
            var metodo = context.Request.Method;

            // Capturar el response status code
            var originalBodyStream = context.Response.Body;
            using (var responseBody = new MemoryStream())
            {
                context.Response.Body = responseBody;

                try
                {
                    await _next(context);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error no capturado en middleware");
                    context.Response.StatusCode = 500;
                }

                var statusCode = context.Response.StatusCode;

                // Log de 403 Forbidden y 401 Unauthorized
                if (statusCode == 403)
                {
                    await auditoriaService.LogAsync(
                        usuarioId,
                        ipAddress,
                        "Acceso Denegado (Forbidden)",
                        ruta,
                        metodo,
                        statusCode,
                        $"Acceso denegado a {ruta}"
                    );
                }
                else if (statusCode == 401)
                {
                    await auditoriaService.LogAsync(
                        null,
                        ipAddress,
                        "No Autorizado (Unauthorized)",
                        ruta,
                        metodo,
                        statusCode,
                        $"Intento sin autenticación en {ruta}"
                    );
                }

                // Log de operaciones sensibles
                if ((metodo == "POST" || metodo == "PUT" || metodo == "DELETE") && statusCode == 200)
                {
                    await auditoriaService.LogAsync(
                        usuarioId,
                        ipAddress,
                        $"Operación {metodo}",
                        ruta,
                        metodo,
                        statusCode,
                        $"Operación exitosa en {ruta}"
                    );
                }

                await responseBody.CopyToAsync(originalBodyStream);
            }
        }

        private int? GetUsuarioId(HttpContext context)
        {
            var usuarioIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier);
            if (int.TryParse(usuarioIdClaim?.Value, out int usuarioId))
            {
                return usuarioId;
            }
            return null;
        }

        private string GetIpAddress(HttpContext context)
        {
            if (context.Connection.RemoteIpAddress != null)
            {
                return context.Connection.RemoteIpAddress.ToString();
            }
            return "0.0.0.0";
        }
    }

    public static class AuditoriaExtensions
    {
        public static IApplicationBuilder UseAuditoria(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<AuditoriaMiddleware>();
        }
    }
}
