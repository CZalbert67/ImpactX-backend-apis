using Microsoft.EntityFrameworkCore;
using Monitor = ImpactX.Core.Domain.Monitor;
using ImpactX.Core.Domain;

namespace ImpactX.Infrastructure.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<Plan> Planes => Set<Plan>();
    public DbSet<Suscripcion> Suscripciones => Set<Suscripcion>();
    public DbSet<Pago> Pagos => Set<Pago>();
    public DbSet<Wearable> Wearables => Set<Wearable>();
    public DbSet<ContactoEmergencia> ContactosEmergencia => Set<ContactoEmergencia>();
    public DbSet<Monitor> Monitores => Set<Monitor>();
    public DbSet<Ruta> Rutas => Set<Ruta>();
    public DbSet<Viaje> Viajes => Set<Viaje>();
    public DbSet<ViajeTelemetry> ViajeTelemetries => Set<ViajeTelemetry>();
    public DbSet<Alerta> Alertas => Set<Alerta>();
    public DbSet<Incidente> Incidentes => Set<Incidente>();
    public DbSet<Notificacion> Notificaciones => Set<Notificacion>();

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

            entity.OwnsOne(u => u.PerfilConduccion, p =>
            {
                p.Property(pf => pf.TipoVehiculo).HasMaxLength(50);
                p.Property(pf => pf.Marca).HasMaxLength(100);
                p.Property(pf => pf.Modelo).HasMaxLength(100);
                p.Property(pf => pf.Color).HasMaxLength(50);
                p.Property(pf => pf.Placa).HasMaxLength(20);
                p.Property(pf => pf.Uso).HasMaxLength(100);
                p.Property(pf => pf.VelocidadPromedioLabel).HasMaxLength(50);
            });

            entity.OwnsOne(u => u.FichaMedica, f =>
            {
                f.Property(fm => fm.TipoSangre).HasMaxLength(10);
            });

            entity.OwnsOne(u => u.Preferencias, p =>
            {
                p.Property(pr => pr.Idioma).HasMaxLength(10);
                p.Property(pr => pr.UnidadVelocidad).HasMaxLength(20);
            });

            entity.OwnsOne(u => u.Permisos, p =>
            {
                p.OwnsOne(pe => pe.Mobile);
                p.OwnsOne(pe => pe.Web);
            });

            entity.OwnsOne(u => u.Settings, s =>
            {
                s.Property(st => st.TwoFactorSecret).HasMaxLength(500);
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

        modelBuilder.Entity<ContactoEmergencia>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Nombre).HasMaxLength(200).IsRequired();
            entity.Property(c => c.Telefono).HasMaxLength(20).IsRequired();
            entity.Property(c => c.Parentesco).HasMaxLength(100);
            entity.Property(c => c.Username).HasMaxLength(100);
            entity.Property(c => c.AppUserId).HasMaxLength(100);
            entity.Property(c => c.Channel).HasMaxLength(50);
            entity.Property(c => c.Priority).HasMaxLength(50);
            entity.HasIndex(c => c.UsuarioId);
        });

        modelBuilder.Entity<Ruta>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Nombre).HasMaxLength(200).IsRequired();
            entity.Property(r => r.Origen).HasMaxLength(500).IsRequired();
            entity.Property(r => r.Destino).HasMaxLength(500).IsRequired();
            entity.HasIndex(r => r.UsuarioId);
        });

        modelBuilder.Entity<Viaje>(entity =>
        {
            entity.HasKey(v => v.Id);
            entity.Property(v => v.DispositivoId).HasMaxLength(200);
            entity.Property(v => v.Estado).HasMaxLength(50);
            entity.Property(v => v.Proposito).HasMaxLength(200);
            entity.Property(v => v.RutaOrigen).HasMaxLength(500);
            entity.Property(v => v.RutaDestino).HasMaxLength(500);
            entity.Property(v => v.RiesgoMaximo).HasMaxLength(50);
            entity.HasIndex(v => v.UsuarioId);
        });

        modelBuilder.Entity<ViajeTelemetry>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.HasIndex(t => t.ViajeId);
        });

        modelBuilder.Entity<Monitor>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.Property(m => m.CorreoInvitado).HasMaxLength(256);
            entity.Property(m => m.Username).HasMaxLength(100);
            entity.Property(m => m.AppUserId).HasMaxLength(100);
            entity.Property(m => m.ProfileId).HasMaxLength(100);
            entity.Property(m => m.Estado).HasMaxLength(50);
            entity.Property(m => m.TokenInvitacion).HasMaxLength(200);
            entity.HasIndex(m => m.UsuarioId);
            entity.HasIndex(m => m.TokenInvitacion);
        });

        modelBuilder.Entity<Plan>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Nombre).HasMaxLength(50).IsRequired();
            entity.HasIndex(p => p.Nombre);
        });

        modelBuilder.Entity<Suscripcion>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Estado).HasMaxLength(20).IsRequired();
            entity.Property(s => s.MotivoCancelacion).HasMaxLength(500);
            entity.HasIndex(s => s.UsuarioId);
            entity.HasIndex(s => s.PlanId);
        });

        modelBuilder.Entity<Pago>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Moneda).HasMaxLength(10);
            entity.Property(p => p.MetodoPago).HasMaxLength(50);
            entity.Property(p => p.Estado).HasMaxLength(20);
            entity.Property(p => p.Referencia).HasMaxLength(200);
            entity.Property(p => p.ComprobanteUrl).HasMaxLength(500);
            entity.HasIndex(p => p.UsuarioId);
            entity.HasIndex(p => p.SuscripcionId);
        });

        modelBuilder.Entity<Wearable>(entity =>
        {
            entity.HasKey(w => w.Id);
            entity.Property(w => w.DispositivoId).HasMaxLength(200);
            entity.Property(w => w.Nombre).HasMaxLength(200);
            entity.Property(w => w.Modelo).HasMaxLength(200);
            entity.Property(w => w.AppVersion).HasMaxLength(50);
            entity.Property(w => w.PairingToken).HasMaxLength(200);
            entity.Property(w => w.Estado).HasMaxLength(50);
            entity.HasIndex(w => w.UsuarioId);
            entity.HasIndex(w => w.PairingToken);
        });

        modelBuilder.Entity<Alerta>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Tipo).HasMaxLength(50);
            entity.Property(a => a.Severidad).HasMaxLength(20);
            entity.Property(a => a.Estado).HasMaxLength(20);
            entity.Property(a => a.Lugar).HasMaxLength(500);
            entity.Property(a => a.GForce).HasMaxLength(20);
            entity.Property(a => a.Decibeles).HasMaxLength(20);
            entity.Property(a => a.FrecuenciaCardiaca).HasMaxLength(20);
            entity.Property(a => a.Activacion).HasMaxLength(50);
            entity.Property(a => a.Modo).HasMaxLength(20);
            entity.Property(a => a.Canal).HasMaxLength(50);
            entity.Property(a => a.ViajeId).HasMaxLength(100);
            entity.Property(a => a.MetodoCierre).HasMaxLength(50);
            entity.HasIndex(a => a.UsuarioId);
        });

        modelBuilder.Entity<Incidente>(entity =>
        {
            entity.HasKey(i => i.Id);
            entity.Property(i => i.Severidad).HasMaxLength(20);
            entity.Property(i => i.Lugar).HasMaxLength(500);
            entity.Property(i => i.GForce).HasMaxLength(20);
            entity.Property(i => i.Decibeles).HasMaxLength(20);
            entity.Property(i => i.FrecuenciaCardiaca).HasMaxLength(20);
            entity.Property(i => i.Canal).HasMaxLength(50);
            entity.Property(i => i.MetodoCierre).HasMaxLength(50);
            entity.HasIndex(i => i.UsuarioId);
            entity.HasIndex(i => i.AlertaId);
        });

        modelBuilder.Entity<Notificacion>(entity =>
        {
            entity.HasKey(n => n.Id);
            entity.Property(n => n.Titulo).HasMaxLength(200).IsRequired();
            entity.Property(n => n.Mensaje).HasMaxLength(1000).IsRequired();
            entity.Property(n => n.Tipo).HasMaxLength(50);
            entity.Property(n => n.ReferenciaId).HasMaxLength(100);
            entity.Property(n => n.ReferenciaTipo).HasMaxLength(50);
            entity.HasIndex(n => n.UsuarioId);
        });
    }
}
