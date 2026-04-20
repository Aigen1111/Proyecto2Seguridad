using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProyectoSeguridad.Services;
using ProyectoSeguridad.Data;
using Microsoft.EntityFrameworkCore;

namespace ProyectoSeguridad.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AuditoriaController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AuditoriaController> _logger;

        public AuditoriaController(
            ApplicationDbContext context,
            ILogger<AuditoriaController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Obtiene logs de auditoría. Solo SuperAdmin y Auditor.
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "SuperAdmin")]

        public async Task<IActionResult> GetAuditLogs([FromQuery] int? usuarioId, [FromQuery] string? ipOrigen, [FromQuery] int diasAtras = 7)
        {
            try
            {
                var fecha = DateTime.UtcNow.AddDays(-diasAtras);

                var query = _context.AuditoriaLogs
                    .Where(a => a.Timestamp >= fecha)
                    .OrderByDescending(a => a.Timestamp);

                if (usuarioId.HasValue)
                {
                    query = (IOrderedQueryable<Models.AuditoriaLog>)query.Where(a => a.UsuarioId == usuarioId);
                }

                if (!string.IsNullOrEmpty(ipOrigen))
                {
                    query = (IOrderedQueryable<Models.AuditoriaLog>)query.Where(a => a.IPOrigen == ipOrigen);
                }

                var logs = await query.Take(1000).ToListAsync();

                return Ok(new { total = logs.Count, logs });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo registros de auditoría");
                return StatusCode(500, new { message = "Error en el servidor" });
            }
        }

        /// <summary>
        /// Obtiene logs de accesos denegados (403). Solo SuperAdmin y Auditor
        /// </summary>
        [HttpGet("forbidden")]
        [Authorize(Roles = "SuperAdmin,Auditor")]
        public async Task<IActionResult> GetForbiddenAttempts()
        {
            try
            {
                var logs = await _context.AuditoriaLogs
                    .Where(a => a.CodigoHTTP == 403)
                    .OrderByDescending(a => a.Timestamp)
                    .Take(500)
                    .ToListAsync();

                return Ok(new { total = logs.Count, logs });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo registros de acceso denegado");
                return StatusCode(500, new { message = "Error en el servidor" });
            }
        }

        /// <summary>
        /// Obtiene logs de intentos fallidos de login. Solo SuperAdmin
        /// </summary>
        [HttpGet("failed-logins")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> GetFailedLogins()
        {
            try
            {
                var logs = await _context.AuditoriaLogs
                    .Where(a => a.Evento.Contains("Login Fallido"))
                    .OrderByDescending(a => a.Timestamp)
                    .Take(500)
                    .ToListAsync();

                return Ok(new { total = logs.Count, logs });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo logs de login fallido");
                return StatusCode(500, new { message = "Error en el servidor" });
            }
        }

        /// <summary>
        /// Obtiene estadísticas de auditoría. Solo SuperAdmin
        /// </summary>
        [HttpGet("stats")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> GetAuditStats()
        {
            try
            {
                var hoy = DateTime.UtcNow.Date;
                var logs = _context.AuditoriaLogs;

                var stats = new
                {
                    totalLogs = await logs.CountAsync(),
                    logsHoy = await logs.Where(a => a.Timestamp >= hoy).CountAsync(),
                    accesoDenegado = await logs.Where(a => a.CodigoHTTP == 403).CountAsync(),
                    noAutorizado = await logs.Where(a => a.CodigoHTTP == 401).CountAsync(),
                    ipsUnicas = await logs.Select(a => a.IPOrigen).Distinct().CountAsync()
                };

                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo estadísticas de auditoría");
                return StatusCode(500, new { message = "Error en el servidor" });
            }
        }
    }
}
