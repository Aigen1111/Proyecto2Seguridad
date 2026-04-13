using System.Security.Claims;
using ProyectoSeguridad.Services;

namespace ProyectoSeguridad.Middleware
{
    public class InactividadMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<InactividadMiddleware> _logger;

        public InactividadMiddleware(RequestDelegate next, ILogger<InactividadMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, IAuthService authService)
        {
            // Solo procesar si el usuario está autenticado
            if (context.User.Identity?.IsAuthenticated == true)
            {
                var usuarioIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier);
                if (int.TryParse(usuarioIdClaim?.Value, out int usuarioId))
                {
                    // Verificar inactividad (5 minutos)
                    if (await authService.CheckInactivityAsync(usuarioId, 5))
                    {
                        context.Response.StatusCode = 401;
                        await context.Response.WriteAsJsonAsync(new { message = "Sesión expirada por inactividad" });
                        return;
                    }

                    // Actualizar última actividad
                    var ipAddress = GetIpAddress(context);
                    await authService.UpdateLastActivityAsync(usuarioId, ipAddress);
                }
            }

            await _next(context);
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

    public static class InactividadExtensions
    {
        public static IApplicationBuilder UseInactividad(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<InactividadMiddleware>();
        }
    }
}
