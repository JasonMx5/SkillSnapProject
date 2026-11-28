using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SkillSnap.Api.Models;
using NotificationResponse = SkillSnap.Shared.Models.NotificationResponse;

namespace SkillSnap.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificationsController(SkillSnapContext context) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<NotificationResponse>>> GetMyNotifications()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        return await context.UserNotifications
            .AsNoTracking()
            .Where(notification => notification.UserId == userId)
            .OrderByDescending(notification => notification.CreatedAtUtc)
            .Take(50)
            .Select(notification => new NotificationResponse(
                notification.Id,
                notification.ProjectId,
                notification.Message,
                notification.CreatedAtUtc,
                notification.ReadAtUtc))
            .ToListAsync();
    }

    [HttpPost("{id:int}/read")]
    public async Task<IActionResult> MarkAsRead(int id)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var notification = await context.UserNotifications
            .FirstOrDefaultAsync(notification => notification.Id == id && notification.UserId == userId);
        if (notification is null)
        {
            return NotFound();
        }

        notification.ReadAtUtc ??= DateTime.UtcNow;
        await context.SaveChangesAsync();

        return NoContent();
    }
}
