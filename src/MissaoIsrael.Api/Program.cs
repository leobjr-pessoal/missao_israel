using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Net.Http.Headers;
using MissaoIsrael.Application;
using MissaoIsrael.Domain;
using MissaoIsrael.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 220 * 1024 * 1024;
});

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = 220 * 1024 * 1024);
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});
builder.Services.AddScoped<CampaignService>();
builder.Services.AddScoped<ContributionService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddInfrastructure(builder.Environment.ContentRootPath, builder.Configuration, builder.Environment.IsDevelopment());
builder.Services.AddSingleton<TokenService>();

var app = builder.Build();

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    await next();
});

app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        if (ctx.Context.Request.Path.StartsWithSegments("/assets"))
        {
            ctx.Context.Response.Headers[HeaderNames.CacheControl] = "public,max-age=604800";
        }
    }
});

app.MapGet("/api/health", () => Results.Ok(new { status = "ok", at = DateTimeOffset.UtcNow }));

app.MapGet("/api/campaign/{slug}", async (string slug, CampaignService service, CancellationToken ct) =>
{
    var campaign = await service.GetPublicAsync(slug, ct);
    return campaign is null ? Results.NotFound() : Results.Ok(campaign);
});

app.MapGet("/api/campaign-current", async (CampaignService service, CancellationToken ct) =>
{
    var campaign = await service.GetDefaultAsync(ct);
    return Results.Redirect($"/api/campaign/{campaign.Slug}");
});

