using System.Text.Json;
using MissaoIsrael.Application;
using MissaoIsrael.Domain;

namespace MissaoIsrael.Infrastructure;

public sealed class JsonDataStore(JsonDataStoreOptions options)
{
    private readonly string _path = BuildStorePath(options.DataRootPath);
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private AppState? _state;

    public async Task<AppState> ReadAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            _state ??= await LoadAsync(cancellationToken);
            return Clone(_state);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task WriteAsync(Func<AppState, Task> update, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            _state ??= await LoadAsync(cancellationToken);
            await update(_state);
            await using var stream = File.Create(_path);
            await JsonSerializer.SerializeAsync(stream, _state, _jsonOptions, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<AppState> LoadAsync(CancellationToken cancellationToken)
    {
        if (File.Exists(_path))
        {
            AppState? loaded;
            await using (var stream = File.OpenRead(_path))
            {
                loaded = await JsonSerializer.DeserializeAsync<AppState>(stream, _jsonOptions, cancellationToken);
            }
            if (loaded is not null)
            {
                await EnsureAdminSeedAsync(loaded, cancellationToken);
                return loaded;
            }
        }

        var state = new AppState();
        SeedAdminIfConfigured(state);
        await using var create = File.Create(_path);
        await JsonSerializer.SerializeAsync(create, state, _jsonOptions, cancellationToken);
        return state;
    }

    private AppState Clone(AppState source)
    {
        var json = JsonSerializer.Serialize(source, _jsonOptions);
        return JsonSerializer.Deserialize<AppState>(json, _jsonOptions) ?? new AppState();
    }

    private static string BuildStorePath(string dataRootPath)
    {
        Directory.CreateDirectory(dataRootPath);
        return Path.Combine(dataRootPath, "store.json");
    }

    private async Task EnsureAdminSeedAsync(AppState state, CancellationToken cancellationToken)
    {
        if (!SeedAdminIfConfigured(state)) return;
        await using var stream = File.Create(_path);
        await JsonSerializer.SerializeAsync(stream, state, _jsonOptions, cancellationToken);
    }

    private bool SeedAdminIfConfigured(AppState state)
    {
        if (state.AdminUsers.Count > 0 || string.IsNullOrWhiteSpace(options.AdminPassword)) return false;
        state.AdminUsers.Add(new AdminUser
        {
            Email = options.AdminEmail,
            Name = options.AdminName,
            PasswordHash = AuthService.HashPassword(options.AdminPassword)
        });
        return true;
    }
}

public sealed record JsonDataStoreOptions(string DataRootPath, string AdminEmail, string AdminName, string? AdminPassword);
