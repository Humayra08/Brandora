using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Brandora.Web.Models.Settings;

public class BrandSettingsViewModel
{
    [Required]
    [StringLength(150)]
    public string CompanyName { get; set; } = string.Empty;

    [Required]
    [StringLength(150)]
    public string ContactFullName { get; set; } = string.Empty;

    [Url]
    public string? WebsiteUrl { get; set; }

    [Required]
    public string Industry { get; set; } = string.Empty;

    [Required]
    public string MonthlyBudget { get; set; } = string.Empty;

    public IFormFile? ProfilePictureFile { get; set; }
    public bool RemoveProfilePicture { get; set; }

    [ValidateNever]
    public string? ExistingProfilePictureUrl { get; set; }
}
