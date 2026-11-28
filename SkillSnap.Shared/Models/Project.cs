using System.ComponentModel.DataAnnotations;

namespace SkillSnap.Shared.Models;

public class Project
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [StringLength(1000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public string ImageUrl { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int PortfolioUserId { get; set; }
}
