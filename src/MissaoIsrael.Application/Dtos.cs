using MissaoIsrael.Domain;

namespace MissaoIsrael.Application;

public sealed record CampaignPublicDto(
    Guid Id,
    string Name,
    string Slug,
    string Title,
    string Description,
    string BibleReference,
    string BibleText,
    string HeroImageUrl,
    string PurposeImageUrl,
    string PurposeHeading,
    string Purpose,
    IReadOnlyList<string> Pillars,
    string ContributionHeading,
    IReadOnlyList<decimal> QuickAmounts,
    decimal GoalAmount,
    decimal RaisedAmount,
    decimal RemainingAmount,
    decimal Percent,
    string PixKey,
    string PixQrCodeUrl,
    string PastorVideoTitle,
    string PastorVideoSubtitle,
    string VideoUrl,
    CampaignStatus Status,
    IReadOnlyList<WallContributionDto> Wall);

public sealed record WallContributionDto(string DisplayName, decimal Amount, string? Message, string? ImageUrl, DateTimeOffset ApprovedAt);

public sealed record DashboardDto(
    decimal GoalAmount,
    decimal RaisedAmount,
    decimal RemainingAmount,
    decimal Percent,
    int ApprovedContributions,
    int PendingContributions);

public sealed record ContributionAdminDto(
    Guid Id,
    string? Name,
    string Phone,
    decimal Amount,
    bool IsAnonymous,
    bool ShowOnWall,
    ContributionStatus Status,
    string? RejectionReason,
    string ReceiptOriginalName,
    string? WallMessage,
    string? WallImageOriginalName,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ApprovedAt);
