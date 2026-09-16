using System.ComponentModel.DataAnnotations;

namespace Brandora.Web.Models.Account;

public class VerifyEmailViewModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter the 6-digit code we sent to your email.")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "The code must be 6 digits.")]
    public string Code { get; set; } = string.Empty;
}
