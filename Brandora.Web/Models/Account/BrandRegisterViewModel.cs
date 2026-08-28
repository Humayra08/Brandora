using System.ComponentModel.DataAnnotations;

namespace Brandora.Web.Models.Account;

public class BrandRegisterViewModel
{
    [Required]
    public string CompanyName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string ContactFullName { get; set; } = string.Empty;

    [Url]
    public string? WebsiteUrl { get; set; }

    [Required]
    public string Industry { get; set; } = string.Empty;

    [Required]
    public string MonthlyBudget { get; set; } = string.Empty;

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
