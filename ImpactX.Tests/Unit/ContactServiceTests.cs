using ImpactX.Core.Exceptions;
using Microsoft.Extensions.Logging;
using Moq;
using ImpactX.Core.Domain;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Models.DTOs;
using ImpactX.Services;

namespace ImpactX.Tests.Unit;

public class ContactServiceTests
{
    private readonly Mock<IContactoRepository> _contactoRepo;
    private readonly Mock<IPlanService> _planService;
    private readonly ContactService _contactService;

    public ContactServiceTests()
    {
        _contactoRepo = new Mock<IContactoRepository>();
        _planService = new Mock<IPlanService>();
        var logger = Mock.Of<ILogger<ContactService>>();
        _contactService = new ContactService(_contactoRepo.Object, _planService.Object, logger);
    }

    [Fact]
    public async Task GetContactsAsync_ReturnsList()
    {
        var usuarioId = Guid.NewGuid();
        _contactoRepo.Setup(r => r.GetByUserAsync(usuarioId))
            .ReturnsAsync([new ContactoEmergencia { Id = Guid.NewGuid(), UsuarioId = usuarioId, Nombre = "Test", Telefono = "555-0001" }]);

        var result = await _contactService.GetContactsAsync(usuarioId);

        Assert.Single(result);
    }

    [Fact]
    public async Task GetContactByIdAsync_WithValidId_ReturnsDto()
    {
        var usuarioId = Guid.NewGuid();
        var contacto = new ContactoEmergencia { Id = Guid.NewGuid(), UsuarioId = usuarioId, Nombre = "Test", Telefono = "555-0001" };
        _contactoRepo.Setup(r => r.GetByIdAsync(contacto.Id)).ReturnsAsync(contacto);

        var result = await _contactService.GetContactByIdAsync(usuarioId, contacto.Id);

        Assert.Equal("Test", result.Nombre);
    }

