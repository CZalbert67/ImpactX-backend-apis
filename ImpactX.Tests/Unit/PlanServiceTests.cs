using ImpactX.Core.Exceptions;
using Moq;
using ImpactX.Core.Domain;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Models.DTOs;
using ImpactX.Services;

namespace ImpactX.Tests.Unit;

public class PlanServiceTests
{
    private readonly Mock<IPlanRepository> _planRepo;
    private readonly Mock<ISuscripcionRepository> _suscripcionRepo;
    private readonly Mock<IPagoRepository> _pagoRepo;
    private readonly Mock<IUsuarioRepository> _usuarioRepo;
    private readonly PlanService _planService;

    public PlanServiceTests()
    {
        _planRepo = new Mock<IPlanRepository>();
        _suscripcionRepo = new Mock<ISuscripcionRepository>();
        _pagoRepo = new Mock<IPagoRepository>();
        _usuarioRepo = new Mock<IUsuarioRepository>();
        _planService = new PlanService(_planRepo.Object, _suscripcionRepo.Object, _pagoRepo.Object, _usuarioRepo.Object);
    }

    [Fact]
    public async Task GetAllPlansAsync_ReturnsAllPlans()
    {
        _planRepo.Setup(r => r.GetAllAsync())
            .ReturnsAsync([
                new Plan { Id = Guid.NewGuid(), Nombre = "Free", PrecioMensual = 0 },
                new Plan { Id = Guid.NewGuid(), Nombre = "Basic", PrecioMensual = 99 },
                new Plan { Id = Guid.NewGuid(), Nombre = "Premium", PrecioMensual = 199 },
            ]);

        var result = await _planService.GetAllPlansAsync();

        Assert.Equal(3, result.Count);
        Assert.Equal("Free", result[0].Nombre);
        Assert.Equal("Basic", result[1].Nombre);
        Assert.Equal("Premium", result[2].Nombre);
    }

    [Fact]
    public async Task GetCurrentSubscriptionAsync_WithActiveSubscription_ReturnsDto()
    {
        var usuarioId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var suscripcion = new Suscripcion { Id = Guid.NewGuid(), UsuarioId = usuarioId, PlanId = planId, Estado = "Activa" };
        var plan = new Plan { Id = planId, Nombre = "Premium" };

        _suscripcionRepo.Setup(r => r.GetActiveByUserAsync(usuarioId)).ReturnsAsync(suscripcion);
        _planRepo.Setup(r => r.GetByIdAsync(planId)).ReturnsAsync(plan);

        var result = await _planService.GetCurrentSubscriptionAsync(usuarioId);

        Assert.NotNull(result);
        Assert.Equal("Premium", result!.PlanNombre);
        Assert.Equal("Activa", result.Estado);
    }

