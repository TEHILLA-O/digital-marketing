using Microsoft.AspNetCore.Identity;

namespace LedgerX.Identity.Infrastructure;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public Guid? CustomerId { get; set; }
}