    [Fact]
    public async Task GetContactByIdAsync_WithWrongUser_Throws()
    {
        var usuarioId = Guid.NewGuid();
        var otroUsuarioId = Guid.NewGuid();
        var contacto = new ContactoEmergencia { Id = Guid.NewGuid(), UsuarioId = otroUsuarioId };
        _contactoRepo.Setup(r => r.GetByIdAsync(contacto.Id)).ReturnsAsync(contacto);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _contactService.GetContactByIdAsync(usuarioId, contacto.Id));
    }

    [Fact]
    public async Task GetContactByIdAsync_NotFound_Throws()
    {
        _contactoRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((ContactoEmergencia?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _contactService.GetContactByIdAsync(Guid.NewGuid(), Guid.NewGuid()));
    }

    [Fact]
    public async Task CreateContactAsync_UnderLimit_Creates()
    {
        var usuarioId = Guid.NewGuid();
        _planService.Setup(s => s.GetCurrentSubscriptionAsync(usuarioId))
            .ReturnsAsync(new SuscripcionDto { PlanNombre = "Premium" });
        _contactoRepo.Setup(r => r.CountByUserAsync(usuarioId)).ReturnsAsync(0);
        _contactoRepo.Setup(r => r.ExistsByTelefonoAsync(usuarioId, "555-0001")).ReturnsAsync(false);

        var result = await _contactService.CreateContactAsync(usuarioId, new CreateContactoRequest
        {
            Nombre = "Juan",
            Telefono = "555-0001",
            Parentesco = "Hermano",
        });

        Assert.Equal("Juan", result.Nombre);
        Assert.Equal("555-0001", result.Telefono);
        _contactoRepo.Verify(r => r.AddAsync(It.IsAny<ContactoEmergencia>()), Times.Once);
    }

    [Fact]
    public async Task CreateContactAsync_FreePlanOverLimit_Throws()
    {
        var usuarioId = Guid.NewGuid();
        _planService.Setup(s => s.GetCurrentSubscriptionAsync(usuarioId))
            .ReturnsAsync(new SuscripcionDto { PlanNombre = "Free" });
        _contactoRepo.Setup(r => r.CountByUserAsync(usuarioId)).ReturnsAsync(3);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _contactService.CreateContactAsync(usuarioId, new CreateContactoRequest
            {
                Nombre = "Test",
                Telefono = "555-0001",
            }));
    }

    [Fact]
    public async Task CreateContactAsync_DuplicateTelefono_Throws()
    {
        var usuarioId = Guid.NewGuid();
        _planService.Setup(s => s.GetCurrentSubscriptionAsync(usuarioId))
            .ReturnsAsync(new SuscripcionDto { PlanNombre = "Premium" });
        _contactoRepo.Setup(r => r.CountByUserAsync(usuarioId)).ReturnsAsync(0);
        _contactoRepo.Setup(r => r.ExistsByTelefonoAsync(usuarioId, "555-0001")).ReturnsAsync(true);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _contactService.CreateContactAsync(usuarioId, new CreateContactoRequest
            {
                Nombre = "Test",
                Telefono = "555-0001",
            }));
    }

    [Fact]
    public async Task CreateContactAsync_AsPrincipal_UnsetsOther()
    {
        var usuarioId = Guid.NewGuid();
        var principal = new ContactoEmergencia { Id = Guid.NewGuid(), UsuarioId = usuarioId, EsPrincipal = true };
        _planService.Setup(s => s.GetCurrentSubscriptionAsync(usuarioId))
            .ReturnsAsync(new SuscripcionDto { PlanNombre = "Premium" });
        _contactoRepo.Setup(r => r.CountByUserAsync(usuarioId)).ReturnsAsync(0);
        _contactoRepo.Setup(r => r.ExistsByTelefonoAsync(usuarioId, "555-0001")).ReturnsAsync(false);
        _contactoRepo.Setup(r => r.GetPrincipalAsync(usuarioId)).ReturnsAsync(principal);

        await _contactService.CreateContactAsync(usuarioId, new CreateContactoRequest
        {
            Nombre = "New",
            Telefono = "555-0001",
            EsPrincipal = true,
        });

        Assert.False(principal.EsPrincipal);
        _contactoRepo.Verify(r => r.UpdateAsync(principal), Times.Once);
    }

    [Fact]
    public async Task UpdateContactAsync_UpdatesFields()
    {
        var usuarioId = Guid.NewGuid();
        var contacto = new ContactoEmergencia { Id = Guid.NewGuid(), UsuarioId = usuarioId, Nombre = "Old", Telefono = "555-0001" };
        _contactoRepo.Setup(r => r.GetByIdAsync(contacto.Id)).ReturnsAsync(contacto);

        var result = await _contactService.UpdateContactAsync(usuarioId, contacto.Id, new UpdateContactoRequest
        {
            Nombre = "New Name",
            Parentesco = "Primo",
        });

        Assert.Equal("New Name", result.Nombre);
        Assert.Equal("Primo", result.Parentesco);
        _contactoRepo.Verify(r => r.UpdateAsync(contacto), Times.Once);
    }

    [Fact]
    public async Task UpdateContactAsync_WithWrongUser_Throws()
    {
        var usuarioId = Guid.NewGuid();
        var otroUsuarioId = Guid.NewGuid();
        var contacto = new ContactoEmergencia { Id = Guid.NewGuid(), UsuarioId = otroUsuarioId };
        _contactoRepo.Setup(r => r.GetByIdAsync(contacto.Id)).ReturnsAsync(contacto);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _contactService.UpdateContactAsync(usuarioId, contacto.Id, new UpdateContactoRequest()));
    }

    [Fact]
    public async Task DeleteContactAsync_Deletes()
    {
        var usuarioId = Guid.NewGuid();
        var contacto = new ContactoEmergencia { Id = Guid.NewGuid(), UsuarioId = usuarioId };
        _contactoRepo.Setup(r => r.GetByIdAsync(contacto.Id)).ReturnsAsync(contacto);

        await _contactService.DeleteContactAsync(usuarioId, contacto.Id);

        _contactoRepo.Verify(r => r.DeleteAsync(contacto), Times.Once);
    }

    [Fact]
    public async Task DeleteContactAsync_WithWrongUser_Throws()
    {
        var usuarioId = Guid.NewGuid();
        var otroUsuarioId = Guid.NewGuid();
        var contacto = new ContactoEmergencia { Id = Guid.NewGuid(), UsuarioId = otroUsuarioId };
        _contactoRepo.Setup(r => r.GetByIdAsync(contacto.Id)).ReturnsAsync(contacto);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _contactService.DeleteContactAsync(usuarioId, contacto.Id));
    }

    [Fact]
    public async Task MakePrimaryAsync_SetsPrimaryAndUnsetsOther()
    {
        var usuarioId = Guid.NewGuid();
        var principal = new ContactoEmergencia { Id = Guid.NewGuid(), UsuarioId = usuarioId, EsPrincipal = true };
        var nuevo = new ContactoEmergencia { Id = Guid.NewGuid(), UsuarioId = usuarioId, EsPrincipal = false };
        _contactoRepo.Setup(r => r.GetByIdAsync(nuevo.Id)).ReturnsAsync(nuevo);
        _contactoRepo.Setup(r => r.GetPrincipalAsync(usuarioId)).ReturnsAsync(principal);

        var result = await _contactService.MakePrimaryAsync(usuarioId, new MakePrimaryRequest { ContactoId = nuevo.Id });

        Assert.True(result.EsPrincipal);
        Assert.False(principal.EsPrincipal);
        _contactoRepo.Verify(r => r.UpdateAsync(principal), Times.Once);
        _contactoRepo.Verify(r => r.UpdateAsync(nuevo), Times.Once);
    }

    [Fact]
    public async Task GetSyncDataAsync_ReturnsAllContacts()
    {
        var usuarioId = Guid.NewGuid();
        _contactoRepo.Setup(r => r.GetByUserAsync(usuarioId))
            .ReturnsAsync([new ContactoEmergencia { Id = Guid.NewGuid(), UsuarioId = usuarioId, Nombre = "A", Telefono = "1" }]);

        var result = await _contactService.GetSyncDataAsync(usuarioId);

        Assert.Single(result.Contactos);
    }
}
