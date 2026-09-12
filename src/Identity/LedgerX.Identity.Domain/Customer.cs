using LedgerX.SharedKernel;
using LedgerX.SharedKernel.Primitives;
using LedgerX.SharedKernel.Time;

namespace LedgerX.Identity.Domain;

public enum CustomerStatus
{
    Active = 1,
    Suspended = 2,
    Closed = 3
}

public sealed class Customer : AggregateRoot<CustomerId>
{
    public UserId UserId { get; private set; }

    public string Email { get; private set; } = string.Empty;

    public string FirstName { get; private set; } = string.Empty;

    public string LastName { get; private set; } = string.Empty;

    public string DisplayName => $"{FirstName} {LastName}".Trim();

    public CustomerStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    private Customer()
    {
    }

    public static Customer Register(
        UserId userId,
        string email,
        string firstName,
        string lastName,
        IClock clock,
        CustomerId? id = null)
    {
        var customer = new Customer
        {
            Id = id ?? CustomerId.New(),
            UserId = userId,
            Email = Guard.AgainstEmpty(email, nameof(email)).ToLowerInvariant(),
            FirstName = Guard.AgainstEmpty(firstName, nameof(firstName)),
            LastName = Guard.AgainstEmpty(lastName, nameof(lastName)),
            Status = CustomerStatus.Active,
            CreatedAt = clock.UtcNow,
            UpdatedAt = clock.UtcNow
        };

        return customer;
    }

    public void UpdateProfile(string firstName, string lastName, IClock clock)
    {
        FirstName = Guard.AgainstEmpty(firstName, nameof(firstName));
        LastName = Guard.AgainstEmpty(lastName, nameof(lastName));
        UpdatedAt = clock.UtcNow;
    }

    public void ChangeStatus(CustomerStatus status, IClock clock)
    {
        if (Status == CustomerStatus.Closed && status != CustomerStatus.Closed)
        {
            throw new IllegalStateTransitionException(nameof(Customer), Status.ToString(), status.ToString());
        }

        Status = status;
        UpdatedAt = clock.UtcNow;
    }

    public bool CanTransact => Status == CustomerStatus.Active;
}