app.MapPost("/api/contribution", async (HttpRequest request, ContributionService service, CancellationToken ct) =>
{
    if (!request.HasFormContentType) return Results.BadRequest(new { message = "Envie os dados como multipart/form-data." });
    var form = await request.ReadFormAsync(ct);
    var file = form.Files["receipt"];
    var wallImageFile = form.Files["wallImage"];
    if (file is null) return Results.BadRequest(new { message = "Envie o comprovante." });

    try
    {
        if (!Guid.TryParse(form["campaignId"], out var campaignId))
            return Results.BadRequest(new { message = "Campanha inválida." });
        if (!decimal.TryParse(form["amount"], NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) || amount <= 0)
            return Results.BadRequest(new { message = "Informe um valor de contribuição válido." });

        var data = new RegisterContributionRequest(
            campaignId,
            form["name"],
            form["phone"]!,
            amount,
            bool.TryParse(form["isAnonymous"], out var anonymous) && anonymous,
            bool.TryParse(form["showOnWall"], out var show) && show,
            form["wallMessage"]);

        await using var stream = file.OpenReadStream();
        await using var wallImageStream = wallImageFile?.OpenReadStream();
        var wallImage = wallImageFile is null || wallImageStream is null
            ? null
            : new WallImageUpload(wallImageStream, wallImageFile.FileName, wallImageFile.ContentType);
        var contribution = await service.RegisterAsync(data, stream, file.FileName, file.ContentType, wallImage, ct);
        return Results.Created($"/api/contribution/{contribution.Id}", new { contribution.Id, contribution.Status });
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
});

app.MapGet("/api/contribution/{id:guid}/wall-image", async (Guid id, ContributionService service, CancellationToken ct) =>
{
    try
    {
        var image = await service.OpenWallImageAsync(id, publicOnly: true, ct);
        return Results.File(image.Stream, "application/octet-stream", image.FileName);
    }
    catch (InvalidOperationException ex) { return Results.BadRequest(new { message = ex.Message }); }
});

app.MapPost("/api/admin/auth/login", async (LoginRequest login, AuthService auth, TokenService tokens, CancellationToken ct) =>
{
    var user = await auth.ValidateAsync(login.Email, login.Password, ct);
    return user is null ? Results.Unauthorized() : Results.Ok(new { token = tokens.Create(user.Email, user.Name), user.Name, user.Email });
});

var admin = app.MapGroup("/api/admin").AddEndpointFilter(AdminAuthFilter);

admin.MapGet("/dashboard", async (CampaignService service, CancellationToken ct) => Results.Ok(await service.GetDashboardAsync(ct)));
admin.MapGet("/campaign", async (CampaignService service, CancellationToken ct) => Results.Ok(await service.GetDefaultAsync(ct)));
admin.MapPut("/campaign/{id:guid}", async (Guid id, Campaign campaign, CampaignService service, CancellationToken ct) =>
{
    try { return Results.Ok(await service.UpdateAsync(id, campaign, ct)); }
    catch (InvalidOperationException ex) { return Results.BadRequest(new { message = ex.Message }); }
});

admin.MapPost("/campaign/{id:guid}/asset", async (Guid id, HttpRequest request, ICampaignRepository campaigns, IConfiguration configuration, IWebHostEnvironment environment, CancellationToken ct) =>
{
    if (!request.HasFormContentType) return Results.BadRequest(new { message = "Envie o arquivo como multipart/form-data." });
    var campaign = await campaigns.GetByIdAsync(id, ct);
    if (campaign is null) return Results.NotFound();

    var form = await request.ReadFormAsync(ct);
    var kind = form["kind"].ToString();
    var file = form.Files["file"];
    if (file is null || file.Length == 0) return Results.BadRequest(new { message = "Envie um arquivo." });
    var isVideo = kind.Equals("video", StringComparison.OrdinalIgnoreCase);
    if (!kind.Equals("hero", StringComparison.OrdinalIgnoreCase) && !kind.Equals("pix", StringComparison.OrdinalIgnoreCase) && !isVideo)
        return Results.BadRequest(new { message = "Tipo de arquivo inválido." });
    var maxSize = isVideo ? 200 * 1024 * 1024 : 5 * 1024 * 1024;
    if (file.Length > maxSize) return Results.BadRequest(new { message = isVideo ? "O vídeo deve ter até 200 MB." : "A imagem deve ter até 5 MB." });

    var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
    var allowedImages = new HashSet<string> { ".jpg", ".jpeg", ".png", ".webp" };
    var allowedVideos = new HashSet<string> { ".mp4", ".webm", ".mov", ".m4v" };
    if (!isVideo && !allowedImages.Contains(extension)) return Results.BadRequest(new { message = "Formato de imagem inválido." });
    if (isVideo && !allowedVideos.Contains(extension)) return Results.BadRequest(new { message = "Formato de vídeo inválido." });

    var uploadDir = Path.Combine(StoragePath.ResolveDataRoot(environment.ContentRootPath, configuration), "campaign-assets");
    Directory.CreateDirectory(uploadDir);
    var fileName = $"{kind}-{Guid.NewGuid():N}{extension}";
    var fullPath = Path.Combine(uploadDir, fileName);
    await using (var output = File.Create(fullPath))
    await using (var input = file.OpenReadStream())
    {
        await input.CopyToAsync(output, ct);
    }

    var publicUrl = $"/uploads/campaign/{fileName}";
    if (kind.Equals("hero", StringComparison.OrdinalIgnoreCase)) campaign.HeroImageUrl = publicUrl;
    else if (kind.Equals("pix", StringComparison.OrdinalIgnoreCase)) campaign.PixQrCodeUrl = publicUrl;
    else campaign.VideoUrl = publicUrl;

    campaign.UpdatedAt = DateTimeOffset.UtcNow;
    await campaigns.SaveAsync(campaign, ct);
    return Results.Ok(new { url = publicUrl });
});

app.MapGet("/uploads/campaign/{fileName}", (string fileName, IConfiguration configuration, IWebHostEnvironment environment) =>
{
    if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return Results.BadRequest();
    var root = Path.GetFullPath(Path.Combine(StoragePath.ResolveDataRoot(environment.ContentRootPath, configuration), "campaign-assets"));
    var path = Path.GetFullPath(Path.Combine(root, fileName));
    if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(path)) return Results.NotFound();

    var contentType = Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".webp" => "image/webp",
        ".mp4" or ".m4v" => "video/mp4",
        ".webm" => "video/webm",
        ".mov" => "video/quicktime",
        _ => "application/octet-stream"
    };
    return Results.File(File.OpenRead(path), contentType, enableRangeProcessing: true);
});

admin.MapGet("/contribution", async (string? status, string? search, ContributionService service, CancellationToken ct) =>
{
    ContributionStatus? parsed = Enum.TryParse<ContributionStatus>(status, true, out var s) ? s : null;
    return Results.Ok(await service.ListAdminAsync(parsed, search, ct));
});

