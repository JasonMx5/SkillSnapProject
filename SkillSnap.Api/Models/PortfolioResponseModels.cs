namespace SkillSnap.Api.Models;

public sealed record PortfolioUserSummary(int Id, string Name);

public sealed record ProjectResponse(
    int Id,
    string Title,
    string Description,
    string ImageUrl,
    int PortfolioUserId,
    PortfolioUserSummary? PortfolioUser);

public sealed record SkillResponse(
    int Id,
    string Name,
    string Level,
    int PortfolioUserId,
    PortfolioUserSummary? PortfolioUser);
