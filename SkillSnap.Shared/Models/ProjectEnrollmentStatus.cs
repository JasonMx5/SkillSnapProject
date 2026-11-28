namespace SkillSnap.Shared.Models;

public enum ProjectEnrollmentStatus
{
    Active = 0,
    Pending = 1,
    Completed = 2
}

public sealed record ProjectEnrollmentSummary(
    int ProjectId,
    ProjectEnrollmentStatus Status,
    DateTime EnrolledAtUtc);

public sealed record ProjectEnrollmentAdminSummary(
    string UserId,
    string Email,
    ProjectEnrollmentStatus Status,
    DateTime EnrolledAtUtc);

public sealed record ProjectEnrollmentStatusUpdateRequest(ProjectEnrollmentStatus Status);
