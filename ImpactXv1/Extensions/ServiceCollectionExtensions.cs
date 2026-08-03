using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using ImpactX.Configuration;
using ImpactX.Core.ImpactDetection;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Core.Interfaces.Services;
using ImpactX.Infrastructure.Data;
using ImpactX.Infrastructure.Data.Repositories.Cosmos;
using ImpactX.Infrastructure.Data.Repositories.EF;
using ImpactX.Infrastructure.Notifications;
using ImpactX.Infrastructure.Security;
using ImpactX.Services;

namespace ImpactX.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection RegisterApplicationServices(
        this IServiceCollection services, IConfiguration config)
    {
        var useCosmosDb = config.GetValue<bool>("UseCosmosDb");

        if (useCosmosDb)
        {
            services.AddOptions<CosmosDatabaseOptions>()
                .Bind(config.GetSection(CosmosDatabaseOptions.SectionName))
                .Validate(options => CosmosDatabaseOptions.Validate(options) is null)
                .ValidateOnStart();
            services.AddSingleton<CosmosDbContext>();
            services.Configure<DatabaseInitializationOptions>(config.GetSection("DatabaseInitialization"));
            services.Configure<ReadinessOptions>(config.GetSection("Readiness"));
            services.AddSingleton<DatabaseInitializationState>();
            services.AddHostedService<CosmosInitializationService>();
            services.AddScoped<IUsuarioRepository, CosmosUsuarioRepository>();
            services.AddScoped<IRefreshTokenRepository, CosmosRefreshTokenRepository>();
            services.AddScoped<IPasswordResetTokenRepository, CosmosPasswordResetTokenRepository>();
            services.AddScoped<IDispositivoRepository, CosmosDispositivoRepository>();
            services.AddScoped<IPlanRepository, CosmosPlanRepository>();
            services.AddScoped<ISuscripcionRepository, CosmosSuscripcionRepository>();
            services.AddScoped<IPagoRepository, CosmosPagoRepository>();
            services.AddScoped<IWearableRepository, CosmosWearableRepository>();
            services.AddScoped<IContactoRepository, CosmosContactoRepository>();
            services.AddScoped<IMonitorRepository, CosmosMonitorRepository>();
            services.AddScoped<IRutaRepository, CosmosRutaRepository>();
            services.AddScoped<IViajeRepository, CosmosViajeRepository>();
            services.AddScoped<IAlertaRepository, CosmosAlertaRepository>();
            services.AddScoped<IIncidenteRepository, CosmosIncidenteRepository>();
            services.AddScoped<INotificacionRepository, CosmosNotificacionRepository>();
            services.AddScoped<IVehicleRepository, CosmosVehicleRepository>();
            services.AddScoped<IFamilySubscriptionRepository, CosmosFamilySubscriptionRepository>();
            services.AddScoped<IMonitoringRelationshipRepository, CosmosMonitoringRelationshipRepository>();
            services.AddScoped<IQuickMessageRepository, CosmosQuickMessageRepository>();
        }
        else
        {
            services.AddScoped<IUsuarioRepository, UsuarioRepository>();
            services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
            services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();
            services.AddScoped<IDispositivoRepository, DispositivoRepository>();
            services.AddScoped<IPlanRepository, PlanRepository>();
            services.AddScoped<ISuscripcionRepository, SuscripcionRepository>();
            services.AddScoped<IPagoRepository, PagoRepository>();
            services.AddScoped<IWearableRepository, WearableRepository>();
            services.AddScoped<IContactoRepository, ContactoRepository>();
            services.AddScoped<IMonitorRepository, MonitorRepository>();
            services.AddScoped<IRutaRepository, RutaRepository>();
            services.AddScoped<IViajeRepository, ViajeRepository>();
            services.AddScoped<IAlertaRepository, AlertaRepository>();
            services.AddScoped<IIncidenteRepository, IncidenteRepository>();
            services.AddScoped<INotificacionRepository, NotificacionRepository>();
            services.AddScoped<IVehicleRepository, VehicleRepository>();
            services.AddScoped<IFamilySubscriptionRepository, FamilySubscriptionRepository>();
            services.AddScoped<IMonitoringRelationshipRepository, MonitoringRelationshipRepository>();
            services.AddScoped<IQuickMessageRepository, QuickMessageRepository>();
        }

        services.AddScoped<IPushNotificationGateway, FirebasePushNotificationGateway>();
        services.AddScoped<IEncryptionService, EncryptionService>();
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IEmailService, StubEmailService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IDeviceService, DeviceService>();
        services.AddScoped<IPlanService, PlanService>();
        services.Configure<SubscriptionLifecycleOptions>(config.GetSection(SubscriptionLifecycleOptions.SectionName));
        services.AddHostedService<SubscriptionLifecycleWorker>();
        services.AddScoped<IWearableService, WearableService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IContactService, ContactService>();
        services.AddScoped<IEmergencyContactService, EmergencyContactService>();
        services.AddScoped<IMonitorService, MonitorService>();
        services.AddScoped<IRutaService, RutaService>();
        services.AddScoped<IViajeService, ViajeService>();
        services.AddScoped<IAlertService, AlertService>();
        services.AddScoped<IIncidentService, IncidentService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IAnalyticsService, AnalyticsService>();
        services.AddScoped<ISettingsService, SettingsService>();
        services.AddScoped<IVehicleQuotaResolver, VehicleQuotaResolver>();
        services.AddScoped<IVehicleService, VehicleService>();
        services.AddScoped<IFamilySubscriptionService, FamilySubscriptionService>();
        services.AddScoped<IMonitoringRelationshipService, MonitoringRelationshipService>();
        services.Configure<ImpactDetectionOptions>(config.GetSection(ImpactDetectionOptions.SectionName));
        services.AddSingleton<IImpactDetectionEngine, ImpactDetectionEngine>();
        services.AddScoped<IImpactAlertOrchestrator, ImpactAlertOrchestrator>();
        services.AddHostedService<ImpactAlertDispatchWorker>();
        services.AddScoped<IMobileSyncService, MobileSyncService>();
        services.AddScoped<IQuickMessageService, QuickMessageService>();
        services.AddScoped<IAccountService, AccountService>();

        return services;
    }

    public static IServiceCollection ConfigureJwtAuthentication(
        this IServiceCollection services, IConfiguration configuration)
    {
        var signingKey = JwtSecurityConfiguration.GetSigningKey(configuration);
        var issuer = configuration["Jwt:Issuer"] ?? "ImpactXApi";
        var audience = configuration["Jwt:Audience"] ?? "ImpactXClients";

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = issuer,
                ValidAudience = audience,
                IssuerSigningKey = signingKey,
                ClockSkew = TimeSpan.Zero
            };
        });

        return services;
    }
}
