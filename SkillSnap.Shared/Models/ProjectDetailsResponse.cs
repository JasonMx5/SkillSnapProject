namespace SkillSnap.Shared.Models;

public sealed record ProjectDetailsResponse(
    int Id,
    string Title,
    string Description,
    string ImageUrl,
    int PortfolioUserId,
    ProjectOwnerDetails? Owner,
    List<ProjectOwnerSkill> OwnerSkills);

public sealed record ProjectOwnerDetails(
    int Id,
    string Name,
    string Bio,
    string ProfileImageUrl);

public sealed record ProjectOwnerSkill(int Id, string Name, string Level);
