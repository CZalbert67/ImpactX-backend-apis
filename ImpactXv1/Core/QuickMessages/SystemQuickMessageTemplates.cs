namespace ImpactX.Core.QuickMessages;

public sealed record SystemQuickMessageTemplate(string PublicTemplateId, string Text, int SortOrder);

public static class SystemQuickMessageTemplates
{
    public static IReadOnlyList<SystemQuickMessageTemplate> All { get; } =
    [
        new("SYS-QM-001", "Estoy bien", 1),
        new("SYS-QM-002", "Necesito ayuda", 2),
        new("SYS-QM-003", "Llámame cuando puedas", 3),
        new("SYS-QM-004", "Revisa mi ubicación", 4),
        new("SYS-QM-005", "Voy en camino", 5),
        new("SYS-QM-006", "Tuve un incidente", 6),
        new("SYS-QM-007", "¿Estás bien?", 7),
        new("SYS-QM-008", "Confirma que recibiste la alerta", 8)
    ];

    public static SystemQuickMessageTemplate? Find(string publicTemplateId)
        => All.FirstOrDefault(value => value.PublicTemplateId == publicTemplateId);
}