    [Fact]
    public async Task GetCurrentSubscriptionAsync_WithoutSubscription_ReturnsNull()
    {
        _suscripcionRepo.Setup(r => r.GetActiveByUserAsync(It.IsAny<Guid>())).ReturnsAsync((Suscripcion?)null);

        var result = await _planService.GetCurrentSubscriptionAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetSubscriptionHistoryAsync_ReturnsHistory()
    {
        var usuarioId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        _suscripcionRepo.Setup(r => r.GetHistoryByUserAsync(usuarioId))
            .ReturnsAsync([
                new Suscripcion { Id = Guid.NewGuid(), UsuarioId = usuarioId, PlanId = planId, Estado = "Activa" },
                new Suscripcion { Id = Guid.NewGuid(), UsuarioId = usuarioId, PlanId = planId, Estado = "Expirada" },
            ]);
        _planRepo.Setup(r => r.GetByIdAsync(planId)).ReturnsAsync(new Plan { Id = planId, Nombre = "Basic" });

        var result = await _planService.GetSubscriptionHistoryAsync(usuarioId);

        Assert.Equal(2, result.Count);
        Assert.Equal("Basic", result[0].PlanNombre);
    }

    [Fact]
    public async Task ChangePlanAsync_WithHigherPlan_ReturnsSuccess()
    {
        var usuarioId = Guid.NewGuid();
        var basicPlan = new Plan { Id = Guid.NewGuid(), Nombre = "Basic", PrecioMensual = 99 };
        var premiumPlan = new Plan { Id = Guid.NewGuid(), Nombre = "Premium", PrecioMensual = 199 };
        var currentSuscripcion = new Suscripcion { Id = Guid.NewGuid(), UsuarioId = usuarioId, PlanId = basicPlan.Id, Estado = "Activa" };
        var usuario = new Usuario { Id = usuarioId, PlanActivo = "Basic" };

        _planRepo.Setup(r => r.GetByNameAsync("Premium")).ReturnsAsync(premiumPlan);
        _suscripcionRepo.Setup(r => r.GetActiveByUserAsync(usuarioId)).ReturnsAsync(currentSuscripcion);
        _planRepo.Setup(r => r.GetByIdAsync(basicPlan.Id)).ReturnsAsync(basicPlan);
        _usuarioRepo.Setup(r => r.GetByIdAsync(usuarioId)).ReturnsAsync(usuario);

        var result = await _planService.ChangePlanAsync(usuarioId, new ChangePlanRequest { PlanNombre = "Premium" });

        Assert.Equal("Premium", result.PlanNombre);
        Assert.Equal("Activa", result.Estado);
        Assert.Equal("Premium", usuario.PlanActivo);
        _suscripcionRepo.Verify(r => r.AddAsync(It.IsAny<Suscripcion>()), Times.Once);
        _usuarioRepo.Verify(r => r.UpdateAsync(usuario), Times.Once);
    }

    [Fact]
    public async Task ChangePlanAsync_WithSameOrLowerPlan_Throws()
    {
        var usuarioId = Guid.NewGuid();
        var premiumPlan = new Plan { Id = Guid.NewGuid(), Nombre = "Premium" };
        var basicPlan = new Plan { Id = Guid.NewGuid(), Nombre = "Basic" };
        var currentSuscripcion = new Suscripcion { Id = Guid.NewGuid(), UsuarioId = usuarioId, PlanId = premiumPlan.Id, Estado = "Activa" };

        _planRepo.Setup(r => r.GetByNameAsync("Basic")).ReturnsAsync(basicPlan);
        _suscripcionRepo.Setup(r => r.GetActiveByUserAsync(usuarioId)).ReturnsAsync(currentSuscripcion);
        _planRepo.Setup(r => r.GetByIdAsync(premiumPlan.Id)).ReturnsAsync(premiumPlan);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _planService.ChangePlanAsync(usuarioId, new ChangePlanRequest { PlanNombre = "Basic" }));
    }

