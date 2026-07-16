using MissaoIsrael.Domain;

namespace MissaoIsrael.Application;

public interface ICampaignRepository
{
    Task<Campaign> GetDefaultAsync(CancellationToken cancellationToken = default);
    Task<Campaign?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);
    Task<Campaign?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task SaveAsync(Campaign campaign, CancellationToken cancellationToken = default);
}

public interface IContributionRepository
{
    Task AddAsync(Contribution contribution, CancellationToken cancellationToken = default);
    Task<Contribution?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Contribution>> ListAsync(ContributionStatus? status = null, string? search = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Contribution>> LatestWallAsync(Guid campaignId, int take, CancellationToken cancellationToken = default);
    Task SaveAsync(Contribution contribution, CancellationToken cancellationToken = default);
    Task<CampaignTotals> GetTotalsAsync(Guid campaignId, CancellationToken cancellationToken = default);
}

public interface IAdminUserRepository
{
    Task<AdminUser?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
}

public interface IReceiptStorage
{
    Task<StoredReceipt> SaveAsync(Stream stream, string fileName, string contentType, CancellationToken cancellationToken = default);
    Task<Stream> OpenReadAsync(string receiptPath, CancellationToken cancellationToken = default);
}

public interface IWallImageStorage
{
    Task<StoredWallImage> SaveAsync(Stream stream, string fileName, string contentType, CancellationToken cancellationToken = default);
    Task<Stream> OpenReadAsync(string imagePath, CancellationToken cancellationToken = default);
}

public sealed record CampaignTotals(decimal RaisedAmount, int ApprovedCount, int PendingCount);
public sealed record StoredReceipt(string Path, string OriginalName);
public sealed record StoredWallImage(string Path, string OriginalName);
