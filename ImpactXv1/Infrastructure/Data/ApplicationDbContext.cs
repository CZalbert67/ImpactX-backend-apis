using Microsoft.EntityFrameworkCore;
using Prueba1.Core.Domain;

namespace Prueba1.Infrastructure.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.HasIndex(u => u.Correo).IsUnique();
            entity.Property(u => u.Nombre).HasMaxLength(200).IsRequired();
            entity.Property(u => u.Correo).HasMaxLength(256).IsRequired();
            entity.Property(u => u.Telefono).HasMaxLength(20);
            entity.Property(u => u.PasswordHash).HasMaxLength(500).IsRequired();
            entity.Property(u => u.PlanActivo).HasMaxLength(50);

            entity.OwnsOne(u => u.PerfilConduccion);
            entity.OwnsOne(u => u.FichaMedica);
            entity.OwnsOne(u => u.Preferencias);
            entity.OwnsOne(u => u.Settings);
            entity.OwnsOne(u => u.Permisos, p =>
            {
                p.OwnsOne(perm => perm.Mobile);
                p.OwnsOne(perm => perm.Web);
            });
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.HasIndex(r => r.Token).IsUnique();
            entity.Property(r => r.Token).HasMaxLength(500).IsRequired();
            entity.Property(r => r.DeviceInfo).HasMaxLength(500);
        });

        modelBuilder.Entity<PasswordResetToken>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.HasIndex(p => p.Token).IsUnique();
            entity.Property(p => p.Token).HasMaxLength(500).IsRequired();
        });
    }
}