    [Fact]
    public async Task ChangePlanAsync_WithNonExistentPlan_Throws()
    {
        _planRepo.Setup(r => r.GetByNameAsync("NonExistent")).ReturnsAsync((Plan?)null);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            _planService.ChangePlanAsync(Guid.NewGuid(), new ChangePlanRequest { PlanNombre = "NonExistent" }));
    }

    [Fact]
    public async Task CancelSubscriptionAsync_WithActiveSubscription_Cancels()
    {
        var usuarioId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var suscripcion = new Suscripcion { Id = Guid.NewGuid(), UsuarioId = usuarioId, PlanId = planId, Estado = "Activa" };
        var plan = new Plan { Id = planId, Nombre = "Premium" };
        var usuario = new Usuario { Id = usuarioId, PlanActivo = "Premium" };

        _suscripcionRepo.Setup(r => r.GetActiveByUserAsync(usuarioId)).ReturnsAsync(suscripcion);
        _planRepo.Setup(r => r.GetByIdAsync(planId)).ReturnsAsync(plan);
        _usuarioRepo.Setup(r => r.GetByIdAsync(usuarioId)).ReturnsAsync(usuario);

        var result = await _planService.CancelSubscriptionAsync(usuarioId, new CancelSubscriptionRequest { Motivo = "Muy caro" });

        Assert.Equal("Cancelada", result.Estado);
        Assert.Equal("Premium", result.PlanNombre);
        Assert.NotNull(result.CanceladaEn);
        Assert.Equal("Free", usuario.PlanActivo);
        _suscripcionRepo.Verify(r => r.UpdateAsync(suscripcion), Times.Once);
        _usuarioRepo.Verify(r => r.UpdateAsync(usuario), Times.Once);
    }

    [Fact]
    public async Task CancelSubscriptionAsync_WithoutActiveSubscription_Throws()
    {
        _suscripcionRepo.Setup(r => r.GetActiveByUserAsync(It.IsAny<Guid>())).ReturnsAsync((Suscripcion?)null);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _planService.CancelSubscriptionAsync(Guid.NewGuid(), null));
    }

    [Fact]
    public async Task GetPaymentsAsync_ReturnsPayments()
    {
        var usuarioId = Guid.NewGuid();
        _pagoRepo.Setup(r => r.GetByUserAsync(usuarioId))
            .ReturnsAsync([
                new Pago { Id = Guid.NewGuid(), UsuarioId = usuarioId, Monto = 199, Estado = "Completado" },
            ]);

        var result = await _planService.GetPaymentsAsync(usuarioId);

        Assert.Single(result);
        Assert.Equal(199, result[0].Monto);
        Assert.Equal("Completado", result[0].Estado);
    }

    [Fact]
    public async Task GetPaymentReceiptAsync_WithOwnPayment_ReturnsDto()
    {
        var usuarioId = Guid.NewGuid();
        var pago = new Pago { Id = Guid.NewGuid(), UsuarioId = usuarioId, Monto = 99 };

        _pagoRepo.Setup(r => r.GetByIdAsync(pago.Id)).ReturnsAsync(pago);

        var result = await _planService.GetPaymentReceiptAsync(pago.Id, usuarioId);

        Assert.NotNull(result);
        Assert.Equal(99, result!.Monto);
    }

    [Fact]
    public async Task GetPaymentReceiptAsync_WithOtherUsersPayment_ReturnsNull()
    {
        var usuarioId = Guid.NewGuid();
        var otroUsuarioId = Guid.NewGuid();
        var pago = new Pago { Id = Guid.NewGuid(), UsuarioId = otroUsuarioId };

        _pagoRepo.Setup(r => r.GetByIdAsync(pago.Id)).ReturnsAsync(pago);

        var result = await _planService.GetPaymentReceiptAsync(pago.Id, usuarioId);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetPaymentReceiptAsync_WithNonExistentPayment_ReturnsNull()
    {
        _pagoRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Pago?)null);

        var result = await _planService.GetPaymentReceiptAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task ExpireSubscriptionsAsync_ExpiresOverdueSubscriptions()
    {
        var usuarioId = Guid.NewGuid();
        var expired = new Suscripcion { Id = Guid.NewGuid(), UsuarioId = usuarioId, Estado = "Activa" };
        var usuario = new Usuario { Id = usuarioId, PlanActivo = "Premium" };

        _suscripcionRepo.Setup(r => r.GetExpiredAsync()).ReturnsAsync([expired]);
        _usuarioRepo.Setup(r => r.GetByIdAsync(usuarioId)).ReturnsAsync(usuario);

        var count = await _planService.ExpireSubscriptionsAsync();

        Assert.Equal(1, count);
        Assert.Equal("Expirada", expired.Estado);
        Assert.Equal("Free", usuario.PlanActivo);
        _suscripcionRepo.Verify(r => r.UpdateAsync(expired), Times.Once);
        _usuarioRepo.Verify(r => r.UpdateAsync(usuario), Times.Once);
    }

    [Fact]
    public async Task ExpireSubscriptionsAsync_WithNoExpired_ReturnsZero()
    {
        _suscripcionRepo.Setup(r => r.GetExpiredAsync()).ReturnsAsync([]);

        var count = await _planService.ExpireSubscriptionsAsync();

        Assert.Equal(0, count);
    }
}
