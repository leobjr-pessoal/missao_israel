using MissaoIsrael.Domain;

namespace MissaoIsrael.Infrastructure;

public sealed class AppState
{
    public Campaign Campaign { get; set; } = new();
    public List<Contribution> Contributions { get; set; } = [];
    public List<AdminUser> AdminUsers { get; set; } = [];
}
