using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoSeguridad.Models
{
    public class Usuario
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50, MinimumLength = 3)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        public int RolId { get; set; }

        public Rol? Rol { get; set; }

        public DateTime? UltimoLogin { get; set; }

        public string? UltimaIP { get; set; }

        public bool Activo { get; set; } = true;

        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

        // Para control de inactividad
        public DateTime? UltimaActividad { get; set; }

        // Para rate limiting
        public int IntentosFallidos { get; set; } = 0;

        public DateTime? FechaBloqueoCuenta { get; set; }
    }
}
