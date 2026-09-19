namespace Brandora.Web.Models.Domain;

public enum UserRole
{
    Brand,
    Influencer
}

public enum CampaignStatus
{
    Draft,
    Published,
    Active,
    Completed,
    Cancelled
}

public enum ProposalInitiator
{
    Brand,
    Influencer
}

public enum ProposalStatus
{
    Pending,
    Accepted,
    Rejected
}

public enum CollaborationStatus
{
    Active,
    Completed,
    Cancelled
}

public enum MilestoneStatus
{
    Pending,
    Submitted,
    Approved,
    RevisionRequested,
    Paid
}

public enum PaymentStatus
{
    Pending,
    Completed,
    Failed
}

public enum DisputeStatus
{
    Open,
    UnderReview,
    Resolved
}

public enum VerificationStatus
{
    Pending,
    Verified,
    Rejected
}

public enum ContactSubmissionStatus
{
    New,
    InProgress,
    Resolved
}

public enum PaymentMethod
{
    Bkash,
    Nagad,
    BankTransfer
}

public enum EscrowStatus
{
    Held,
    Released,
    Refunded
}

public enum WithdrawalStatus
{
    Pending,
    Approved,
    Rejected,
    Paid
}

public enum PayoutMethodKind
{
    Bkash,
    Nagad,
    BankAccount
}
