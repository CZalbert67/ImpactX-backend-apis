namespace Prueba1.Models.DTOs;

public class SessionDto
{
    public Guid Id { get; set; }
    public string? DeviceInfo { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsActive { get; set; }
}
