using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProyectoSeguridad.Models;
using ProyectoSeguridad.Services;
using ProyectoSeguridad.Data;
using ProyectoSeguridad.Utilities;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace ProyectoSeguridad.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ProductosController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditoriaService _auditoriaService;
        private readonly ILogger<ProductosController> _logger;

        public ProductosController(
            ApplicationDbContext context,
            IAuditoriaService auditoriaService,
            ILogger<ProductosController> logger)
        {
            _context = context;
            _auditoriaService = auditoriaService;
            _logger = logger;
        }

        /// <summary>
        /// Obtiene lista de productos. Acceso: SuperAdmin, Auditor, Registrador
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetProductos()
        {
            try
            {
                var productos = await _context.Productos
                    .Include(p => p.Creador)
                    .ToListAsync();

                return Ok(productos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo productos");
                return StatusCode(500, new { message = "Error en el servidor" });
            }
        }

        /// <summary>
        /// Obtiene un producto por ID.
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetProducto(int id)
        {
            try
            {
                var producto = await _context.Productos
                    .Include(p => p.Creador)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (producto == null)
                {
                    return NotFound(new { message = "Producto no encontrado" });
                }

                return Ok(producto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error obteniendo producto {id}");
                return StatusCode(500, new { message = "Error en el servidor" });
            }
        }

        /// <summary>
        /// Crea un nuevo producto. Solo SuperAdmin y Registrador
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "SuperAdmin,Registrador")]
        public async Task<IActionResult> CreateProducto([FromBody] ProductoDto dto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors);
                return BadRequest(new { message = "Validación fallida", errors });
            }

            // Validación adicional del backend
            if (string.IsNullOrWhiteSpace(dto.Codigo) || dto.Codigo.Length > 20)
            {
                return BadRequest(new { message = "Código inválido" });
            }

            // Verificar código único (prevención de SQL Injection usando LINQ)
            if (await _context.Productos.AnyAsync(p => p.Codigo == dto.Codigo))
            {
                return BadRequest(new { message = "El código de producto ya existe" });
            }

            try
            {
                var usuarioId = GetUsuarioId();

                var producto = new Producto
                {
                    Codigo = dto.Codigo,
                    Nombre = dto.Nombre,
                    Descripcion = dto.Descripcion,
                    Cantidad = dto.Cantidad,
                    Precio = dto.Precio,
                    CreadorId = usuarioId,
                    FechaCreacion = DateTime.UtcNow
                };

                _context.Productos.Add(producto);
                await _context.SaveChangesAsync();

                await _auditoriaService.LogAsync(
                    usuarioId,
                    GetIpAddress(),
                    "Producto Creado",
                    $"/api/productos",
                    "POST",
                    201,
                    $"Producto {producto.Codigo} creado exitosamente");

                return CreatedAtAction(nameof(GetProducto), new { id = producto.Id }, producto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creando producto");
                return StatusCode(500, new { message = "Error en el servidor" });
            }
        }

        /// <summary>
        /// Actualiza un producto. Solo SuperAdmin
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> UpdateProducto(int id, [FromBody] ProductoDto dto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors);
                return BadRequest(new { message = "Validación fallida", errors });
            }

            try
            {
                var producto = await _context.Productos.FindAsync(id);
                if (producto == null)
                {
                    return NotFound(new { message = "Producto no encontrado" });
                }

                // Verificar que el código no se duplique
                if (producto.Codigo != dto.Codigo &&
                    await _context.Productos.AnyAsync(p => p.Codigo == dto.Codigo))
                {
                    return BadRequest(new { message = "El código de producto ya existe" });
                }

                producto.Codigo = dto.Codigo;
                producto.Nombre = dto.Nombre;
                producto.Descripcion = dto.Descripcion;
                producto.Cantidad = dto.Cantidad;
                producto.Precio = dto.Precio;
                producto.FechaActualizacion = DateTime.UtcNow;

                _context.Productos.Update(producto);
                await _context.SaveChangesAsync();

                var usuarioId = GetUsuarioId();
                await _auditoriaService.LogAsync(
                    usuarioId,
                    GetIpAddress(),
                    "Producto Actualizado",
                    $"/api/productos/{id}",
                    "PUT",
                    200,
                    $"Producto {producto.Codigo} actualizado exitosamente");

                return Ok(producto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error actualizando producto {id}");
                return StatusCode(500, new { message = "Error en el servidor" });
            }
        }

        /// <summary>
        /// Elimina un producto. Solo SuperAdmin
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> DeleteProducto(int id)
        {
            try
            {
                var producto = await _context.Productos.FindAsync(id);
                if (producto == null)
                {
                    return NotFound(new { message = "Producto no encontrado" });
                }

                _context.Productos.Remove(producto);
                await _context.SaveChangesAsync();

                var usuarioId = GetUsuarioId();
                await _auditoriaService.LogAsync(
                    usuarioId,
                    GetIpAddress(),
                    "Producto Eliminado",
                    $"/api/productos/{id}",
                    "DELETE",
                    200,
                    $"Producto {producto.Codigo} eliminado exitosamente");

                return Ok(new { message = "Producto eliminado" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error eliminando producto {id}");
                return StatusCode(500, new { message = "Error en el servidor" });
            }
        }

        private int? GetUsuarioId()
        {
            var usuarioIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (int.TryParse(usuarioIdClaim?.Value, out int usuarioId))
            {
                return usuarioId;
            }
            return null;
        }

        private string GetIpAddress()
        {
            if (HttpContext.Connection.RemoteIpAddress != null)
            {
                return HttpContext.Connection.RemoteIpAddress.ToString();
            }
            return "0.0.0.0";
        }
    }
}
