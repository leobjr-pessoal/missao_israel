using MissaoIsrael.Application;
using MissaoIsrael.Domain;

namespace MissaoIsrael.Infrastructure;

public sealed class CampaignRepository(JsonDataStore store) : ICampaignRepository
{
    public async Task<Campaign> GetDefaultAsync(CancellationToken cancellationToken = default) =>
        (await store.ReadAsync(cancellationToken)).Campaign;

    public async Task<Campaign?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        var state = await store.ReadAsync(cancellationToken);
        return state.Campaign.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase) ? state.Campaign : null;
    }

    public async Task<Campaign?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var state = await store.ReadAsync(cancellationToken);
        return state.Campaign.Id == id ? state.Campaign : null;
    }

    public Task SaveAsync(Campaign campaign, CancellationToken cancellationToken = default) =>
        store.WriteAsync(state =>
        {
            state.Campaign = campaign;
            return Task.CompletedTask;
        }, cancellationToken);
}

public sealed class ContributionRepository(JsonDataStore store) : IContributionRepository
{
    public Task AddAsync(Contribution contribution, CancellationToken cancellationToken = default) =>
        store.WriteAsync(state =>
        {
            state.Contributions.Add(contribution);
            return Task.CompletedTask;
        }, cancellationToken);

    public async Task<Contribution?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        (await store.ReadAsync(cancellationToken)).Contributions.FirstOrDefault(c => c.Id == id);

    public async Task<IReadOnlyList<Contribution>> ListAsync(ContributionStatus? status = null, string? search = null, CancellationToken cancellationToken = default)
    {
        var query = (await store.ReadAsync(cancellationToken)).Contributions.AsEnumerable();
        if (status is not null) query = query.Where(c => c.Status == status);
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(c =>
                (c.Name?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                c.Phone.Contains(search, StringComparison.OrdinalIgnoreCase));
        }
        return query.OrderByDescending(c => c.CreatedAt).ToList();
    }

    public async Task<IReadOnlyList<Contribution>> LatestWallAsync(Guid campaignId, int take, CancellationToken cancellationToken = default) =>
        (await store.ReadAsync(cancellationToken)).Contributions
            .Where(c => c.CampaignId == campaignId && c.Status == ContributionStatus.Aprovada && c.ShowOnWall)
            .OrderByDescending(c => c.ApprovedAt ?? c.UpdatedAt)
            .Take(take)
            .ToList();

    public Task SaveAsync(Contribution contribution, CancellationToken cancellationToken = default) =>
        store.WriteAsync(state =>
        {
            var index = state.Contributions.FindIndex(c => c.Id == contribution.Id);
            if (index >= 0) state.Contributions[index] = contribution;
            return Task.CompletedTask;
        }, cancellationToken);

    public async Task<CampaignTotals> GetTotalsAsync(Guid campaignId, CancellationToken cancellationToken = default)
    {
        var items = (await store.ReadAsync(cancellationToken)).Contributions.Where(c => c.CampaignId == campaignId).ToList();
        return new CampaignTotals(
            items.Where(c => c.Status == ContributionStatus.Aprovada).Sum(c => c.Amount),
            items.Count(c => c.Status == ContributionStatus.Aprovada),
            items.Count(c => c.Status == ContributionStatus.Pendente));
    }
}

public sealed class AdminUserRepository(JsonDataStore store) : IAdminUserRepository
{
    public async Task<AdminUser?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        (await store.ReadAsync(cancellationToken)).AdminUsers.FirstOrDefault(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
}
