using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Prueba1.Core.Interfaces.Repositories;
using Prueba1.Core.Interfaces.Services;
using Prueba1.Infrastructure.Data;
using Prueba1.Infrastructure.Data.Repositories.Cosmos;
using Prueba1.Infrastructure.Data.Repositories.EF;
using Prueba1.Infrastructure.Security;
using Prueba1.Services;

namespace Prueba1.Extensions;

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
        }

        services.AddScoped<IEncryptionService, EncryptionService>();
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IEmailService, StubEmailService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IPlanService, PlanService>();
        services.AddScoped<IWearableService, WearableService>();
        services.AddScoped<IPermissionService, PermissionService>();

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
