using System.Security.Claims;
using ImpactX.Core.Security;
using ImpactX.Models.DTOs;
using ImpactX.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ImpactX.Controllers;

[ApiController]
[Route("api/v1/account")]
[Authorize]
[RequireClientCapability(ClientTypePolicy.Web, ClientTypePolicy.Mobile)]
public sealed class AccountController : ControllerBase
{
    private readonly IAccountService _service;

    public AccountController(IAccountService service)
    {
        _service = service;
    }

    [HttpGet("export")]
    [ProducesResponseType(typeof(AccountExportV2Dto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Export(CancellationToken cancellationToken)
        => Ok(await _service.ExportAsync(GetUserId(), cancellationToken));

    [HttpGet("retention")]
    [ProducesResponseType(typeof(AccountRetentionDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRetention(CancellationToken cancellationToken)
        => Ok(await _service.GetRetentionAsync(GetUserId(), cancellationToken));

    [HttpPost("consents/revoke")]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> RevokeConsents(
        [FromBody] RevokeConsentsRequest request,
        CancellationToken cancellationToken)
        => Ok(await _service.RevokeConsentsAsync(GetUserId(), request, cancellationToken));

    [HttpDelete]
    [ProducesResponseType(typeof(DeleteAccountV2Response), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Delete(
        [FromBody] DeleteAccountV2Request request,
        CancellationToken cancellationToken)
        => Ok(await _service.DeleteAsync(GetUserId(), request, cancellationToken));

    private Guid GetUserId()
        => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
}
