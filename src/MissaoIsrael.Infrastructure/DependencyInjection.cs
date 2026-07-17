using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MissaoIsrael.Application;

namespace MissaoIsrael.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string contentRootPath, IConfiguration configuration, bool isDevelopment)
    {
        var dataRootPath = StoragePath.ResolveDataRoot(contentRootPath, configuration);
        var adminPassword = configuration["AdminSeed:Password"];
        if (string.IsNullOrWhiteSpace(adminPassword))
        {
            adminPassword = isDevelopment
                ? "admin123"
                : null;
        }

        services.AddSingleton(new JsonDataStore(new JsonDataStoreOptions(
            dataRootPath,
            configuration["AdminSeed:Email"] ?? "admin@envioisrael.local",
            configuration["AdminSeed:Name"] ?? "Administrador",
            adminPassword)));
        services.AddSingleton<IReceiptStorage>(new LocalReceiptStorage(dataRootPath));
        services.AddSingleton<IWallImageStorage>(new LocalWallImageStorage(dataRootPath));
        services.AddScoped<ICampaignRepository, CampaignRepository>();
        services.AddScoped<IContributionRepository, ContributionRepository>();
        services.AddScoped<IAdminUserRepository, AdminUserRepository>();
        return services;
    }
}

public static class StoragePath
{
    public static string ResolveDataRoot(string contentRootPath, IConfiguration configuration)
    {
        var configured = configuration["Storage:DataRoot"];
        var dataRoot = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(contentRootPath, "App_Data")
            : configured;
        Directory.CreateDirectory(dataRoot);
        return dataRoot;
    }
}
