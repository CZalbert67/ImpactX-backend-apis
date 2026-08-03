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
    public DbSet<Dispositivo> Dispositivos => Set<Dispositivo>();
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
    public DbSet<AppInvite> AppInvites => Set<AppInvite>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<FamilySubscription> FamilySubscriptions => Set<FamilySubscription>();
    public DbSet<MonitoringRelationship> MonitoringRelationships => Set<MonitoringRelationship>();
    public DbSet<QuickMessageTemplate> QuickMessageTemplates => Set<QuickMessageTemplate>();
    public DbSet<QuickMessage> QuickMessages => Set<QuickMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.HasIndex(u => u.Correo).IsUnique();
            entity.Property(u => u.Nombre).HasMaxLength(200).IsRequired();
            entity.Property(u => u.Correo).HasMaxLength(256).IsRequired();
            entity.Property(u => u.CorreoNormalizado).HasMaxLength(256);
            entity.Property(u => u.PublicProfileId).HasMaxLength(64);
            entity.Property(u => u.Telefono).HasMaxLength(20);
            entity.Property(u => u.Ciudad).HasMaxLength(200);
            entity.Property(u => u.PasswordHash).HasMaxLength(500).IsRequired();
            entity.Property(u => u.PlanActivo).HasMaxLength(50);
            entity.Property(u => u.FcmToken).HasMaxLength(1000);
            entity.Property(u => u.DeletionReason).HasMaxLength(500);
            entity.PrimitiveCollection(u => u.UsernamesAnteriores);

            entity.OwnsOne(u => u.Onboarding, onboarding =>
            {
                onboarding.Property(value => value.TermsVersion).HasMaxLength(40);
                onboarding.Property(value => value.PrivacyNoticeVersion).HasMaxLength(40);
            });

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

            entity.Property(u => u.MobileSyncLastAcknowledgedCursor).HasMaxLength(128);
            entity.Property(u => u.MobileSyncClientInstanceId).HasMaxLength(100);
            entity.OwnsMany(u => u.MobileSyncReceipts, receipt =>
            {
                receipt.WithOwner().HasForeignKey("UsuarioId");
                receipt.HasKey("UsuarioId", nameof(MobileSyncOperationReceipt.OperationId));
                receipt.Property(value => value.OperationId).ValueGeneratedNever();
                receipt.Property(value => value.Type).HasMaxLength(80).IsRequired();
                receipt.Property(value => value.Result).HasMaxLength(30).IsRequired();
                receipt.Property(value => value.Message).HasMaxLength(500);
            });
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.HasIndex(r => r.Token).IsUnique();
            entity.Property(r => r.Token).HasMaxLength(500).IsRequired();
            entity.Property(r => r.DeviceInfo).HasMaxLength(500);
            entity.Property(r => r.Client).HasMaxLength(20).IsRequired();
        });

        modelBuilder.Entity<PasswordResetToken>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.HasIndex(p => p.TokenHash).IsUnique();
            entity.Property(p => p.TokenHash).HasMaxLength(64).IsRequired();
        });

        modelBuilder.Entity<Dispositivo>(entity =>
        {
            entity.HasKey(d => d.Id);
            entity.Property(d => d.DeviceId).HasMaxLength(200).IsRequired();
            entity.Property(d => d.Platform).HasMaxLength(20).IsRequired();
            entity.Property(d => d.TokenFcm).HasMaxLength(1000).IsRequired();
            entity.Property(d => d.Nombre).HasMaxLength(200);
            entity.HasIndex(d => d.UsuarioId);
            entity.HasIndex(d => new { d.UsuarioId, d.DeviceId }).IsUnique();
        });

        modelBuilder.Entity<ContactoEmergencia>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.PublicContactId).HasMaxLength(40);
            entity.Property(c => c.TargetEmailNormalized).HasMaxLength(256);
            entity.Property(c => c.TargetPublicProfileId).HasMaxLength(64);
            entity.Property(c => c.TargetUsername).HasMaxLength(50);
            entity.Property(c => c.InvitationCodeHash).HasMaxLength(64);
            entity.Property(c => c.Nombre).HasMaxLength(200).IsRequired();
            entity.Property(c => c.Telefono).HasMaxLength(20).IsRequired();
            entity.Property(c => c.Parentesco).HasMaxLength(100);
            entity.Property(c => c.Username).HasMaxLength(100);
            entity.Property(c => c.AppUserId).HasMaxLength(100);
            entity.Property(c => c.Channel).HasMaxLength(50);
            entity.Property(c => c.Priority).HasMaxLength(50);
            entity.Property(c => c.Email).HasMaxLength(256);
            entity.Property(c => c.Status).HasConversion<string>();
            entity.Property(c => c.Notes).HasMaxLength(1000);
            entity.Property(c => c.PreviousStatus).HasMaxLength(50);
            entity.HasIndex(c => c.UsuarioId);
            entity.HasIndex(c => c.PublicContactId).IsUnique();
            entity.HasIndex(c => c.ContactUserId);
            entity.HasIndex(c => c.InvitationCodeHash);
        });

        modelBuilder.Entity<Ruta>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Nombre).HasMaxLength(200).IsRequired();
            entity.Property(r => r.Etiqueta).HasMaxLength(200);
            entity.Property(r => r.Nota).HasMaxLength(1000);
            entity.Property(r => r.Origen).HasMaxLength(500).IsRequired();
            entity.Property(r => r.Destino).HasMaxLength(500).IsRequired();
            entity.HasIndex(r => r.UsuarioId);
        });

        modelBuilder.Entity<Viaje>(entity =>
        {
            entity.HasKey(v => v.Id);
            entity.Property(v => v.DispositivoId).HasMaxLength(200);
            entity.Property(v => v.VehiclePublicId).HasMaxLength(30);
            entity.Property(v => v.ControlClient).HasMaxLength(20).IsRequired();
            entity.Property(v => v.FallbackReason).HasMaxLength(500);
            entity.Property(v => v.Estado).HasMaxLength(50);
            entity.Property(v => v.Proposito).HasMaxLength(200);
            entity.Property(v => v.RutaOrigen).HasMaxLength(500);
            entity.Property(v => v.RutaDestino).HasMaxLength(500);
            entity.Property(v => v.RiesgoMaximo).HasMaxLength(50);
            entity.Property(v => v.NivelRiesgo).HasMaxLength(50);
            entity.Property(v => v.Canal).HasMaxLength(50);
            entity.HasIndex(v => v.UsuarioId);
        });

        modelBuilder.Entity<ViajeTelemetry>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.WearableDeviceId).HasMaxLength(200);
            entity.Property(t => t.WearableModel).HasMaxLength(200);
            entity.Property(t => t.WearableAppVersion).HasMaxLength(80);
            entity.Property(t => t.WearableOsVersion).HasMaxLength(80);
            entity.Property(t => t.WearableFirmwareVersion).HasMaxLength(80);
            entity.Property(t => t.VehiclePublicId).HasMaxLength(30);
            entity.Property(t => t.CalidadSensor).HasMaxLength(20);
            entity.Property(t => t.SensorFlagsCsv).HasMaxLength(1200);
            entity.Property(t => t.DetectionLabel).HasMaxLength(100);
            entity.Property(t => t.SeverityLabel).HasMaxLength(50);
            entity.Property(t => t.RuleVersion).HasMaxLength(80);
            entity.Property(t => t.ModelVersion).HasMaxLength(80);
            entity.HasIndex(t => t.ViajeId);
        });

        modelBuilder.Entity<Monitor>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Nombre).HasMaxLength(200);
            entity.Property(m => m.Telefono).HasMaxLength(20);
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
            entity.Property(p => p.Descripcion).HasMaxLength(500);
            entity.HasIndex(p => p.Nombre);
        });

        modelBuilder.Entity<AppInvite>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Token).HasMaxLength(100).IsRequired();
            entity.Property(a => a.SuggestedUsername).HasMaxLength(100);
            entity.Property(a => a.Relation).HasMaxLength(100);
            entity.Property(a => a.Priority).HasMaxLength(50);
            entity.Property(a => a.Status).HasMaxLength(50);
            entity.Property(a => a.PersonalMessage).HasMaxLength(1000);
            entity.Property(a => a.InviteUrl).HasMaxLength(500);
            entity.HasIndex(a => a.UsuarioId);
            entity.HasIndex(a => a.Token).IsUnique();
        });

        modelBuilder.Entity<Suscripcion>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Estado).HasMaxLength(20).IsRequired();
            entity.Property(s => s.BillingCycle).HasMaxLength(20).IsRequired();
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
            entity.Property(w => w.Fabricante).HasMaxLength(100);
            entity.Property(w => w.Plataforma).HasMaxLength(100);
            entity.Property(w => w.AppVersion).HasMaxLength(80);
            entity.Property(w => w.VersionSistemaOperativo).HasMaxLength(80);
            entity.Property(w => w.VersionFirmware).HasMaxLength(80);
            entity.Property(w => w.CalidadSensores).HasMaxLength(20);
            entity.Property(w => w.PairingToken).HasMaxLength(200);
            entity.Property(w => w.CodigoEmparejamiento).HasMaxLength(50);
            entity.Property(w => w.TrustToken).HasMaxLength(100);
            entity.Property(w => w.Estado).HasMaxLength(50);
            entity.PrimitiveCollection(w => w.PermisosOtorgados);
            entity.PrimitiveCollection(w => w.CapacidadesSensores);
            entity.PrimitiveCollection(w => w.SensoresDisponibles);
            entity.PrimitiveCollection(w => w.SensoresNoDisponibles);
            entity.HasIndex(w => w.UsuarioId);
            entity.HasIndex(w => w.PairingToken);
            entity.OwnsOne(w => w.SensoresActivos);
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
            entity.Property(a => a.DetectionLabel).HasMaxLength(100);
            entity.Property(a => a.RuleVersion).HasMaxLength(80);
            entity.Property(a => a.MetodoCierre).HasMaxLength(50);
            entity.HasIndex(a => a.UsuarioId);
            entity.HasIndex(a => new { a.UsuarioId, a.SourceTelemetryEventId });
        });

        modelBuilder.Entity<Incidente>(entity =>
        {
            entity.HasKey(i => i.Id);
            entity.Property(i => i.Tipo).HasMaxLength(50);
            entity.Property(i => i.Severidad).HasMaxLength(20);
            entity.Property(i => i.Estado).HasMaxLength(20);
            entity.Property(i => i.Lugar).HasMaxLength(500);
            entity.Property(i => i.GForce).HasMaxLength(20);
            entity.Property(i => i.Decibeles).HasMaxLength(20);
            entity.Property(i => i.FrecuenciaCardiaca).HasMaxLength(20);
            entity.Property(i => i.Canal).HasMaxLength(50);
            entity.Property(i => i.Activacion).HasMaxLength(50);
            entity.Property(i => i.TiempoRespuesta).HasMaxLength(50);
            entity.Property(i => i.ViajeId).HasMaxLength(100);
            entity.Property(i => i.DetectionLabel).HasMaxLength(100);
            entity.Property(i => i.RuleVersion).HasMaxLength(80);
            entity.Property(i => i.MetodoCierre).HasMaxLength(50);
            entity.HasIndex(i => i.UsuarioId);
            entity.HasIndex(i => i.AlertaId);
            entity.HasIndex(i => new { i.UsuarioId, i.Estado });
        });

        modelBuilder.Entity<Notificacion>(entity =>
        {
            entity.HasKey(n => n.Id);
            entity.Property(n => n.Titulo).HasMaxLength(200).IsRequired();
            entity.Property(n => n.Mensaje).HasMaxLength(1000).IsRequired();
            entity.Property(n => n.Tipo).HasMaxLength(50);
            entity.Property(n => n.ReferenciaId).HasMaxLength(100);
            entity.Property(n => n.ReferenciaTipo).HasMaxLength(50);
            entity.Property(n => n.Ruta).HasMaxLength(200);
            entity.Property(n => n.Canal).HasMaxLength(20);
            entity.Property(n => n.EstadoEnvio).HasMaxLength(30);
            entity.Property(n => n.ClaveIdempotencia).HasMaxLength(300);
            entity.HasIndex(n => n.UsuarioId);
            entity.HasIndex(n => n.ClaveIdempotencia).IsUnique();
        });

        modelBuilder.Entity<Vehicle>(entity =>
        {
            entity.HasKey(v => v.Id);
            entity.HasIndex(v => v.PublicVehicleId).IsUnique();
            entity.HasIndex(v => v.OwnerUserId);
            entity.Property(v => v.PublicVehicleId).HasMaxLength(30).IsRequired();
            entity.Property(v => v.Marca).HasMaxLength(100).IsRequired();
            entity.Property(v => v.Modelo).HasMaxLength(100).IsRequired();
            entity.Property(v => v.TipoVehiculo)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();
            entity.Property(v => v.UsoPrincipalVehiculo)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();
        });


        modelBuilder.Entity<FamilySubscription>(entity =>
        {
            entity.HasKey(subscription => subscription.Id);
            entity.HasIndex(subscription => subscription.PublicSubscriptionId).IsUnique();
            entity.HasIndex(subscription => subscription.OwnerUserId);
            entity.Property(subscription => subscription.PublicSubscriptionId)
                .HasMaxLength(30)
                .IsRequired();
            entity.Property(subscription => subscription.PlanName)
                .HasMaxLength(30)
                .IsRequired();
            entity.Property(subscription => subscription.PendingPlanName)
                .HasMaxLength(30);
            entity.Property(subscription => subscription.Status)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();
            entity.Ignore(subscription => subscription.ETag);

            entity.OwnsMany(subscription => subscription.Memberships, membership =>
            {
                membership.WithOwner().HasForeignKey("FamilySubscriptionId");
                membership.HasKey(value => value.Id);
                membership.Property(value => value.Id).ValueGeneratedNever();
                membership.Property(value => value.PublicMembershipId)
                    .HasMaxLength(30)
                    .IsRequired();
                membership.Property(value => value.Role)
                    .HasConversion<string>()
                    .HasMaxLength(20)
                    .IsRequired();
                membership.Property(value => value.Status)
                    .HasConversion<string>()
                    .HasMaxLength(20)
                    .IsRequired();
                membership.Property(value => value.PublicProfileIdSnapshot).HasMaxLength(64);
                membership.Property(value => value.UsernameSnapshot).HasMaxLength(50);
                membership.Property(value => value.DisplayNameSnapshot).HasMaxLength(200);
                membership.HasIndex(value => value.PublicMembershipId).IsUnique();
                membership.HasIndex(value => value.UserId);
            });

            entity.OwnsMany(subscription => subscription.Invitations, invitation =>
            {
                invitation.WithOwner().HasForeignKey("FamilySubscriptionId");
                invitation.HasKey(value => value.Id);
                invitation.Property(value => value.Id).ValueGeneratedNever();
                invitation.Property(value => value.PublicInvitationId)
                    .HasMaxLength(30)
                    .IsRequired();
                invitation.Property(value => value.TargetEmailNormalized).HasMaxLength(256);
                invitation.Property(value => value.TargetPublicProfileId).HasMaxLength(64);
                invitation.Property(value => value.TargetUsername).HasMaxLength(50);
                invitation.Property(value => value.CodeHash).HasMaxLength(64);
                invitation.Property(value => value.Status)
                    .HasConversion<string>()
                    .HasMaxLength(20)
                    .IsRequired();
                invitation.HasIndex(value => value.PublicInvitationId).IsUnique();
                invitation.HasIndex(value => value.CodeHash);
            });

            entity.OwnsMany(subscription => subscription.Payments, payment =>
            {
                payment.WithOwner().HasForeignKey("FamilySubscriptionId");
                payment.HasKey(value => value.Id);
                payment.Property(value => value.Id).ValueGeneratedNever();
                payment.Property(value => value.PublicPaymentId)
                    .HasMaxLength(30)
                    .IsRequired();
                payment.Property(value => value.Result).HasMaxLength(30).IsRequired();
                payment.Property(value => value.PlanName).HasMaxLength(30).IsRequired();
                payment.Property(value => value.Currency).HasMaxLength(10).IsRequired();
                payment.HasIndex(value => value.PublicPaymentId).IsUnique();
            });
        });


        modelBuilder.Entity<MonitoringRelationship>(entity =>
        {
            entity.HasKey(relationship => relationship.Id);
            entity.HasIndex(relationship => relationship.PublicRelationshipId).IsUnique();
            entity.HasIndex(relationship => relationship.MonitorUserId);
            entity.HasIndex(relationship => relationship.MonitoredUserId);
            entity.HasIndex(relationship => relationship.InvitationCodeHash);
            entity.Property(relationship => relationship.PublicRelationshipId)
                .HasMaxLength(30)
                .IsRequired();
            entity.Property(relationship => relationship.TargetEmailNormalized).HasMaxLength(256);
            entity.Property(relationship => relationship.TargetPublicProfileId).HasMaxLength(64);
            entity.Property(relationship => relationship.TargetUsername).HasMaxLength(50);
            entity.Property(relationship => relationship.InvitationCodeHash).HasMaxLength(64);
            entity.Property(relationship => relationship.Status)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();
            entity.Property(relationship => relationship.Direction)
                .HasConversion<string>()
                .HasMaxLength(40)
                .IsRequired();
            entity.OwnsOne(relationship => relationship.Permissions);
            entity.Ignore(relationship => relationship.ETag);
        });


        modelBuilder.Entity<QuickMessageTemplate>(entity =>
        {
            entity.HasKey(template => template.Id);
            entity.HasIndex(template => template.PublicTemplateId).IsUnique();
            entity.HasIndex(template => new { template.OwnerUserId, template.Active });
            entity.Property(template => template.PublicTemplateId).HasMaxLength(30).IsRequired();
            entity.Property(template => template.OwnerKey).HasMaxLength(64).IsRequired();
            entity.Property(template => template.Text).HasMaxLength(160).IsRequired();
        });

        modelBuilder.Entity<QuickMessage>(entity =>
        {
            entity.HasKey(message => message.Id);
            entity.HasIndex(message => message.PublicMessageId).IsUnique();
            entity.HasIndex(message => new { message.RecipientUserId, message.IsRead });
            entity.HasIndex(message => message.SenderUserId);
            entity.Property(message => message.PublicMessageId).HasMaxLength(30).IsRequired();
            entity.Property(message => message.PublicRelationshipId).HasMaxLength(30).IsRequired();
            entity.Property(message => message.PublicTemplateId).HasMaxLength(40).IsRequired();
            entity.Property(message => message.TextSnapshot).HasMaxLength(160).IsRequired();
            entity.Property(message => message.RoutePublicId).HasMaxLength(64);
            entity.Property(message => message.IncidentPublicId).HasMaxLength(64);
        });
    }
}
