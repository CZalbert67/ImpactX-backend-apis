using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using ImpactX.Controllers;
using ImpactX.Core.Domain;
using ImpactX.Core.QuickMessages;
using ImpactX.Core.Security;
using ImpactX.Infrastructure.Security;
using ImpactX.Models.DTOs;
using ImpactX.Models.DTOs.FamilySubscriptions;
using ImpactX.Models.DTOs.Monitoring;
using ImpactX.Models.DTOs.QuickMessages;
using ImpactX.Models.DTOs.Vehicles;
using Microsoft.Extensions.Configuration;

namespace ImpactX.Tests.Unit;

public class BackendSurfaceSecurityTests
{
    [Fact]
    [Trait("Category", "Security")]
    public void QuickMessageSendContract_DoesNotAcceptFreeText()
    {
        var properties = typeof(SendQuickMessageRequest)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        Assert.DoesNotContain("Text", properties);
        Assert.Contains("PublicTemplateId", properties);
        Assert.Contains("RecipientPublicProfileId", properties);
    }

    [Fact]
    public void SystemQuickMessages_ContainExactlyTheApprovedEightTemplates()
    {
        var texts = SystemQuickMessageTemplates.All.Select(value => value.Text).ToArray();

        Assert.Equal(8, texts.Length);
        Assert.Equal(new[]
        {
            "Estoy bien",
            "Necesito ayuda",
            "Llámame cuando puedas",
            "Revisa mi ubicación",
            "Voy en camino",
            "Tuve un incidente",
            "¿Estás bien?",
            "Confirma que recibiste la alerta"
        }, texts);
    }

    [Fact]
    [Trait("Category", "Security")]
    public void NewPublicDtos_DoNotExposeInternalGuidIdentifiers()
    {
        var dtoTypes = new[]
        {
            typeof(VehicleDto),
            typeof(FamilySubscriptionSummaryDto),
            typeof(FamilyMemberDto),
            typeof(FamilyInvitationDto),
            typeof(MonitoringRelationshipDto),
            typeof(EmergencyContactDto),
            typeof(QuickMessageTemplateDto),
            typeof(QuickMessageDto),
            typeof(QuickMessageRecipientDto)
        };

        foreach (var type in dtoTypes)
        {
            var properties = type.GetProperties().Select(property => property.Name).ToArray();
            Assert.DoesNotContain("Id", properties);
            Assert.DoesNotContain("OwnerUserId", properties);
            Assert.DoesNotContain("UserId", properties);
            Assert.DoesNotContain("MonitorUserId", properties);
            Assert.DoesNotContain("MonitoredUserId", properties);
        }
    }

    [Theory]
    [InlineData(typeof(TripsController), "Start")]
    [InlineData(typeof(TripsController), "Pause")]
    [InlineData(typeof(TripsController), "Resume")]
    [InlineData(typeof(TripsController), "Finish")]
    [InlineData(typeof(TripsController), "UpdateTelemetry")]
    [InlineData(typeof(TripsController), "IngestTelemetry")]
    [InlineData(typeof(AlertasController), "Detect")]
    [InlineData(typeof(AlertasController), "SendSos")]
    [InlineData(typeof(WearableController), "Pair")]
    [InlineData(typeof(WearableController), "PairConfirm")]
    [InlineData(typeof(WearableController), "Sync")]
    [InlineData(typeof(WearableController), "Calibrate")]
    [InlineData(typeof(WearableController), "Unlink")]
    [InlineData(typeof(WearableController), "UpdatePermissions")]
    [InlineData(typeof(WearableController), "UpdateBattery")]
    [InlineData(typeof(WearableController), "Heartbeat")]
    [InlineData(typeof(WearableController), "ReportSensorDiagnostics")]
    [Trait("Category", "Security")]
    public void SensitiveWrites_RequireExplicitClientCapability(Type controller, string methodName)
    {
        var method = controller.GetMethod(methodName)
            ?? throw new InvalidOperationException($"No se encontró {controller.Name}.{methodName}.");

        Assert.NotNull(method.GetCustomAttribute<RequireClientCapabilityAttribute>());
    }

    [Fact]
    [Trait("Category", "Security")]
    public void JwtToken_IncludesNormalizedClientClaim()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "test-secret-test-secret-test-secret-123456789",
                ["Jwt:Issuer"] = "ImpactX-Test",
                ["Jwt:Audience"] = "ImpactX-Test-Clients"
            })
            .Build();
        var service = new JwtTokenService(configuration);
        var token = service.GenerateAccessToken(new Usuario
        {
            Id = Guid.NewGuid(),
            Correo = "user@test.com",
            Nombre = "User",
            PlanActivo = "Free"
        }, "WEB");

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal("web", jwt.Claims.Single(claim => claim.Type == "client").Value);
    }
}
