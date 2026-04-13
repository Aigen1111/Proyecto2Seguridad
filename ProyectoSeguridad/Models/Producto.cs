using System.ComponentModel.DataAnnotations;

namespace ProyectoSeguridad.Models
{
    public class Producto
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(20)]
        [RegularExpression(@"^[A-Z0-9]+$", ErrorMessage = "El código debe ser alfanumérico en mayúsculas")]
        public string Codigo { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string Nombre { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Descripcion { get; set; }

        [Required]
        [Range(0, 1000000)]
        public int Cantidad { get; set; }

        [Required]
        [Range(0.01, 999999.99)]
        [DataType(DataType.Currency)]
        public decimal Precio { get; set; }

        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

        public DateTime? FechaActualizacion { get; set; }

        public int? CreadorId { get; set; }

        public Usuario? Creador { get; set; }
    }
}
