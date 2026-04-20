using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProyectoSeguridad.Services;
using ProyectoSeguridad.Utilities;

namespace ProyectoSeguridad.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IAuditoriaService _auditoriaService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            IAuthService authService,
            IAuditoriaService auditoriaService,
            ILogger<AuthController> logger)
        {
            _authService = authService;
            _auditoriaService = auditoriaService;
            _logger = logger;
        }

        /// <summary>
        /// Endpoint de login. Retorna JWT en Cookie HttpOnly y Secure
        /// </summary>
        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            // Validación en el backend (además de los validadores).
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors);
                return BadRequest(new { message = "Validación fallida", errors });
            }

            var ipAddress = GetIpAddress();

            var (success, token, message) = await _authService.LoginAsync(
                request.Username,
                request.Password,
                ipAddress);

            if (!success)
            {
                await _auditoriaService.LogAsync(
                    null,
                    ipAddress,
                    "Login Fallido",
                    "/api/auth/login",
                    "POST",
                    401,
                    message);

                return Unauthorized(new LoginResponse
                {
                    Success = false,
                    Message = message
                });
            }

            // Configurar cookie HttpOnly y Secure
            Response.Cookies.Append("Authorization", token!, new CookieOptions
            {
                HttpOnly = true,
                Secure = false, // Cambiar a true en producción con HTTPS
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.AddHours(1)
            });

            await _auditoriaService.LogAsync(
                null, // Se actualizará con el usuario ID después
                ipAddress,
                "Login Exitoso",
                "/api/auth/login",
                "POST",
                200);

            return Ok(new LoginResponse
            {
                Success = true,
                Token = token,
                Message = message
            });
        }

        /// <summary>
        /// Endpoint de registro para crear nuevos usuarios con rol Registrador
        /// </summary>
        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors);
                return BadRequest(new { message = "Validación fallida", errors });
            }

            var ipAddress = GetIpAddress();

            var (success, message) = await _authService.RegisterAsync(
                request.Username,
                request.Email,
                request.Password);

            if (!success)
            {
                await _auditoriaService.LogAsync(
                    null,
                    ipAddress,
                    "Registro Fallido",
                    "/api/auth/register",
                    "POST",
                    400,
                    message);

                return BadRequest(new { message });
            }

            await _auditoriaService.LogAsync(
                null,
                ipAddress,
                "Registro Exitoso",
                "/api/auth/register",
                "POST",
                201);

            return CreatedAtAction(nameof(Register), new { message });
        }

        /// <summary>
        /// Endpoint protegido para obtener información del usuario actual.
        /// </summary>
        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> GetCurrentUser()
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdClaim?.Value, out int userId))
            {
                return Unauthorized();
            }

            // Aquí obtendrías la información del usuario de la BD
            return Ok(new { message = "Usuario autenticado", userId });
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
