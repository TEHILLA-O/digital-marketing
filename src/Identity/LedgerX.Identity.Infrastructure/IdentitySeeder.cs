using LedgerX.Contracts.Demo;
using LedgerX.Eventing.Persistence;
using LedgerX.Identity.Domain;
using LedgerX.SharedKernel.Primitives;
using LedgerX.SharedKernel.Security;
using LedgerX.SharedKernel.Time;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LedgerX.Identity.Infrastructure;

public sealed class IdentitySeeder(
    IdentityDbContext db,
    UserManager<ApplicationUser> users,
    RoleManager<IdentityRole<Guid>> roles,
    IConfiguration configuration,
    IClock clock,
    ILogger<IdentitySeeder> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        await db.MigrateOrCreateAsync(cancellationToken).ConfigureAwait(false);

        foreach (var role in Roles.All)
        {
            if (!await roles.RoleExistsAsync(role).ConfigureAwait(false))
            {
                await roles.CreateAsync(new IdentityRole<Guid>(role) { Id = Guid.NewGuid() }).ConfigureAwait(false);
            }
        }

        var password = configuration["LedgerX:Seed:DemoPassword"]
                       ?? Environment.GetEnvironmentVariable("LEDGERX_DEMO_PASSWORD")
                       ?? "LedgerX-Demo-2026!";

        await EnsureUserAsync("demo.customer@ledgerx.local", "Alice", "Thompson", [Roles.Customer], password, DemoIds.AliceUser, DemoIds.AliceCustomer, cancellationToken).ConfigureAwait(false);
        await EnsureUserAsync("bob.customer@ledgerx.local", "Bob", "Nguyen", [Roles.Customer], password, DemoIds.BobUser, DemoIds.BobCustomer, cancellationToken).ConfigureAwait(false);
        await EnsureUserAsync("carol.customer@ledgerx.local", "Carol", "Okoye", [Roles.Customer], password, DemoIds.CarolUser, DemoIds.CarolCustomer, cancellationToken).ConfigureAwait(false);
        await EnsureUserAsync("admin@ledgerx.local", "Amina", "Reid", [Roles.Administrator], password, DemoIds.AdminUser, null, cancellationToken).ConfigureAwait(false);
        await EnsureUserAsync("auditor@ledgerx.local", "Eli", "Marsh", [Roles.Auditor], password, DemoIds.AuditorUser, null, cancellationToken).ConfigureAwait(false);
        await EnsureUserAsync("finance@ledgerx.local", "Priya", "Shah", [Roles.FinanceOperator], password, DemoIds.FinanceUser, null, cancellationToken).ConfigureAwait(false);
        await EnsureUserAsync("support@ledgerx.local", "Noah", "Patel", [Roles.SupportAgent], password, DemoIds.SupportUser, null, cancellationToken).ConfigureAwait(false);

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Identity seed completed. Demo password is read from LedgerX:Seed:DemoPassword / LEDGERX_DEMO_PASSWORD.");
    }

    private async Task EnsureUserAsync(
        string email,
        string first,
        string last,
        IReadOnlyList<string> userRoles,
        string password,
        Guid userId,
        Guid? customerId,
        CancellationToken cancellationToken)
    {
        var user = await users.FindByEmailAsync(email).ConfigureAwait(false);
        if (user is null)
        {
            user = new ApplicationUser
            {
                Id = userId,
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FirstName = first,
                LastName = last
            };

            var result = await users.CreateAsync(user, password).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"Failed to seed {email}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }
        }

        foreach (var role in userRoles)
        {
            if (!await users.IsInRoleAsync(user, role).ConfigureAwait(false))
            {
                await users.AddToRoleAsync(user, role).ConfigureAwait(false);
            }
        }

        if (userRoles.Contains(Roles.Customer) && user.CustomerId is null)
        {
            var existing = await db.Customers.FirstOrDefaultAsync(c => c.Email == email, cancellationToken).ConfigureAwait(false);
            if (existing is null)
            {
                var customer = Customer.Register(
                    UserId.From(user.Id),
                    email,
                    first,
                    last,
                    clock,
                    customerId is { } cid ? CustomerId.From(cid) : null);
                db.Customers.Add(customer);
                user.CustomerId = customer.Id.Value;
                await users.UpdateAsync(user).ConfigureAwait(false);
            }
            else
            {
                user.CustomerId = existing.Id.Value;
                await users.UpdateAsync(user).ConfigureAwait(false);
            }
        }
    }
}
