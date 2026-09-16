namespace Brandora.Web.Models.Domain;

public class PasswordResetCode
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    public string CodeHash { get; set; } = string.Empty;
    public int Attempts { get; set; }
    public bool Used { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
}
