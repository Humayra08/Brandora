namespace Brandora.Web.Models.Domain;

public class AdminAuditLog
{
    public int Id { get; set; }

    public string AdminName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? Details { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
