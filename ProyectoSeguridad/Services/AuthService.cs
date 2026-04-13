using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using ProyectoSeguridad.Models;
using ProyectoSeguridad.Data;
using BCrypt.Net;
using Microsoft.EntityFrameworkCore;

namespace ProyectoSeguridad.Services
{
    public interface IAuthService
    {
        Task<(bool success, string? token, string? message)> LoginAsync(string username, string password, string ipAddress);
        Task<(bool success, string? message)> RegisterAsync(string username, string email, string password);
        Task<bool> VerifyPasswordAsync(string password, string hash);
        string HashPassword(string password);
        Task<bool> ValidateTokenAsync(string token);
        Task UpdateLastActivityAsync(int userId, string ipAddress);
        Task<bool> CheckInactivityAsync(int userId, int minutosInactividad = 5);
        Task<bool> IsAccountLockedAsync(int userId);
    }

    public class AuthService : IAuthService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthService> _logger;
        private const int BCryptCostFactor = 12;
        private const int RateLimitMaxIntents = 5;
        private const int RateLimitDurationMinutes = 5;

        public AuthService(
            ApplicationDbContext context,
            IConfiguration configuration,
            ILogger<AuthService> logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
        }

        public string HashPassword(string password)
        {
            var salt = BCrypt.Net.BCrypt.GenerateSalt(BCryptCostFactor);
            return BCrypt.Net.BCrypt.HashPassword(password, salt);
        }

        public async Task<bool> VerifyPasswordAsync(string password, string hash)
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }

        public async Task<(bool success, string? token, string? message)> LoginAsync(
            string username, string password, string ipAddress)
        {
            try
            {
                // Buscar usuario
                var usuario = await _context.Usuarios
                    .Include(u => u.Rol)
                    .FirstOrDefaultAsync(u => u.Username == username);

                if (usuario == null)
                {
                    _logger.LogWarning($"Intento de login con usuario no existente: {username} desde IP {ipAddress}");
                    return (false, null, "Usuario o contraseña inválidos");
                }

                // Verificar si la cuenta está bloqueada por rate limiting
                if (await IsAccountLockedAsync(usuario.Id))
                {
                    _logger.LogWarning($"Intento de login bloqueado por rate limiting: {username} desde IP {ipAddress}");
                    return (false, null, "Cuenta bloqueada temporalmente. Intente más tarde.");
                }

                // Verificar contraseña
                if (!await VerifyPasswordAsync(password, usuario.PasswordHash))
                {
                    // Incrementar intentos fallidos
                    usuario.IntentosFallidos++;
                    if (usuario.IntentosFallidos >= RateLimitMaxIntents)
                    {
                        usuario.FechaBloqueoCuenta = DateTime.UtcNow;
                        _logger.LogWarning($"Cuenta bloqueada por rate limiting: {username}");
                    }
                    await _context.SaveChangesAsync();

                    _logger.LogWarning($"Intento de login fallido para: {username} desde IP {ipAddress}");
                    return (false, null, "Usuario o contraseña inválidos");
                }

                // Restablecer intentos fallidos
                usuario.IntentosFallidos = 0;
                usuario.FechaBloqueoCuenta = null;
                usuario.UltimoLogin = DateTime.UtcNow;
                usuario.UltimaIP = ipAddress;
                usuario.UltimaActividad = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                // Generar JWT
                var token = GenerateJwtToken(usuario);

                _logger.LogInformation($"Login exitoso para: {username} desde IP {ipAddress}");

                return (true, token, "Login exitoso");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error durante login para: {username}");
                return (false, null, "Error en el servidor");
            }
        }

        public async Task<(bool success, string? message)> RegisterAsync(
            string username, string email, string password)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                {
                    return (false, "Usuario y contraseña requeridos");
                }

                if (await _context.Usuarios.AnyAsync(u => u.Username == username))
                {
                    return (false, "El usuario ya existe");
                }

                if (await _context.Usuarios.AnyAsync(u => u.Email == email))
                {
                    return (false, "El email ya está registrado");
                }

                var usuarioRol = await _context.Roles.FirstOrDefaultAsync(r => r.Nombre == "Registrador");
                if (usuarioRol == null)
                {
                    return (false, "Error al obtener rol de usuario");
                }

                var nuevoUsuario = new Usuario
                {
                    Username = username,
                    Email = email,
                    PasswordHash = HashPassword(password),
                    RolId = usuarioRol.Id,
                    Activo = true,
                    FechaCreacion = DateTime.UtcNow
                };

                _context.Usuarios.Add(nuevoUsuario);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Nuevo usuario registrado: {username}");

                return (true, "Usuario registrado exitosamente");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error durante registro de: {username}");
                return (false, "Error en el servidor");
            }
        }

        public async Task<bool> ValidateTokenAsync(string token)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.ASCII.GetBytes(_configuration["Jwt:SecretKey"] ?? "default-secret-key-must-be-changed");

                tokenHandler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = _configuration["Jwt:Issuer"],
                    ValidateAudience = true,
                    ValidAudience = _configuration["Jwt:Audience"],
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                }, out SecurityToken validatedToken);

                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task UpdateLastActivityAsync(int userId, string ipAddress)
        {
            var usuario = await _context.Usuarios.FindAsync(userId);
            if (usuario != null)
            {
                usuario.UltimaActividad = DateTime.UtcNow;
                usuario.UltimaIP = ipAddress;
                await _context.SaveChangesAsync();
            }
        }

        public async Task<bool> CheckInactivityAsync(int userId, int minutosInactividad = 5)
        {
            var usuario = await _context.Usuarios.FindAsync(userId);
            if (usuario?.UltimaActividad == null)
                return false;

            var tiempoInactivo = DateTime.UtcNow - usuario.UltimaActividad.Value;
            return tiempoInactivo.TotalMinutes > minutosInactividad;
        }

        public async Task<bool> IsAccountLockedAsync(int userId)
        {
            var usuario = await _context.Usuarios.FindAsync(userId);
            if (usuario?.FechaBloqueoCuenta == null)
                return false;

            var tiempoTranscurrido = DateTime.UtcNow - usuario.FechaBloqueoCuenta.Value;
            if (tiempoTranscurrido.TotalMinutes > RateLimitDurationMinutes)
            {
                usuario.FechaBloqueoCuenta = null;
                usuario.IntentosFallidos = 0;
                await _context.SaveChangesAsync();
                return false;
            }

            return true;
        }

        private string GenerateJwtToken(Usuario usuario)
        {
            var key = Encoding.ASCII.GetBytes(
                _configuration["Jwt:SecretKey"] ?? "default-secret-key-must-be-changed-in-production");

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
                new Claim(ClaimTypes.Name, usuario.Username),
                new Claim(ClaimTypes.Email, usuario.Email),
                new Claim(ClaimTypes.Role, usuario.Rol?.Nombre ?? "User")
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddHours(1),
                Issuer = _configuration["Jwt:Issuer"],
                Audience = _configuration["Jwt:Audience"],
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature)
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}
