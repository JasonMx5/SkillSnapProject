using SkillSnap.Shared.Models;

namespace SkillSnap.Api.Models;

public class ProjectEnrollment
{
    public string UserId { get; set; } = string.Empty;

    public ApplicationUser? User { get; set; }

    public int ProjectId { get; set; }

    public Project? Project { get; set; }

    public DateTime EnrolledAtUtc { get; set; }

    public ProjectEnrollmentStatus Status { get; set; } = ProjectEnrollmentStatus.Active;
}
