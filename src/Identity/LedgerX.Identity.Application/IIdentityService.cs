using LedgerX.Contracts.Api;
using LedgerX.SharedKernel.Paging;

namespace LedgerX.Identity.Application;

public interface IIdentityService
{
    Task<TokenResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);

    Task<TokenResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

    Task<ProfileResponse> GetProfileAsync(Guid userId, CancellationToken cancellationToken);

    Task<ProfileResponse> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken);

    Task<PagedResult<CustomerAdminResponse>> SearchCustomersAsync(string? query, PageRequest page, CancellationToken cancellationToken);

    Task<CustomerAdminResponse> GetCustomerAsync(Guid customerId, CancellationToken cancellationToken);

    Task ChangeCustomerStatusAsync(Guid customerId, string status, Guid actorId, CancellationToken cancellationToken);
}
