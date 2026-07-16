namespace MissaoIsrael.Domain;

public sealed class Contribution
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CampaignId { get; set; }
    public string? Name { get; set; }
    public string Phone { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string ReceiptPath { get; set; } = string.Empty;
    public string ReceiptOriginalName { get; set; } = string.Empty;
    public string? WallMessage { get; set; }
    public string? WallImagePath { get; set; }
    public string? WallImageOriginalName { get; set; }
    public bool IsAnonymous { get; set; }
    public bool ShowOnWall { get; set; }
    public ContributionStatus Status { get; set; } = ContributionStatus.Pendente;
    public string? RejectionReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ApprovedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
