namespace SkillSnap.Shared.Models;

public sealed record NotificationResponse(
    int Id,
    int ProjectId,
    string Message,
    DateTime CreatedAtUtc,
    DateTime? ReadAtUtc);
