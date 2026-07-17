using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Core.Interfaces.Services;
using ImpactX.Infrastructure.Data;
using ImpactX.Infrastructure.Data.Repositories.Cosmos;
using ImpactX.Infrastructure.Data.Repositories.EF;
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
            services.AddSingleton<CosmosDbContext>();
            services.AddScoped<IUsuarioRepository, CosmosUsuarioRepository>();
            services.AddScoped<IRefreshTokenRepository, CosmosRefreshTokenRepository>();
            services.AddScoped<IPasswordResetTokenRepository, CosmosPasswordResetTokenRepository>();
            services.AddScoped<IPlanRepository, CosmosPlanRepository>();
            services.AddScoped<ISuscripcionRepository, CosmosSuscripcionRepository>();
            services.AddScoped<IPagoRepository, CosmosPagoRepository>();
            services.AddScoped<IWearableRepository, CosmosWearableRepository>();
            services.AddScoped<IContactoRepository, CosmosContactoRepository>();
            services.AddScoped<IMonitorRepository, CosmosMonitorRepository>();
            services.AddScoped<IRutaRepository, CosmosRutaRepository>();
            services.AddScoped<IViajeRepository, CosmosViajeRepository>();
        }
        else
        {
            services.AddScoped<IUsuarioRepository, UsuarioRepository>();
            services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
            services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();
            services.AddScoped<IPlanRepository, PlanRepository>();
            services.AddScoped<ISuscripcionRepository, SuscripcionRepository>();
            services.AddScoped<IPagoRepository, PagoRepository>();
            services.AddScoped<IWearableRepository, WearableRepository>();
            services.AddScoped<IContactoRepository, ContactoRepository>();
            services.AddScoped<IMonitorRepository, MonitorRepository>();
            services.AddScoped<IRutaRepository, RutaRepository>();
            services.AddScoped<IViajeRepository, ViajeRepository>();
        }

        services.AddScoped<IEncryptionService, EncryptionService>();
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IEmailService, StubEmailService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IPlanService, PlanService>();
        services.AddScoped<IWearableService, WearableService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IContactService, ContactService>();
        services.AddScoped<IMonitorService, MonitorService>();
        services.AddScoped<IRutaService, RutaService>();
        services.AddScoped<IViajeService, ViajeService>();

        return services;
    }

    public static IServiceCollection ConfigureJwtAuthentication(
        this IServiceCollection services, IConfiguration configuration)
    {
        var jwtSecret = configuration["Jwt:Secret"]!;

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
                ValidIssuer = configuration["Jwt:Issuer"],
                ValidAudience = configuration["Jwt:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtSecret)),
                ClockSkew = TimeSpan.Zero
            };
        });

        return services;
    }
}