admin.MapGet("/contribution/{id:guid}", async (Guid id, ContributionService service, CancellationToken ct) =>
{
    var item = await service.GetAdminAsync(id, ct);
    return item is null ? Results.NotFound() : Results.Ok(item);
});

admin.MapPut("/contribution/{id:guid}/approve", async (Guid id, ContributionService service, CancellationToken ct) =>
{
    try { return Results.Ok(await service.ApproveAsync(id, ct)); }
    catch (InvalidOperationException ex) { return Results.BadRequest(new { message = ex.Message }); }
});

admin.MapPut("/contribution/{id:guid}/reject", async (Guid id, RejectRequest request, ContributionService service, CancellationToken ct) =>
{
    try { return Results.Ok(await service.RejectAsync(id, request.Reason, ct)); }
    catch (InvalidOperationException ex) { return Results.BadRequest(new { message = ex.Message }); }
});

admin.MapGet("/contribution/{id:guid}/receipt", async (Guid id, ContributionService service, CancellationToken ct) =>
{
    try
    {
        var receipt = await service.OpenReceiptAsync(id, ct);
        return Results.File(receipt.Stream, "application/octet-stream", receipt.FileName);
    }
    catch (InvalidOperationException ex) { return Results.BadRequest(new { message = ex.Message }); }
});

admin.MapGet("/contribution/{id:guid}/wall-image", async (Guid id, ContributionService service, CancellationToken ct) =>
{
    try
    {
        var image = await service.OpenWallImageAsync(id, publicOnly: false, ct);
        return Results.File(image.Stream, "application/octet-stream", image.FileName);
    }
    catch (InvalidOperationException ex) { return Results.BadRequest(new { message = ex.Message }); }
});

app.MapFallbackToFile("index.html");

app.Run();

static async ValueTask<object?> AdminAuthFilter(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
{
    var http = context.HttpContext;
    var tokenService = http.RequestServices.GetRequiredService<TokenService>();
    var header = http.Request.Headers.Authorization.ToString();
    var token = header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
        ? header["Bearer ".Length..]
        : http.Request.Query["token"].ToString();
    if (string.IsNullOrWhiteSpace(token) || !tokenService.Validate(token, out var principal))
        return Results.Unauthorized();
    http.User = principal;
    return await next(context);
}

public sealed record LoginRequest(string Email, string Password);
public sealed record RejectRequest(string? Reason);

public sealed class TokenService(IConfiguration configuration, IWebHostEnvironment environment)
{
    private readonly string _issuer = configuration["AdminAuth:Issuer"] ?? "MissaoIsrael";
    private readonly string _secret = GetRequiredSecret(configuration, environment);

    public string Create(string email, string name)
    {
        var expires = DateTimeOffset.UtcNow.AddHours(8).ToUnixTimeSeconds();
        var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{email}|{name}|{expires}|{_issuer}"));
        var signature = Sign(payload);
        return $"{payload}.{signature}";
    }

    public bool Validate(string token, out ClaimsPrincipal principal)
    {
        principal = new ClaimsPrincipal(new ClaimsIdentity());
        var parts = token.Split('.');
        if (parts.Length != 2 || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(Sign(parts[0])), Encoding.UTF8.GetBytes(parts[1])))
            return false;
        var payload = Encoding.UTF8.GetString(Convert.FromBase64String(parts[0])).Split('|');
        if (payload.Length != 4 || payload[3] != _issuer || !long.TryParse(payload[2], out var exp) || DateTimeOffset.UtcNow.ToUnixTimeSeconds() > exp)
            return false;
        principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Email, payload[0]), new Claim(ClaimTypes.Name, payload[1])], "Bearer"));
        return true;
    }

    private string Sign(string payload)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_secret));
        return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));
    }

    private static string GetRequiredSecret(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var secret = configuration["AdminAuth:Secret"];
        if (environment.IsDevelopment() && string.IsNullOrWhiteSpace(secret))
            return "dev-secret-change-me-before-production-32chars";
        if (string.IsNullOrWhiteSpace(secret) || secret is "dev-secret-change-me-before-production" or "__set_by_environment__")
            throw new InvalidOperationException("Configure AdminAuth:Secret com um valor forte antes de iniciar a aplicação.");
        if (secret.Length < 32)
            throw new InvalidOperationException("AdminAuth:Secret deve ter pelo menos 32 caracteres.");
        return secret;
    }
}
