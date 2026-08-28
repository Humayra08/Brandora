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
