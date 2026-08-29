using Brandora.Web.Models.Domain;

namespace Brandora.Web.Models.Payments;

public class PaymentListViewModel
{
    public List<Payment> Payments { get; set; } = new();
    public decimal TotalPaid { get; set; }
    public decimal TotalPending { get; set; }
    public int CompletedCount { get; set; }
    public int PendingCount { get; set; }
}
