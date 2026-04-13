using System.ComponentModel.DataAnnotations;

namespace ProyectoSeguridad.Models
{
    public class AuditoriaLog
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [Required]
        [StringLength(50)]
        public string IPOrigen { get; set; } = string.Empty;

        [Required]
        [StringLength(500)]
        public string Evento { get; set; } = string.Empty;

        [Required]
        [StringLength(500)]
        public string RutaSolicitada { get; set; } = string.Empty;

        public int? UsuarioId { get; set; }

        public Usuario? Usuario { get; set; }

        [StringLength(20)]
        public string? Metodo { get; set; } // GET, POST, PUT, DELETE

        public int? CodigoHTTP { get; set; }

        [StringLength(1000)]
        public string? Detalles { get; set; }
    }
}
