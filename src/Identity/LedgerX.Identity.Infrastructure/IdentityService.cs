using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

using LedgerX.Contracts.Api;
using LedgerX.Contracts.Events;
using LedgerX.Eventing.Outbox;
using LedgerX.Identity.Application;
using LedgerX.Identity.Domain;
using LedgerX.ServiceDefaults;
using LedgerX.SharedKernel;
using LedgerX.SharedKernel.Context;
using LedgerX.SharedKernel.Paging;
using LedgerX.SharedKernel.Primitives;
using LedgerX.SharedKernel.Security;
using LedgerX.SharedKernel.Time;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace LedgerX.Identity.Infrastructure;

public sealed class IdentityService(
    UserManager<ApplicationUser> users,
    RoleManager<IdentityRole<Guid>> roles,
    IdentityDbContext db,
    IOutboxWriter outbox,
    IClock clock,
    ICorrelationAccessor correlation,
    IOptions<JwtOptions> jwtOptions) : IIdentityService
{
    public async Task<TokenResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        ValidatePassword(request.Password);
        var email = Guard.AgainstEmpty(request.Email, nameof(request.Email)).ToLowerInvariant();

        if (await users.FindByEmailAsync(email).ConfigureAwait(false) is not null)
        {
            throw new ConflictException("An account with this email already exists.", "email_taken");
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FirstName = Guard.AgainstEmpty(request.FirstName, nameof(request.FirstName)),
            LastName = Guard.AgainstEmpty(request.LastName, nameof(request.LastName))
        };

        var create = await users.CreateAsync(user, request.Password).ConfigureAwait(false);
        if (!create.Succeeded)
        {
            throw new DomainException(string.Join("; ", create.Errors.Select(e => e.Description)), "identity_error");
        }

        await EnsureRole(Roles.Customer).ConfigureAwait(false);
        await users.AddToRoleAsync(user, Roles.Customer).ConfigureAwait(false);

        var customer = Customer.Register(UserId.From(user.Id), email, user.FirstName, user.LastName, clock);
        user.CustomerId = customer.Id.Value;
        db.Customers.Add(customer);
        await users.UpdateAsync(user).ConfigureAwait(false);

        var evt = IntegrationEvents.Create(
            EventTypes.CustomerRegistered,
            new CustomerRegisteredV1(customer.Id.Value, user.Id, email, user.FirstName, user.LastName),
            correlation.Current.CorrelationId);

        await outbox.EnqueueAsync(KafkaTopics.Identity, evt, cancellationToken).ConfigureAwait(false);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await IssueTokenAsync(user).ConfigureAwait(false);
    }

    public async Task<TokenResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var email = Guard.AgainstEmpty(request.Email, nameof(request.Email)).ToLowerInvariant();
        var user = await users.FindByEmailAsync(email).ConfigureAwait(false);
        if (user is null || !await users.CheckPasswordAsync(user, request.Password).ConfigureAwait(false))
        {
            throw new ForbiddenException("Invalid email or password.");
        }

        if (user.CustomerId is { } customerId)
        {
            var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == CustomerId.From(customerId), cancellationToken)
                .ConfigureAwait(false);
            if (customer is { Status: CustomerStatus.Suspended or CustomerStatus.Closed })
            {
                throw new ForbiddenException("This customer account is not permitted to sign in.");
            }
        }

        return await IssueTokenAsync(user).ConfigureAwait(false);
    }

    public async Task<ProfileResponse> GetProfileAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId.ToString()).ConfigureAwait(false)
                   ?? throw new NotFoundException("User", userId);
        var roleList = await users.GetRolesAsync(user).ConfigureAwait(false);
        var status = "Active";
        if (user.CustomerId is { } cid)
        {
            var customer = await db.Customers.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == CustomerId.From(cid), cancellationToken)
                .ConfigureAwait(false);
            status = customer?.Status.ToString() ?? status;
        }

        return new ProfileResponse(user.Id, user.CustomerId, user.Email ?? string.Empty, user.FirstName, user.LastName, status, roleList.ToList());
    }

    public async Task<ProfileResponse> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId.ToString()).ConfigureAwait(false)
                   ?? throw new NotFoundException("User", userId);
        user.FirstName = Guard.AgainstEmpty(request.FirstName, nameof(request.FirstName));
        user.LastName = Guard.AgainstEmpty(request.LastName, nameof(request.LastName));
        await users.UpdateAsync(user).ConfigureAwait(false);

        if (user.CustomerId is { } cid)
        {
            var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == CustomerId.From(cid), cancellationToken)
                .ConfigureAwait(false);
            customer?.UpdateProfile(user.FirstName, user.LastName, clock);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return await GetProfileAsync(userId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<PagedResult<CustomerAdminResponse>> SearchCustomersAsync(string? query, PageRequest page, CancellationToken cancellationToken)
    {
        var q = db.Customers.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim().ToLowerInvariant();
            q = q.Where(c => c.Email.Contains(term) || c.FirstName.ToLower().Contains(term) || c.LastName.ToLower().Contains(term));
        }

        var total = await q.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await q.OrderBy(c => c.LastName).ThenBy(c => c.FirstName)
            .Skip(page.Skip)
            .Take(page.Take)
            .Select(c => new CustomerAdminResponse(c.Id.Value, c.UserId.Value, c.Email, c.FirstName, c.LastName, c.Status.ToString(), c.CreatedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<CustomerAdminResponse>(items, total, page.Page, page.Take);
    }

    public async Task<CustomerAdminResponse> GetCustomerAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var customer = await db.Customers.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == CustomerId.From(customerId), cancellationToken)
            .ConfigureAwait(false) ?? throw new NotFoundException("Customer", customerId);

        return new CustomerAdminResponse(customer.Id.Value, customer.UserId.Value, customer.Email, customer.FirstName, customer.LastName, customer.Status.ToString(), customer.CreatedAt);
    }

    public async Task ChangeCustomerStatusAsync(Guid customerId, string status, Guid actorId, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<CustomerStatus>(status, true, out var parsed))
        {
            throw new DomainException($"Unknown customer status '{status}'.");
        }

        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == CustomerId.From(customerId), cancellationToken)
            .ConfigureAwait(false) ?? throw new NotFoundException("Customer", customerId);

        var old = customer.Status.ToString();
        customer.ChangeStatus(parsed, clock);

        var evt = IntegrationEvents.Create(
            EventTypes.SecurityEventRaised,
            new SecurityEventRaisedV1("CUSTOMER_STATUS_CHANGED", actorId.ToString(), "Customer", customerId.ToString(), $"{old}->{parsed}"),
            correlation.Current.CorrelationId);

        await outbox.EnqueueAsync(KafkaTopics.Security, evt, cancellationToken).ConfigureAwait(false);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<TokenResponse> IssueTokenAsync(ApplicationUser user)
    {
        var jwt = jwtOptions.Value;
        var roleList = await users.GetRolesAsync(user).ConfigureAwait(false);
        var expires = clock.UtcNow.AddMinutes(jwt.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(ClaimTypesLx.DisplayName, $"{user.FirstName} {user.LastName}".Trim())
        };

        if (user.CustomerId is { } cid)
        {
            claims.Add(new Claim(ClaimTypesLx.CustomerId, cid.ToString()));
        }

        claims.AddRange(roleList.Select(r => new Claim(ClaimTypes.Role, r)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey));
        var token = new JwtSecurityToken(
            jwt.Issuer,
            jwt.Audience,
            claims,
            expires: expires.UtcDateTime,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new TokenResponse(
            new JwtSecurityTokenHandler().WriteToken(token),
            expires,
            "Bearer",
            roleList.ToList(),
            user.CustomerId,
            $"{user.FirstName} {user.LastName}".Trim());
    }

    private async Task EnsureRole(string role)
    {
        if (!await roles.RoleExistsAsync(role).ConfigureAwait(false))
        {
            await roles.CreateAsync(new IdentityRole<Guid>(role) { Id = Guid.NewGuid() }).ConfigureAwait(false);
        }
    }

    private static void ValidatePassword(string password)
    {
        Guard.AgainstEmpty(password, nameof(password));
        Guard.Against(password.Length < 12, "Passwords must be at least 12 characters.");
        Guard.Against(!password.Any(char.IsUpper), "Passwords must contain an uppercase letter.");
        Guard.Against(!password.Any(char.IsLower), "Passwords must contain a lowercase letter.");
        Guard.Against(!password.Any(char.IsDigit), "Passwords must contain a digit.");
    }
}

file static class ClaimTypesLx
{
    public const string CustomerId = LedgerClaims.CustomerId;
    public const string DisplayName = LedgerClaims.DisplayName;
}
