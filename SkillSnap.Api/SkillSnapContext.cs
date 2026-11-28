using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SkillSnap.Api.Models;

namespace SkillSnap.Api;

public class SkillSnapContext(DbContextOptions<SkillSnapContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<PortfolioUser> PortfolioUsers => Set<PortfolioUser>();

    public DbSet<Project> Projects => Set<Project>();

    public DbSet<Skill> Skills => Set<Skill>();

    public DbSet<ProjectEnrollment> ProjectEnrollments => Set<ProjectEnrollment>();

    public DbSet<UserNotification> UserNotifications => Set<UserNotification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<PortfolioUser>()
            .HasMany(portfolioUser => portfolioUser.Projects)
            .WithOne(project => project.PortfolioUser)
            .HasForeignKey(project => project.PortfolioUserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProjectEnrollment>()
            .HasKey(enrollment => new { enrollment.UserId, enrollment.ProjectId });

        modelBuilder.Entity<ProjectEnrollment>()
            .HasOne(enrollment => enrollment.User)
            .WithMany()
            .HasForeignKey(enrollment => enrollment.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProjectEnrollment>()
            .HasOne(enrollment => enrollment.Project)
            .WithMany()
            .HasForeignKey(enrollment => enrollment.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserNotification>()
            .HasOne(notification => notification.User)
            .WithMany()
            .HasForeignKey(notification => notification.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserNotification>()
            .HasOne(notification => notification.Project)
            .WithMany()
            .HasForeignKey(notification => notification.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserNotification>()
            .HasIndex(notification => new { notification.UserId, notification.CreatedAtUtc });
    }
}
