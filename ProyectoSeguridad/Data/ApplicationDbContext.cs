using Microsoft.EntityFrameworkCore;
using ProyectoSeguridad.Models;

namespace ProyectoSeguridad.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<Rol> Roles { get; set; }
        public DbSet<Producto> Productos { get; set; }
        public DbSet<AuditoriaLog> AuditoriaLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configuración de claves foráneas
            modelBuilder.Entity<Usuario>()
                .HasOne(u => u.Rol)
                .WithMany(r => r.Usuarios)
                .HasForeignKey(u => u.RolId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Producto>()
                .HasOne(p => p.Creador)
                .WithMany()
                .HasForeignKey(p => p.CreadorId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<AuditoriaLog>()
                .HasOne(a => a.Usuario)
                .WithMany()
                .HasForeignKey(a => a.UsuarioId)
                .OnDelete(DeleteBehavior.SetNull);

            // Índices para optimización y seguridad
            modelBuilder.Entity<Usuario>()
                .HasIndex(u => u.Username)
                .IsUnique();

            modelBuilder.Entity<Usuario>()
                .HasIndex(u => u.Email)
                .IsUnique();

            modelBuilder.Entity<Producto>()
                .HasIndex(p => p.Codigo)
                .IsUnique();

            modelBuilder.Entity<AuditoriaLog>()
                .HasIndex(a => a.Timestamp);

            modelBuilder.Entity<AuditoriaLog>()
                .HasIndex(a => a.IPOrigen);

            // Data seeding de Roles
            modelBuilder.Entity<Rol>().HasData(
                new Rol { Id = 1, Nombre = "SuperAdmin", Descripcion = "Acceso total al sistema" },
                new Rol { Id = 2, Nombre = "Auditor", Descripcion = "Visualiza logs y auditoría" },
                new Rol { Id = 3, Nombre = "Registrador", Descripcion = "Gestiona productos" }
            );

            // Data seeding de Usuario SuperAdmin (password por defecto: Admin123!)
            // El hash será generado en el Application Startup
            modelBuilder.Entity<Usuario>().HasData(
                new Usuario
                {
                    Id = 1,
                    Username = "admin",
                    Email = "admin@seguridad.local",
                    PasswordHash = "$2b$12$dummyhashforseeding", // Será actualizado en el startup
                    RolId = 1,
                    Activo = true,
                    FechaCreacion = new DateTime(2026, 4, 13, 0, 0, 0, DateTimeKind.Utc)
                }
            );
        }
    }
}
