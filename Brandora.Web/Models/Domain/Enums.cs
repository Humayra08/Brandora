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
