using MissaoIsrael.Application;

namespace MissaoIsrael.Infrastructure;

public sealed class LocalWallImageStorage : IWallImageStorage
{
    private readonly string _root;

    public LocalWallImageStorage(string dataRootPath)
    {
        _root = Path.Combine(dataRootPath, "wall-images");
        Directory.CreateDirectory(_root);
    }

    public async Task<StoredWallImage> SaveAsync(Stream stream, string fileName, string contentType, CancellationToken cancellationToken = default)
    {
        var safeName = $"{Guid.NewGuid():N}{Path.GetExtension(fileName).ToLowerInvariant()}";
        var path = Path.Combine(_root, safeName);
        await using var output = File.Create(path);
        if (stream.CanSeek) stream.Position = 0;
        await stream.CopyToAsync(output, cancellationToken);
        return new StoredWallImage(safeName, fileName);
    }

    public Task<Stream> OpenReadAsync(string imagePath, CancellationToken cancellationToken = default)
    {
        var root = Path.GetFullPath(_root);
        var path = Path.GetFullPath(Path.Combine(root, imagePath));
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Caminho inválido.");
        return Task.FromResult<Stream>(File.OpenRead(path));
    }
}
