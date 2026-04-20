using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ProyectoSeguridad.Data;
using ProyectoSeguridad.Services;
using ProyectoSeguridad.Middleware;
using BCrypt.Net;
using AppModels = ProyectoSeguridad.Models;

var builder = WebApplication.CreateBuilder(args);

// ==================== CONFIGURACIÓN DE CONEXIÓN A BD ====================
var databaseProvider = builder.Configuration["Database:Provider"]?.Trim().ToLowerInvariant();
if (string.IsNullOrWhiteSpace(databaseProvider))
{
    databaseProvider = builder.Environment.IsDevelopment() ? "sqlite" : "postgres";
}

var useSqlite = databaseProvider == "sqlite";

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    if (useSqlite)
    {
        var sqliteConnectionString = builder.Configuration.GetConnectionString("SqliteConnection")
            ?? "Data Source=ProyectoSeguridad.dev.db";
        options.UseSqlite(sqliteConnectionString);
        return;
    }

    var postgresConnectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    options.UseNpgsql(postgresConnectionString);
});

// ==================== AUTENTICACIÓN Y AUTORIZACIÓN ====================
var jwtKey = builder.Configuration["Jwt:SecretKey"];
if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32)
{
    throw new InvalidOperationException("Jwt:SecretKey debe estar configurada y tener al menos 32 caracteres.");
}

var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "ProyectoSeguridad";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "ProyectoSeguridad";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(jwtKey)),
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };

    // Leer token desde Cookie
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            if (context.Request.Cookies.TryGetValue("Authorization", out var token))
            {
                context.Token = token;
            }
            return Task.CompletedTask;
        }
    };
});

// ==================== AUTORIZACIÓN CON ROLES ====================
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("SuperAdminOnly", policy => policy.RequireRole("SuperAdmin"));
    options.AddPolicy("AuditorAccess", policy => policy.RequireRole("SuperAdmin", "Auditor"));
    options.AddPolicy("RegistradorAccess", policy => policy.RequireRole("SuperAdmin", "Registrador"));
});

// ==================== SERVICIOS DE APLICACIÓN ====================
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAuditoriaService, AuditoriaService>();

// ==================== CONTROLADORES Y SWAGGER ====================
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ==================== CORS (si es necesario) ====================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowLocalhost", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "https://localhost:3000")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// ==================== LOGGING ====================
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

var app = builder.Build();

// ==================== MIGRACIÓN Y SEEDING DE BD ====================
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();

    try
    {
        if (useSqlite)
        {
            // SQLite local: crea schema si no existe sin depender de migraciones de PostgreSQL.
            await context.Database.EnsureCreatedAsync();
            logger.LogInformation("Esquema SQLite verificado/creado exitosamente");
        }
        else
        {
            // PostgreSQL: aplicar migraciones versionadas.
            await context.Database.MigrateAsync();
            logger.LogInformation("Migraciones de BD aplicadas exitosamente");
        }

        // Seeding de datos iniciales
        var admin = await context.Usuarios.FirstOrDefaultAsync(u => u.Username == "admin");
        if (admin == null)
        {
            admin = new AppModels.Usuario
            {
                Username = "admin",
                Email = "admin@seguridad.local",
                PasswordHash = authService.HashPassword("Admin123!"),
                RolId = 1,
                Activo = true,
                FechaCreacion = DateTime.UtcNow
            };
            context.Usuarios.Add(admin);
        }
        else
        {
            // Siempre corregir el hash si era el dummy
            if (admin.PasswordHash.Contains("dummy"))
            {
                admin.PasswordHash = authService.HashPassword("Admin123!");
                await context.SaveChangesAsync();
                logger.LogInformation("Hash del admin corregido exitosamente");
            }
        }

        // Crear usuario Auditor de demostración
        if (!context.Usuarios.Any(u => u.Username == "auditor"))
        {
            var auditor = new AppModels.Usuario
            {
                Username = "auditor",
                Email = "auditor@seguridad.local",
                PasswordHash = authService.HashPassword("Auditor123!"),
                RolId = 2, // Auditor
                Activo = true,
                FechaCreacion = DateTime.UtcNow
            };

            context.Usuarios.Add(auditor);
            await context.SaveChangesAsync();
            logger.LogInformation("Usuario auditor creado: auditor");
        }

        // Crear usuario Registrador de demostración
        if (!context.Usuarios.Any(u => u.Username == "registrador"))
        {
            var registrador = new AppModels.Usuario
            {
                Username = "registrador",
                Email = "registrador@seguridad.local",
                PasswordHash = authService.HashPassword("Registrador123!"),
                RolId = 3, // Registrador
                Activo = true,
                FechaCreacion = DateTime.UtcNow
            };

            context.Usuarios.Add(registrador);
            await context.SaveChangesAsync();
            logger.LogInformation("Usuario registrador creado: registrador");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error durante la migración y seeding de BD");
        throw;
    }
}

// ==================== PIPELINE HTTP ====================
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "API Segura v1");
    });
}

// ==================== ARCHIVOS ESTÁTICOS (wwwroot) ====================
// Middlewares de seguridad
app.UseSecurityHeaders();

app.UseDefaultFiles();   // sirve index.html cuando se accede a /
app.UseStaticFiles();    // sirve css/, js/, etc.


//app.UseHttpsRedirection(); // Deshabilitado para desarrollo local sin HTTPS, habilitar en producción
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuditoria();
app.UseInactividad();

app.UseCors("AllowLocalhost");
app.UseAuthentication();
app.UseAuthorization();



app.MapControllers();

// Endpoint de health check
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
    .WithName("Health")
    .WithOpenApi();

app.Run();