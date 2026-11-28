using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SkillSnap.Api.Models;

namespace SkillSnap.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SkillsController(
    SkillSnapContext context,
    IMemoryCache memoryCache,
    ILogger<SkillsController> logger) : ControllerBase
{
    private const string SkillsCacheKey = "Skills_All";

    [HttpGet]
    public async Task<ActionResult<List<SkillResponse>>> GetSkills()
    {
        var stopwatch = Stopwatch.StartNew();
        var cacheHit = memoryCache.TryGetValue(SkillsCacheKey, out List<SkillResponse>? skills);

        try
        {
            if (!cacheHit)
            {
                skills = await memoryCache.GetOrCreateAsync(SkillsCacheKey, async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                    var skillEntities = await context.Skills
                        .AsNoTracking()
                        .Include(skill => skill.PortfolioUser)
                        .ToListAsync();

                    return skillEntities.Select(skill => new SkillResponse(
                        skill.Id,
                        skill.Name,
                        skill.Level,
                        skill.PortfolioUserId,
                        skill.PortfolioUser is null
                            ? null
                            : new PortfolioUserSummary(skill.PortfolioUser.Id, skill.PortfolioUser.Name)))
                        .ToList();
                });
            }

            return skills ?? throw new InvalidOperationException("The skills cache returned no result.");
        }
        finally
        {
            stopwatch.Stop();
            logger.LogInformation(
                "Skills GET cache {CacheResult}; completed in {ElapsedMilliseconds:F2} ms",
                cacheHit ? "hit" : "miss",
                stopwatch.Elapsed.TotalMilliseconds);
        }
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<Skill>> CreateSkill(Skill skill)
    {
        context.Skills.Add(skill);
        await context.SaveChangesAsync();
        memoryCache.Remove(SkillsCacheKey);

        return StatusCode(StatusCodes.Status201Created, skill);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateSkill(int id, Skill skill)
    {
        var existingSkill = await context.Skills.FindAsync(id);
        if (existingSkill is null)
        {
            return NotFound();
        }

        existingSkill.Name = skill.Name;
        existingSkill.Level = skill.Level;
        existingSkill.PortfolioUserId = skill.PortfolioUserId;

        await context.SaveChangesAsync();
        memoryCache.Remove(SkillsCacheKey);

        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteSkill(int id)
    {
        var skill = await context.Skills.FindAsync(id);
        if (skill is null)
        {
            return NotFound();
        }

        context.Skills.Remove(skill);
        await context.SaveChangesAsync();
        memoryCache.Remove(SkillsCacheKey);

        return NoContent();
    }
}
