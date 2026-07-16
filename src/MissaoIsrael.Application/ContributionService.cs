using MissaoIsrael.Domain;

namespace MissaoIsrael.Application;

public sealed class ContributionService(ICampaignRepository campaigns, IContributionRepository contributions, IReceiptStorage receiptStorage, IWallImageStorage wallImageStorage)
{
    private static readonly HashSet<string> AllowedExtensions = [".jpg", ".jpeg", ".png", ".pdf"];
    private static readonly HashSet<string> AllowedWallImageExtensions = [".jpg", ".jpeg", ".png", ".webp"];
    private const long MaxReceiptSize = 10 * 1024 * 1024;
    private const long MaxWallImageSize = 3 * 1024 * 1024;
    private const int MaxWallMessageLength = 180;

    public async Task<Contribution> RegisterAsync(RegisterContributionRequest request, Stream receipt, string receiptName, string contentType, WallImageUpload? wallImage = null, CancellationToken cancellationToken = default)
    {
        var campaign = await campaigns.GetByIdAsync(request.CampaignId, cancellationToken) ?? throw new InvalidOperationException("Campanha não encontrada.");
        if (campaign.Status != CampaignStatus.Ativa) throw new InvalidOperationException("Esta campanha não está aberta para novas contribuições.");

        Validate(request, receiptName, receipt.Length, wallImage);
        var stored = await receiptStorage.SaveAsync(receipt, receiptName, contentType, cancellationToken);
        StoredWallImage? storedWallImage = null;
        if (request.ShowOnWall && wallImage is not null)
        {
            storedWallImage = await wallImageStorage.SaveAsync(wallImage.Stream, wallImage.FileName, wallImage.ContentType, cancellationToken);
        }

        var contribution = new Contribution
        {
            CampaignId = campaign.Id,
            Name = request.IsAnonymous ? null : request.Name?.Trim(),
            Phone = request.Phone.Trim(),
            Amount = request.Amount,
            ReceiptPath = stored.Path,
            ReceiptOriginalName = stored.OriginalName,
            WallMessage = request.ShowOnWall ? NormalizeWallMessage(request.WallMessage) : null,
            WallImagePath = storedWallImage?.Path,
            WallImageOriginalName = storedWallImage?.OriginalName,
            IsAnonymous = request.IsAnonymous,
            ShowOnWall = request.ShowOnWall,
            Status = ContributionStatus.Pendente
        };
        await contributions.AddAsync(contribution, cancellationToken);
        return contribution;
    }

    public async Task<IReadOnlyList<ContributionAdminDto>> ListAdminAsync(ContributionStatus? status, string? search, CancellationToken cancellationToken = default)
    {
        var items = await contributions.ListAsync(status, search, cancellationToken);
        return items.Select(ToAdminDto).ToList();
    }

    public async Task<ContributionAdminDto?> GetAdminAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var item = await contributions.GetByIdAsync(id, cancellationToken);
        return item is null ? null : ToAdminDto(item);
    }

    public async Task<Contribution> ApproveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var item = await contributions.GetByIdAsync(id, cancellationToken) ?? throw new InvalidOperationException("Contribuição não encontrada.");
        item.Status = ContributionStatus.Aprovada;
        item.ApprovedAt = DateTimeOffset.UtcNow;
        item.UpdatedAt = DateTimeOffset.UtcNow;
        item.RejectionReason = null;
        await contributions.SaveAsync(item, cancellationToken);
        return item;
    }

    public async Task<Contribution> RejectAsync(Guid id, string? reason, CancellationToken cancellationToken = default)
    {
        var item = await contributions.GetByIdAsync(id, cancellationToken) ?? throw new InvalidOperationException("Contribuição não encontrada.");
        item.Status = ContributionStatus.Rejeitada;
        item.RejectionReason = reason;
        item.UpdatedAt = DateTimeOffset.UtcNow;
        await contributions.SaveAsync(item, cancellationToken);
        return item;
    }

    public async Task<(Stream Stream, string FileName)> OpenReceiptAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var item = await contributions.GetByIdAsync(id, cancellationToken) ?? throw new InvalidOperationException("Contribuição não encontrada.");
        return (await receiptStorage.OpenReadAsync(item.ReceiptPath, cancellationToken), item.ReceiptOriginalName);
    }

    public async Task<(Stream Stream, string FileName)> OpenWallImageAsync(Guid id, bool publicOnly, CancellationToken cancellationToken = default)
    {
        var item = await contributions.GetByIdAsync(id, cancellationToken) ?? throw new InvalidOperationException("Contribuição não encontrada.");
        if (string.IsNullOrWhiteSpace(item.WallImagePath)) throw new InvalidOperationException("Imagem não encontrada.");
        if (publicOnly && (item.Status != ContributionStatus.Aprovada || !item.ShowOnWall))
            throw new InvalidOperationException("Imagem ainda não publicada.");
        return (await wallImageStorage.OpenReadAsync(item.WallImagePath, cancellationToken), item.WallImageOriginalName ?? "mural.jpg");
    }

    private static void Validate(RegisterContributionRequest request, string receiptName, long receiptSize, WallImageUpload? wallImage)
    {
        if (request.Amount <= 0) throw new InvalidOperationException("Informe um valor de contribuição válido.");
        if (string.IsNullOrWhiteSpace(request.Phone)) throw new InvalidOperationException("Informe o WhatsApp.");
        if (!request.IsAnonymous && string.IsNullOrWhiteSpace(request.Name)) throw new InvalidOperationException("Informe o nome ou marque contribuição anônima.");
        if (receiptSize <= 0 || receiptSize > MaxReceiptSize) throw new InvalidOperationException("O comprovante deve ter até 10 MB.");
        if (!AllowedExtensions.Contains(Path.GetExtension(receiptName).ToLowerInvariant())) throw new InvalidOperationException("Formato de comprovante inválido.");
        var message = NormalizeWallMessage(request.WallMessage);
        if (message?.Length > MaxWallMessageLength) throw new InvalidOperationException($"A mensagem do mural deve ter até {MaxWallMessageLength} caracteres.");
        if (message is not null && ContainsUnsafeMessageContent(message)) throw new InvalidOperationException("A mensagem do mural não deve conter links ou dados sensíveis.");
        if (!request.ShowOnWall && (message is not null || wallImage is not null)) throw new InvalidOperationException("Marque a opção de aparecer no mural para enviar mensagem ou foto.");
        if (wallImage is null) return;
        if (wallImage.Stream.Length <= 0 || wallImage.Stream.Length > MaxWallImageSize) throw new InvalidOperationException("A foto do mural deve ter até 3 MB.");
        if (!AllowedWallImageExtensions.Contains(Path.GetExtension(wallImage.FileName).ToLowerInvariant())) throw new InvalidOperationException("Formato de foto inválido.");
    }

    private static string? NormalizeWallMessage(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool ContainsUnsafeMessageContent(string value) =>
        value.Contains("http://", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("https://", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("www.", StringComparison.OrdinalIgnoreCase) ||
        value.Contains('@');

    private static ContributionAdminDto ToAdminDto(Contribution c) =>
        new(c.Id, c.Name, c.Phone, c.Amount, c.IsAnonymous, c.ShowOnWall, c.Status, c.RejectionReason, c.ReceiptOriginalName, c.WallMessage, c.WallImageOriginalName, c.CreatedAt, c.ApprovedAt);
}

public sealed record RegisterContributionRequest(Guid CampaignId, string? Name, string Phone, decimal Amount, bool IsAnonymous, bool ShowOnWall, string? WallMessage);
public sealed record WallImageUpload(Stream Stream, string FileName, string ContentType);
