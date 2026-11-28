using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SkillSnap.Api.Models;
using ProjectImageUploadResponse = SkillSnap.Shared.Models.ProjectImageUploadResponse;
using ProjectDetailsResponse = SkillSnap.Shared.Models.ProjectDetailsResponse;
using ProjectEnrollmentAdminSummary = SkillSnap.Shared.Models.ProjectEnrollmentAdminSummary;
using ProjectEnrollmentStatus = SkillSnap.Shared.Models.ProjectEnrollmentStatus;
using ProjectEnrollmentStatusUpdateRequest = SkillSnap.Shared.Models.ProjectEnrollmentStatusUpdateRequest;
using ProjectEnrollmentSummary = SkillSnap.Shared.Models.ProjectEnrollmentSummary;
using ProjectOwnerDetails = SkillSnap.Shared.Models.ProjectOwnerDetails;
using ProjectOwnerSkill = SkillSnap.Shared.Models.ProjectOwnerSkill;

namespace SkillSnap.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProjectsController(
    SkillSnapContext context,
    IMemoryCache memoryCache,
    IWebHostEnvironment environment,
    ILogger<ProjectsController> logger) : ControllerBase
{
    private const string ProjectsCacheKey = "Projects_All";
    private const long MaxProjectImageBytes = 5 * 1024 * 1024;

    [HttpGet]
    public async Task<ActionResult<List<ProjectResponse>>> GetProjects()
    {
        var stopwatch = Stopwatch.StartNew();
        var cacheHit = memoryCache.TryGetValue(ProjectsCacheKey, out List<ProjectResponse>? projects);

        try
        {
            if (!cacheHit)
            {
                projects = await memoryCache.GetOrCreateAsync(ProjectsCacheKey, async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                    var projectEntities = await context.Projects
                        .AsNoTracking()
                        .Include(project => project.PortfolioUser)
                        .ToListAsync();

                    return projectEntities.Select(project => new ProjectResponse(
                        project.Id,
                        project.Title,
                        project.Description,
                        project.ImageUrl,
                        project.PortfolioUserId,
                        project.PortfolioUser is null
                            ? null
                            : new PortfolioUserSummary(project.PortfolioUser.Id, project.PortfolioUser.Name)))
                        .ToList();
                });
            }

            return projects ?? throw new InvalidOperationException("The projects cache returned no result.");
        }
        finally
        {
            stopwatch.Stop();
            logger.LogInformation(
                "Projects GET cache {CacheResult}; completed in {ElapsedMilliseconds:F2} ms",
                cacheHit ? "hit" : "miss",
                stopwatch.Elapsed.TotalMilliseconds);
        }
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProjectDetailsResponse>> GetProjectDetails(int id)
    {
        var project = await context.Projects
            .AsNoTracking()
            .Include(project => project.PortfolioUser)
            .ThenInclude(portfolioUser => portfolioUser!.Skills)
            .FirstOrDefaultAsync(project => project.Id == id);

        if (project is null)
        {
            return NotFound();
        }

        var owner = project.PortfolioUser is null
            ? null
            : new ProjectOwnerDetails(
                project.PortfolioUser.Id,
                project.PortfolioUser.Name,
                project.PortfolioUser.Bio,
                project.PortfolioUser.ProfileImageUrl);
        var ownerSkills = project.PortfolioUser?.Skills
            .Select(skill => new ProjectOwnerSkill(skill.Id, skill.Name, skill.Level))
            .ToList() ?? [];

        return new ProjectDetailsResponse(
            project.Id,
            project.Title,
            project.Description,
            project.ImageUrl,
            project.PortfolioUserId,
            owner,
            ownerSkills);
    }

    [HttpGet("enrollments")]
    [Authorize]
    public async Task<ActionResult<List<ProjectEnrollmentSummary>>> GetMyEnrollments()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        return await context.ProjectEnrollments
            .AsNoTracking()
            .Where(enrollment => enrollment.UserId == userId)
            .Select(enrollment => new ProjectEnrollmentSummary(
                enrollment.ProjectId,
                enrollment.Status,
                enrollment.EnrolledAtUtc))
            .ToListAsync();
    }

    [HttpGet("{projectId:int}/enrollments")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<List<ProjectEnrollmentAdminSummary>>> GetProjectEnrollments(int projectId)
    {
        if (!await context.Projects.AnyAsync(project => project.Id == projectId))
        {
            return NotFound();
        }

        return await context.ProjectEnrollments
            .AsNoTracking()
            .Where(enrollment => enrollment.ProjectId == projectId)
            .OrderBy(enrollment => enrollment.EnrolledAtUtc)
            .Select(enrollment => new ProjectEnrollmentAdminSummary(
                enrollment.UserId,
                enrollment.User == null
                    ? "Unknown user"
                    : enrollment.User.Email ?? enrollment.User.UserName ?? "Unknown user",
                enrollment.Status,
                enrollment.EnrolledAtUtc))
            .ToListAsync();
    }

    [HttpPut("{projectId:int}/enrollments/{userId}/status")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateProjectEnrollmentStatus(
        int projectId,
        string userId,
        [FromBody] ProjectEnrollmentStatusUpdateRequest request)
    {
        if (!Enum.IsDefined(typeof(ProjectEnrollmentStatus), request.Status))
        {
            return BadRequest(new { message = "Choose a valid enrollment status." });
        }

        var enrollment = await context.ProjectEnrollments
            .Include(enrollment => enrollment.Project)
            .FirstOrDefaultAsync(enrollment =>
                enrollment.ProjectId == projectId && enrollment.UserId == userId);
        if (enrollment is null)
        {
            return NotFound();
        }

        if (enrollment.Status == request.Status)
        {
            return NoContent();
        }

        var canTransition =
            enrollment.Status == ProjectEnrollmentStatus.Pending &&
            request.Status == ProjectEnrollmentStatus.Active ||
            enrollment.Status == ProjectEnrollmentStatus.Active &&
            request.Status == ProjectEnrollmentStatus.Completed;
        if (!canTransition)
        {
            return Conflict(new { message = "Enrollment status can only move from Pending to Active or Active to Completed." });
        }

        var wasApproved = enrollment.Status == ProjectEnrollmentStatus.Pending &&
            request.Status == ProjectEnrollmentStatus.Active;
        enrollment.Status = request.Status;

        if (wasApproved)
        {
            context.UserNotifications.Add(new UserNotification
            {
                UserId = enrollment.UserId,
                ProjectId = enrollment.ProjectId,
                Message = $"Your enrollment for \"{enrollment.Project?.Title ?? "this project"}\" was approved.",
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        await context.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost("{id:int}/enroll")]
    [Authorize]
    public async Task<IActionResult> EnrollInProject(int id)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        if (!await context.Projects.AnyAsync(project => project.Id == id))
        {
            return NotFound();
        }

        if (await context.ProjectEnrollments.AnyAsync(
                enrollment => enrollment.UserId == userId && enrollment.ProjectId == id))
        {
            return Conflict(new { message = "You are already enrolled in this project." });
        }

        context.ProjectEnrollments.Add(new ProjectEnrollment
        {
            UserId = userId,
            ProjectId = id,
            EnrolledAtUtc = DateTime.UtcNow,
            Status = ProjectEnrollmentStatus.Pending
        });

        try
        {
            await context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            if (await context.ProjectEnrollments.AnyAsync(
                    enrollment => enrollment.UserId == userId && enrollment.ProjectId == id))
            {
                return Conflict(new { message = "You are already enrolled in this project." });
            }

            throw;
        }

        return NoContent();
    }

    [HttpDelete("{id:int}/enroll")]
    [Authorize]
    public async Task<IActionResult> UnenrollFromProject(int id)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var enrollment = await context.ProjectEnrollments
            .FirstOrDefaultAsync(enrollment => enrollment.UserId == userId && enrollment.ProjectId == id);
        if (enrollment is not null)
        {
            context.ProjectEnrollments.Remove(enrollment);
            await context.SaveChangesAsync();
        }

        return NoContent();
    }

    [HttpPost("images")]
    [Authorize(Roles = "Admin")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxProjectImageBytes + 65536)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxProjectImageBytes + 65536)]
    public async Task<ActionResult<ProjectImageUploadResponse>> UploadProjectImage([FromForm] IFormFile? file)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { message = "Choose an image to upload." });
        }

        if (file.Length > MaxProjectImageBytes)
        {
            return StatusCode(StatusCodes.Status413PayloadTooLarge, new { message = "Images must be 5 MB or smaller." });
        }

        if (file.Length < 12)
        {
            return BadRequest(new { message = "The selected file is not a supported image." });
        }

        await using var imageStream = file.OpenReadStream();
        var header = new byte[12];
        await imageStream.ReadExactlyAsync(header, HttpContext.RequestAborted);
        var extension = GetProjectImageExtension(header);
        if (extension is null)
        {
            return BadRequest(new { message = "Upload a PNG, JPEG, or WebP image." });
        }

        imageStream.Position = 0;
        var fileName = $"{Guid.NewGuid():N}{extension}";
        var uploadsDirectory = Path.Combine(
            environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"),
            "uploads");
        Directory.CreateDirectory(uploadsDirectory);

        var imagePath = Path.Combine(uploadsDirectory, fileName);
        await using (var destination = new FileStream(
                         imagePath,
                         FileMode.CreateNew,
                         FileAccess.Write,
                         FileShare.None))
        {
            await imageStream.CopyToAsync(destination, HttpContext.RequestAborted);
        }

        return Ok(new ProjectImageUploadResponse($"/uploads/{fileName}"));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<Project>> CreateProject(Project project)
    {
        context.Projects.Add(project);
        await context.SaveChangesAsync();
        memoryCache.Remove(ProjectsCacheKey);

        return StatusCode(StatusCodes.Status201Created, project);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateProject(int id, Project project)
    {
        var existingProject = await context.Projects.FindAsync(id);
        if (existingProject is null)
        {
            return NotFound();
        }

        existingProject.Title = project.Title;
        existingProject.Description = project.Description;
        existingProject.ImageUrl = project.ImageUrl;
        existingProject.PortfolioUserId = project.PortfolioUserId;

        await context.SaveChangesAsync();
        memoryCache.Remove(ProjectsCacheKey);

        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteProject(int id)
    {
        var project = await context.Projects.FindAsync(id);
        if (project is null)
        {
            return NotFound();
        }

        context.Projects.Remove(project);
        await context.SaveChangesAsync();
        memoryCache.Remove(ProjectsCacheKey);

        return NoContent();
    }

    private static string? GetProjectImageExtension(byte[] header)
    {
        if (header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
            header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A)
        {
            return ".png";
        }

        if (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
        {
            return ".jpg";
        }

        if (header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 &&
            header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50)
        {
            return ".webp";
        }

        return null;
    }
}
