using System.Security.Cryptography;
using System.Text;
using ImpactX.Core.Domain;
using ImpactX.Core.Exceptions;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Models.DTOs;

namespace ImpactX.Services;

public class SettingsService : ISettingsService
{
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly ILogger<SettingsService> _logger;

    public SettingsService(
        IUsuarioRepository usuarioRepository,
        ILogger<SettingsService> logger)
    {
        _usuarioRepository = usuarioRepository;
        _logger = logger;
    }

    public async Task<SettingsResponseDto> GetSettingsAsync(Guid usuarioId)
    {
        var usuario = await _usuarioRepository.GetByIdAsync(usuarioId)
            ?? throw new NotFoundException("Usuario no encontrado.");

        return MapToDto(usuario);
    }

    public async Task<SettingsResponseDto> UpdateSettingsAsync(Guid usuarioId, UpdateSettingsRequest request)
    {
        var usuario = await _usuarioRepository.GetByIdAsync(usuarioId)
            ?? throw new NotFoundException("Usuario no encontrado.");

        usuario.Preferencias ??= new PreferenciasUsuario();

        if (request.Idioma is not null)
            usuario.Preferencias.Idioma = request.Idioma;
        if (request.UnidadVelocidad is not null)
            usuario.Preferencias.UnidadVelocidad = request.UnidadVelocidad;
        if (request.NotificacionesPush.HasValue)
            usuario.Preferencias.NotificacionesPush = request.NotificacionesPush.Value;
        if (request.NotificacionesEmail.HasValue)
            usuario.Preferencias.NotificacionesEmail = request.NotificacionesEmail.Value;
        if (request.CompartirUbicacion.HasValue)
            usuario.Preferencias.CompartirUbicacion = request.CompartirUbicacion.Value;

        await _usuarioRepository.UpdateAsync(usuario);
        _logger.LogInformation("Settings actualizados para usuario {UsuarioId}", usuarioId);

        return MapToDto(usuario);
    }

    public async Task<Setup2FaResponse> Setup2FaAsync(Guid usuarioId)
    {
        var usuario = await _usuarioRepository.GetByIdAsync(usuarioId)
            ?? throw new NotFoundException("Usuario no encontrado.");

        if (usuario.Settings?.TwoFactorEnabled == true)
            throw new ConflictException("2FA ya está activo. Desactívalo primero para reconfigurar.");

        var secret = GenerateTotpSecret();
        var issuer = "ImpactX";
        var label = $"{issuer}:{usuario.Correo}";
        var qrUri = $"otpauth://totp/{Uri.EscapeDataString(label)}?secret={secret}&issuer={issuer}&algorithm=SHA1&digits=6&period=30";

        usuario.Settings ??= new SettingsUsuario();
        usuario.Settings.TwoFactorSecret = secret;
        usuario.Settings.TwoFactorEnabled = false;
        usuario.Settings.TwoFactorVerifiedAt = null;

        await _usuarioRepository.UpdateAsync(usuario);
        _logger.LogInformation("2FA setup iniciado para usuario {UsuarioId}", usuarioId);

        var formatted = $"{secret[..4]} {secret[4..8]} {secret[8..12]} {secret[12..]}";

        return new Setup2FaResponse
        {
            Secret = secret,
            QrCodeUri = qrUri,
            ManualKey = formatted,
        };
    }

    public async Task Enable2FaAsync(Guid usuarioId, Enable2FaRequest request)
    {
        var usuario = await _usuarioRepository.GetByIdAsync(usuarioId)
            ?? throw new NotFoundException("Usuario no encontrado.");

        if (usuario.Settings?.TwoFactorSecret is null)
            throw new ConflictException("Debes iniciar la configuración 2FA primero (POST /api/settings/2fa/setup).");

        if (usuario.Settings.TwoFactorEnabled)
            throw new ConflictException("2FA ya está activo.");

        if (string.IsNullOrWhiteSpace(request.Code) || request.Code.Length < 6)
            throw new BadRequestException("Código de verificación inválido. Debe tener 6 dígitos.");

        var isValid = VerifyTotpCode(usuario.Settings.TwoFactorSecret, request.Code);
        if (!isValid)
            throw new BadRequestException("Código inválido. Verifica que la hora del dispositivo esté sincronizada.");

        usuario.Settings.TwoFactorEnabled = true;
        usuario.Settings.TwoFactorVerifiedAt = DateTime.UtcNow;

        await _usuarioRepository.UpdateAsync(usuario);
        _logger.LogWarning("2FA activado para usuario {UsuarioId}", usuarioId);
    }

