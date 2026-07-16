using MissaoIsrael.Domain;

namespace MissaoIsrael.Application;

public sealed class CampaignService(ICampaignRepository campaigns, IContributionRepository contributions)
{
    public async Task<CampaignPublicDto?> GetPublicAsync(string slug, CancellationToken cancellationToken = default)
    {
        var campaign = await campaigns.GetBySlugAsync(slug, cancellationToken);
        if (campaign is null) return null;

        var totals = await contributions.GetTotalsAsync(campaign.Id, cancellationToken);
        var wall = await contributions.LatestWallAsync(campaign.Id, 10, cancellationToken);
        return ToPublicDto(campaign, totals, wall);
    }

    public async Task<DashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var campaign = await campaigns.GetDefaultAsync(cancellationToken);
        var totals = await contributions.GetTotalsAsync(campaign.Id, cancellationToken);
        var remaining = Math.Max(0, campaign.GoalAmount - totals.RaisedAmount);
        var percent = campaign.GoalAmount <= 0 ? 0 : Math.Round(totals.RaisedAmount / campaign.GoalAmount * 100, 2);
        return new DashboardDto(campaign.GoalAmount, totals.RaisedAmount, remaining, percent, totals.ApprovedCount, totals.PendingCount);
    }

    public Task<Campaign> GetDefaultAsync(CancellationToken cancellationToken = default) => campaigns.GetDefaultAsync(cancellationToken);

    public async Task<Campaign> UpdateAsync(Guid id, Campaign input, CancellationToken cancellationToken = default)
    {
        var campaign = await campaigns.GetByIdAsync(id, cancellationToken) ?? throw new InvalidOperationException("Campanha não encontrada.");
        campaign.Name = Required(input.Name, "Nome");
        campaign.Slug = Required(input.Slug, "Slug").ToLowerInvariant();
        campaign.Title = Required(input.Title, "Título principal");
        campaign.Description = Required(input.Description, "Texto principal");
        campaign.BibleReference = Required(input.BibleReference, "Referência bíblica");
        campaign.BibleText = Required(input.BibleText, "Texto bíblico");
        campaign.HeroImageUrl = Required(input.HeroImageUrl, "Imagem principal");
        campaign.PurposeImageUrl = Required(input.PurposeImageUrl, "Imagem do propósito");
        campaign.PurposeImageUrl = Required(input.PurposeImageUrl, "Imagem do propósito");
        campaign.PurposeHeading = Required(input.PurposeHeading, "Título do propósito");
        campaign.Purpose = Required(input.Purpose, "Texto do propósito");
        campaign.Pillars = input.Pillars.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()).ToList();
        campaign.ContributionHeading = Required(input.ContributionHeading, "Título de contribuição");
        campaign.QuickAmounts = input.QuickAmounts.Where(a => a > 0).Distinct().OrderBy(a => a).ToList();
        campaign.GoalAmount = input.GoalAmount > 0 ? input.GoalAmount : throw new InvalidOperationException("A meta financeira deve ser maior que zero.");
        campaign.PixKey = Required(input.PixKey, "Chave PIX");
        campaign.PixQrCodeUrl = Required(input.PixQrCodeUrl, "QR Code PIX");
        campaign.PastorVideoTitle = Required(input.PastorVideoTitle, "Título do vídeo");
        campaign.PastorVideoSubtitle = Required(input.PastorVideoSubtitle, "Subtítulo do vídeo");
        campaign.VideoUrl = input.VideoUrl.Trim();
        campaign.Status = input.Status;
        campaign.UpdatedAt = DateTimeOffset.UtcNow;
        await campaigns.SaveAsync(campaign, cancellationToken);
        return campaign;
    }

    private static CampaignPublicDto ToPublicDto(Campaign campaign, CampaignTotals totals, IReadOnlyList<Contribution> wall)
    {
        var remaining = Math.Max(0, campaign.GoalAmount - totals.RaisedAmount);
        var percent = campaign.GoalAmount <= 0 ? 0 : Math.Round(totals.RaisedAmount / campaign.GoalAmount * 100, 2);
        return new CampaignPublicDto(
            campaign.Id,
            campaign.Name,
            campaign.Slug,
            campaign.Title,
            campaign.Description,
            campaign.BibleReference,
            campaign.BibleText,
            campaign.HeroImageUrl,
            campaign.PurposeImageUrl,
            campaign.PurposeHeading,
            campaign.Purpose,
            campaign.Pillars,
            campaign.ContributionHeading,
            campaign.QuickAmounts,
            campaign.GoalAmount,
            totals.RaisedAmount,
            remaining,
            percent,
            campaign.PixKey,
            campaign.PixQrCodeUrl,
            campaign.PastorVideoTitle,
            campaign.PastorVideoSubtitle,
            campaign.VideoUrl,
            campaign.Status,
            wall.Select(c => new WallContributionDto(
                c.IsAnonymous ? "Contribuição Anônima" : c.Name ?? "Contribuição",
                c.Amount,
                c.WallMessage,
                string.IsNullOrWhiteSpace(c.WallImagePath) ? null : $"/api/contribution/{c.Id}/wall-image",
                c.ApprovedAt ?? c.UpdatedAt)).ToList());
    }

    private static string Required(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException($"{fieldName} é obrigatório.");
        return value.Trim();
    }
}
