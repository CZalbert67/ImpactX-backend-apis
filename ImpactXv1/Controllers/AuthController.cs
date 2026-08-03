using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ImpactX.Core.Identity;
using ImpactX.Models.DTOs;
using ImpactX.Services;

namespace ImpactX.Controllers;

[ApiController]
[Route("api/[controller]")]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpGet("registration-contract")]
    [ProducesResponseType(typeof(RegistrationContractDto), StatusCodes.Status200OK)]
    public IActionResult GetRegistrationContract()
    {
        return Ok(new RegistrationContractDto
        {
            ContractVersion = RegistrationContract.CurrentVersion,
            TermsVersion = RegistrationContract.TermsVersion,
            PrivacyNoticeVersion = RegistrationContract.PrivacyNoticeVersion,
            SupportedClients = RegistrationContract.SupportedAccountClients,
            RequiredFields =
            [
                "nombre",
                "username",
                "correo",
                "telefono",
                "password",
                "termsAccepted",
                "privacyAccepted",
                "client"
            ],
            Username = new UsernameRequirementsDto
            {
                MinLength = UsernamePolicy.MinLength,
                MaxLength = UsernamePolicy.MaxLength,
                Pattern = "^[a-zA-Z0-9](?:[a-zA-Z0-9._]*[a-zA-Z0-9])?$",
                Description = "Letras, números, punto y guion bajo; sin puntos consecutivos."
            },
            Password = new PasswordRequirementsDto
            {
                MinLength = RegistrationContract.PasswordMinLength,
                MaxLength = RegistrationContract.PasswordMaxLength,
                RequireUppercase = true,
                RequireLowercase = true,
                RequireDigit = true,
                RequireSpecialCharacter = true
            },
            ConfirmPasswordIsClientOnly = true
        });
    }

    [HttpPost("register")]
    [EnableRateLimiting("auth-register")]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var result = await _authService.RegisterAsync(request);

        if (!result.Success)
        {
            return Conflict(result);
        }

        return Ok(result);
    }

    [HttpPost("login")]
    [EnableRateLimiting("auth-login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _authService.LoginAsync(request);

        if (!result.Success)
            return Unauthorized(result);

        return Ok(result);
    }

    [HttpPost("recover-password")]
    [EnableRateLimiting("auth-recover")]
    public async Task<IActionResult> RecoverPassword([FromBody] RecoverPasswordRequest request)
    {
        var result = await _authService.RecoverPasswordAsync(request);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }



    [HttpPost("reset-password")]
    [EnableRateLimiting("auth-reset")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        var result = await _authService.ResetPasswordAsync(request);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var usuarioId = GetUsuarioId();
        var result = await _authService.ChangePasswordAsync(usuarioId, request);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request)
    {
        var usuarioId = GetUsuarioId();
        var result = await _authService.LogoutAsync(usuarioId, request.RefreshToken);

        return Ok(result);
    }

    [HttpPost("refresh")]
    [EnableRateLimiting("auth-refresh")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _authService.RefreshTokenAsync(request, ipAddress);

        if (!result.Success)
            return Unauthorized(result);

        return Ok(result);
    }

    [Authorize]
    [HttpGet("sessions")]
    public async Task<IActionResult> GetSessions()
    {
        var usuarioId = GetUsuarioId();
        var sessions = await _authService.GetSessionsAsync(usuarioId);
        return Ok(sessions);
    }

    [Authorize]
    [HttpDelete("sessions/{sessionId}")]
    public async Task<IActionResult> DeleteSession(Guid sessionId)
    {
        var usuarioId = GetUsuarioId();
        await _authService.DeleteSessionAsync(usuarioId, sessionId);
        return NoContent();
    }

    [Authorize]
    [HttpDelete("account")]
    public async Task<IActionResult> DeleteAccount()
    {
        var usuarioId = GetUsuarioId();
        await _authService.DeleteAccountAsync(usuarioId);
        return NoContent();
    }

    [Authorize]
    [HttpGet("account/export")]
    public async Task<IActionResult> ExportAccount()
    {
        var usuarioId = GetUsuarioId();
        var data = await _authService.ExportAccountAsync(usuarioId);
        return Ok(data);
    }

    private Guid GetUsuarioId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return Guid.Parse(claim!.Value);
    }
}
