namespace MissaoIsrael.Domain;

public sealed class AdminUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Administrador";
    public string Email { get; set; } = "admin@envioisrael.local";
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