    public async Task Disable2FaAsync(Guid usuarioId, Disable2FaRequest request)
    {
        var usuario = await _usuarioRepository.GetByIdAsync(usuarioId)
            ?? throw new NotFoundException("Usuario no encontrado.");

        if (usuario.Settings?.TwoFactorEnabled != true)
            throw new ConflictException("2FA no está activo.");

        if (string.IsNullOrWhiteSpace(request.Code) || request.Code.Length < 6)
            throw new BadRequestException("Código de verificación inválido.");

        var isValid = VerifyTotpCode(usuario.Settings.TwoFactorSecret!, request.Code);
        if (!isValid)
            throw new BadRequestException("Código inválido.");

        usuario.Settings.TwoFactorEnabled = false;
        usuario.Settings.TwoFactorSecret = null;
        usuario.Settings.TwoFactorVerifiedAt = null;

        await _usuarioRepository.UpdateAsync(usuario);
        _logger.LogWarning("2FA desactivado para usuario {UsuarioId}", usuarioId);
    }

    private static string GenerateTotpSecret()
    {
        var bytes = RandomNumberGenerator.GetBytes(20);
        return ConvertToBase32(bytes);
    }

    private static string ConvertToBase32(byte[] data)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var result = new StringBuilder();
        var bitCount = 0;
        var currentByte = 0;

        foreach (var b in data)
        {
            currentByte = (currentByte << 8) | b;
            bitCount += 8;

            while (bitCount >= 5)
            {
                bitCount -= 5;
                result.Append(alphabet[(currentByte >> bitCount) & 0x1F]);
            }
        }

        if (bitCount > 0)
        {
            currentByte <<= (5 - bitCount);
            result.Append(alphabet[currentByte & 0x1F]);
        }

        return result.ToString();
    }

    internal static bool VerifyTotpCode(string secret, string code)
    {
        var secretBytes = FromBase32(secret);
        var window = 1;

        var timeNow = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;

        for (var i = -window; i <= window; i++)
        {
            var expectedCode = GenerateTotpCode(secretBytes, timeNow + i);
            if (expectedCode == code)
                return true;
        }

        return false;
    }

    private static string GenerateTotpCode(byte[] secretBytes, long timeCounter)
    {
        var timeBytes = BitConverter.GetBytes(timeCounter);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(timeBytes);

        using var hmac = new HMACSHA1(secretBytes);
        var hash = hmac.ComputeHash(timeBytes);

        var offset = hash[^1] & 0x0F;
        var binary = ((hash[offset] & 0x7F) << 24) |
                     ((hash[offset + 1] & 0xFF) << 16) |
                     ((hash[offset + 2] & 0xFF) << 8) |
                     (hash[offset + 3] & 0xFF);

        var otp = binary % 1_000_000;
        return otp.ToString("D6");
    }

    internal static byte[] FromBase32(string base32)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        base32 = base32.TrimEnd('=').ToUpperInvariant();

        var byteCount = base32.Length * 5 / 8;
        var result = new byte[byteCount];

        var buffer = 0;
        var bitsRemaining = 0;
        var idx = 0;

        foreach (var c in base32)
        {
            var value = alphabet.IndexOf(c);
            if (value < 0) continue;

            buffer = (buffer << 5) | value;
            bitsRemaining += 5;

            if (bitsRemaining >= 8 && idx < byteCount)
            {
                bitsRemaining -= 8;
                result[idx++] = (byte)((buffer >> bitsRemaining) & 0xFF);
            }
        }

        return result;
    }

    private static SettingsResponseDto MapToDto(Usuario u) => new()
    {
        Idioma = u.Preferencias?.Idioma,
        UnidadVelocidad = u.Preferencias?.UnidadVelocidad,
        NotificacionesPush = u.Preferencias?.NotificacionesPush ?? true,
        NotificacionesEmail = u.Preferencias?.NotificacionesEmail ?? true,
        CompartirUbicacion = u.Preferencias?.CompartirUbicacion ?? true,
        TwoFactorEnabled = u.Settings?.TwoFactorEnabled ?? false,
    };
}
