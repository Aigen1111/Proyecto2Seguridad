using ProyectoSeguridad.Models;
using ProyectoSeguridad.Data;
using Microsoft.EntityFrameworkCore;

namespace ProyectoSeguridad.Services
{
    public interface IAuditoriaService
    {
        Task LogAsync(int? usuarioId, string ipOrigen, string evento, string rutaSolicitada, 
            string? metodo = null, int? codigoHTTP = null, string? detalles = null);
    }

    public class AuditoriaService : IAuditoriaService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AuditoriaService> _logger;

        public AuditoriaService(ApplicationDbContext context, ILogger<AuditoriaService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task LogAsync(int? usuarioId, string ipOrigen, string evento, string rutaSolicitada,
            string? metodo = null, int? codigoHTTP = null, string? detalles = null)
        {
            try
            {
                var log = new AuditoriaLog
                {
                    Timestamp = DateTime.UtcNow,
                    IPOrigen = ipOrigen,
                    Evento = evento,
                    RutaSolicitada = rutaSolicitada,
                    UsuarioId = usuarioId,
                    Metodo = metodo,
                    CodigoHTTP = codigoHTTP,
                    Detalles = detalles
                };

                _context.AuditoriaLogs.Add(log);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registrando auditoría");
            }
        }
    }
}
