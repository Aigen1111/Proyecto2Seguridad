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
    public class UsuariosController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuthService _authService;
        private readonly IAuditoriaService _auditoriaService;
        private readonly ILogger<UsuariosController> _logger;

        public UsuariosController(
            ApplicationDbContext context,
            IAuthService authService,
            IAuditoriaService auditoriaService,
            ILogger<UsuariosController> logger)
        {
            _context = context;
            _authService = authService;
            _auditoriaService = auditoriaService;
            _logger = logger;
        }

        /// <summary>
        /// Obtiene lista de usuarios. Acceso: SuperAdmin, Auditor, Registrador (solo lectura).
        /// RF-04: Muestra username, email, rol, último login e IP.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetUsuarios()
        {
            try
            {
                var usuarios = await _context.Usuarios
                    .Include(u => u.Rol)
                    .Where(u => u.Activo)
                    .Select(u => new
                    {
                        u.Id,
                        u.Username,
                        u.Email,
                        rolId = u.RolId,
                        rol = new { u.Rol!.Id, u.Rol.Nombre },
                        u.UltimoLogin,
                        u.UltimaIP,
                        u.Activo,
                        u.FechaCreacion
                    })
                    .OrderBy(u => u.Id)
                    .ToListAsync();

                return Ok(usuarios);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo usuarios");
                return StatusCode(500, new { message = "Error en el servidor" });
            }
        }

        /// <summary>
        /// Obtiene un usuario por ID. Solo SuperAdmin.
        /// </summary>
        [HttpGet("{id}")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> GetUsuario(int id)
        {
            try
            {
                var usuario = await _context.Usuarios
                    .Include(u => u.Rol)
                    .Where(u => u.Id == id && u.Activo)
                    .Select(u => new
                    {
                        u.Id,
                        u.Username,
                        u.Email,
                        rolId = u.RolId,
                        rol = new { u.Rol!.Id, u.Rol.Nombre },
                        u.UltimoLogin,
                        u.UltimaIP,
                        u.Activo,
                        u.FechaCreacion
                    })
                    .FirstOrDefaultAsync();

                if (usuario == null)
                    return NotFound(new { message = "Usuario no encontrado" });

                return Ok(usuario);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error obteniendo usuario {id}");
                return StatusCode(500, new { message = "Error en el servidor" });
            }
        }

        /// <summary>
        /// Crea un nuevo usuario. Solo SuperAdmin. RF-04.
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> CreateUsuario([FromBody] CreateUsuarioRequest request)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors);
                return BadRequest(new { message = "Validación fallida", errors });
            }

            var ipAddress = GetIpAddress();

            // Verificar username único
            if (await _context.Usuarios.AnyAsync(u => u.Username == request.Username))
                return BadRequest(new { message = "El nombre de usuario ya está en uso" });

            // Verificar email único
            if (await _context.Usuarios.AnyAsync(u => u.Email == request.Email))
                return BadRequest(new { message = "El email ya está registrado" });

            // Verificar que el rol existe
            var rol = await _context.Roles.FindAsync(request.RolId);
            if (rol == null)
                return BadRequest(new { message = "El rol especificado no existe" });

            try
            {
                var usuarioId = GetUsuarioId();

                var nuevoUsuario = new Usuario
                {
                    Username = request.Username.Trim(),
                    Email = request.Email.Trim().ToLower(),
                    PasswordHash = _authService.HashPassword(request.Password),
                    RolId = request.RolId,
                    Activo = true,
                    FechaCreacion = DateTime.UtcNow
                };

                _context.Usuarios.Add(nuevoUsuario);
                await _context.SaveChangesAsync();

                await _auditoriaService.LogAsync(
                    usuarioId,
                    ipAddress,
                    "Usuario Creado",
                    "/api/usuarios",
                    "POST",
                    201,
                    $"Usuario '{nuevoUsuario.Username}' creado con rol '{rol.Nombre}'");

                return CreatedAtAction(nameof(GetUsuario), new { id = nuevoUsuario.Id }, new
                {
                    nuevoUsuario.Id,
                    nuevoUsuario.Username,
                    nuevoUsuario.Email,
                    rolId = nuevoUsuario.RolId,
                    rol = new { rol.Id, rol.Nombre },
                    nuevoUsuario.Activo,
                    nuevoUsuario.FechaCreacion
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creando usuario");
                return StatusCode(500, new { message = "Error en el servidor" });
            }
        }

        /// <summary>
        /// Actualiza datos de un usuario (username, email, rol). Solo SuperAdmin. RF-05.
        /// No permite cambiar contraseña desde aquí.
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> UpdateUsuario(int id, [FromBody] UpdateUsuarioRequest request)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors);
                return BadRequest(new { message = "Validación fallida", errors });
            }

            var ipAddress = GetIpAddress();

            try
            {
                var usuario = await _context.Usuarios
                    .Include(u => u.Rol)
                    .FirstOrDefaultAsync(u => u.Id == id && u.Activo);

                if (usuario == null)
                    return NotFound(new { message = "Usuario no encontrado" });

                // Verificar username único (excluyendo el mismo usuario)
                if (usuario.Username != request.Username &&
                    await _context.Usuarios.AnyAsync(u => u.Username == request.Username && u.Id != id))
                    return BadRequest(new { message = "El nombre de usuario ya está en uso" });

                // Verificar email único
                if (usuario.Email != request.Email.ToLower() &&
                    await _context.Usuarios.AnyAsync(u => u.Email == request.Email.ToLower() && u.Id != id))
                    return BadRequest(new { message = "El email ya está registrado" });

                // Verificar que el rol existe
                var rol = await _context.Roles.FindAsync(request.RolId);
                if (rol == null)
                    return BadRequest(new { message = "El rol especificado no existe" });

                var rolAnterior = usuario.Rol?.Nombre ?? "desconocido";
                usuario.Username = request.Username.Trim();
                usuario.Email = request.Email.Trim().ToLower();
                usuario.RolId = request.RolId;

                _context.Usuarios.Update(usuario);
                await _context.SaveChangesAsync();

                var actualizadorId = GetUsuarioId();
                var detalles = rolAnterior != rol.Nombre
                    ? $"Usuario '{usuario.Username}' actualizado. Rol cambiado de '{rolAnterior}' a '{rol.Nombre}'"
                    : $"Usuario '{usuario.Username}' actualizado";

                await _auditoriaService.LogAsync(
                    actualizadorId,
                    ipAddress,
                    rolAnterior != rol.Nombre ? "Cambio de Rol" : "Usuario Actualizado",
                    $"/api/usuarios/{id}",
                    "PUT",
                    200,
                    detalles);

                return Ok(new
                {
                    usuario.Id,
                    usuario.Username,
                    usuario.Email,
                    rolId = usuario.RolId,
                    rol = new { rol.Id, rol.Nombre },
                    usuario.UltimoLogin,
                    usuario.UltimaIP,
                    usuario.Activo
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error actualizando usuario {id}");
                return StatusCode(500, new { message = "Error en el servidor" });
            }
        }

        /// <summary>
        /// Elimina (desactiva) un usuario. Solo SuperAdmin. RF-04.
        /// Hace soft delete para preservar integridad referencial en auditoría.
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> DeleteUsuario(int id)
        {
            var ipAddress = GetIpAddress();
            var actualizadorId = GetUsuarioId();

            // Evitar que el SuperAdmin se elimine a sí mismo
            if (actualizadorId == id)
                return BadRequest(new { message = "No puedes eliminar tu propia cuenta" });

            try
            {
                var usuario = await _context.Usuarios
                    .Include(u => u.Rol)
                    .FirstOrDefaultAsync(u => u.Id == id && u.Activo);

                if (usuario == null)
                    return NotFound(new { message = "Usuario no encontrado" });

                // Soft delete: desactivar en lugar de borrar
                usuario.Activo = false;
                _context.Usuarios.Update(usuario);
                await _context.SaveChangesAsync();

                await _auditoriaService.LogAsync(
                    actualizadorId,
                    ipAddress,
                    "Usuario Eliminado",
                    $"/api/usuarios/{id}",
                    "DELETE",
                    200,
                    $"Usuario '{usuario.Username}' desactivado del sistema");

                return Ok(new { message = $"Usuario '{usuario.Username}' eliminado exitosamente" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error eliminando usuario {id}");
                return StatusCode(500, new { message = "Error en el servidor" });
            }
        }

        /// <summary>
        /// Obtiene la lista de roles disponibles. Todos los roles autenticados pueden ver esto.
        /// Necesario para llenar los selects del frontend.
        /// </summary>
        [HttpGet("roles")]
        public async Task<IActionResult> GetRoles()
        {
            try
            {
                var roles = await _context.Roles
                    .Select(r => new { r.Id, r.Nombre, r.Descripcion })
                    .OrderBy(r => r.Id)
                    .ToListAsync();

                return Ok(roles);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo roles");
                return StatusCode(500, new { message = "Error en el servidor" });
            }
        }

        private int? GetUsuarioId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            return int.TryParse(claim?.Value, out int id) ? id : null;
        }

        private string GetIpAddress()
        {
            return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0";
        }
    }
}
