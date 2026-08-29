using System.ComponentModel.DataAnnotations;

namespace Brandora.Web.Models.Account;

public class InfluencerRegisterViewModel
{
    [Required]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Select a primary platform.")]
    public string PrimaryPlatform { get; set; } = string.Empty;

    [Required]
    public string PlatformUsername { get; set; } = string.Empty;

    [Required]
    public string ContentNiche { get; set; } = string.Empty;

    [Required]
    public string AudienceSize { get; set; } = string.Empty;

    [Required]
    [Range(1, 500000000, ErrorMessage = "Enter your approximate follower count.")]
    public int Followers { get; set; }

    [Required]
    [Range(0.1, 100, ErrorMessage = "Enter an engagement rate between 0.1 and 100.")]
    public decimal EngagementRate { get; set; }

    [Required]
    public string Location { get; set; } = string.Empty;

    [Required]
    [StringLength(600)]
    public string Bio { get; set; } = string.Empty;

    [StringLength(200)]
    public string? RateNote { get; set; }

    [Required]
    [DataType(DataType.Password)]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
    public string Password { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required]
    [Range(typeof(bool), "true", "true", ErrorMessage = "You must agree to the Terms of Service and Privacy Policy.")]
    public bool AcceptTerms { get; set; }
}
