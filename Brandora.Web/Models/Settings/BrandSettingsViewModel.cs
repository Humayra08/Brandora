using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Brandora.Web.Models.Domain;

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

    [ValidateNever]
    public NotificationPreferencesFormViewModel NotificationPreferences { get; set; } = new();

    [ValidateNever]
    public decimal TotalFunded { get; set; }

    [ValidateNever]
    public decimal PendingPayments { get; set; }

    [ValidateNever]
    public decimal ReleasedPayments { get; set; }

    [ValidateNever]
    public decimal CampaignSpend { get; set; }

    // Read-only account facts shown in the Settings rail.
    [ValidateNever]
    public VerificationStatus VerificationStatus { get; set; }

    [ValidateNever]
    public DateTime? VerifiedAt { get; set; }

    [ValidateNever]
    public DateTime MemberSince { get; set; }

    // Current social profile links, keyed by SocialPlatform.Key ("facebook", "instagram", ...).
    // Saved through their own form (SettingsController.UpdateSocialLinks), not the profile form.
    [ValidateNever]
    public Dictionary<string, string?> SocialLinks { get; set; } = new();
}
