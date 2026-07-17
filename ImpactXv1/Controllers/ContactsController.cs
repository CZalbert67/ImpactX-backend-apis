using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ImpactX.Models.DTOs;
using ImpactX.Services;

namespace ImpactX.Controllers;

[ApiController]
[Route("api/contacts")]
[Authorize]
public class ContactsController : ControllerBase
{
    private readonly IContactService _contactService;

    public ContactsController(IContactService contactService)
    {
        _contactService = contactService;
    }

    [HttpGet]
    public async Task<IActionResult> GetContacts()
    {
        var usuarioId = GetUsuarioId();
        var contacts = await _contactService.GetContactsAsync(usuarioId);
        return Ok(contacts);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetContactById(Guid id)
    {
        var usuarioId = GetUsuarioId();
        var contact = await _contactService.GetContactByIdAsync(usuarioId, id);
        return Ok(contact);
    }

    [HttpPost]
    public async Task<IActionResult> CreateContact([FromBody] CreateContactoRequest request)
    {
        var usuarioId = GetUsuarioId();
        var contact = await _contactService.CreateContactAsync(usuarioId, request);
        return CreatedAtAction(nameof(GetContactById), new { id = contact.Id }, contact);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateContact(Guid id, [FromBody] UpdateContactoRequest request)
    {
        var usuarioId = GetUsuarioId();
        var contact = await _contactService.UpdateContactAsync(usuarioId, id, request);
        return Ok(contact);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteContact(Guid id)
    {
        var usuarioId = GetUsuarioId();
        await _contactService.DeleteContactAsync(usuarioId, id);
        return Ok(new { mensaje = "Contacto eliminado exitosamente." });
    }

    [HttpPatch("make-primary")]
    public async Task<IActionResult> MakePrimary([FromBody] MakePrimaryRequest request)
    {
        var usuarioId = GetUsuarioId();
        var contact = await _contactService.MakePrimaryAsync(usuarioId, request);
        return Ok(contact);
    }

    [HttpGet("sync")]
    public async Task<IActionResult> GetSyncData()
    {
        var usuarioId = GetUsuarioId();
        var data = await _contactService.GetSyncDataAsync(usuarioId);
        return Ok(data);
    }

    private Guid GetUsuarioId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return Guid.Parse(claim!.Value);
    }
}
