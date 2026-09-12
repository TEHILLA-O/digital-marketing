using LedgerX.SharedKernel;
using LedgerX.SharedKernel.Money;
using LedgerX.SharedKernel.Primitives;
using LedgerX.SharedKernel.Time;

namespace LedgerX.Accounts.Domain;

/// <summary>
/// Customer-facing bank account. Balances are projected from the immutable ledger;
/// <see cref="ProjectedBalance"/> is a cached read model, not the source of truth.
/// </summary>
public sealed class BankAccount : AggregateRoot<BankAccountId>
{
    public CustomerId CustomerId { get; private set; }

    public AccountNumber AccountNumber { get; private set; } = null!;

    public Currency Currency { get; private set; }

    public AccountType AccountType { get; private set; }

    public AccountStatus Status { get; private set; }

    public string DisplayName { get; private set; } = string.Empty;

    public decimal ProjectedBalance { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Optimistic concurrency token.</summary>
    public uint Version { get; private set; }

    private BankAccount()
    {
    }

    public static BankAccount Open(
        CustomerId customerId,
        AccountType type,
        Currency currency,
        string displayName,
        IClock clock,
        BankAccountId? id = null,
        AccountNumber? accountNumber = null)
    {
        return new BankAccount
        {
            Id = id ?? BankAccountId.New(),
            CustomerId = customerId,
            AccountNumber = accountNumber ?? AccountNumber.CreateSimulated(),
            Currency = currency,
            AccountType = type,
            Status = AccountStatus.Active,
            DisplayName = Guard.AgainstEmpty(displayName, nameof(displayName)),
            ProjectedBalance = 0m,
            CreatedAt = clock.UtcNow,
            UpdatedAt = clock.UtcNow
        };
    }

    public static BankAccount Restore(
        BankAccountId id,
        CustomerId customerId,
        AccountNumber accountNumber,
        AccountType type,
        Currency currency,
        AccountStatus status,
        string displayName,
        decimal projectedBalance,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        uint version)
    {
        return new BankAccount
        {
            Id = id,
            CustomerId = customerId,
            AccountNumber = accountNumber,
            AccountType = type,
            Currency = currency,
            Status = status,
            DisplayName = displayName,
            ProjectedBalance = projectedBalance,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            Version = version
        };
    }

    public bool CanSendMoney => Status == AccountStatus.Active;

    /// <summary>
    /// Incoming funds are allowed while Frozen. A freeze blocks outbound activity
    /// (transfers and withdrawals) but still permits credits such as salary or
    /// incoming transfers so the customer is not stranded. Documented in README and CONCURRENCY.md.
    /// </summary>
    public bool CanReceiveMoney => Status is AccountStatus.Active or AccountStatus.Frozen;

    public void Freeze(IClock clock)
    {
        EnsureNotClosed();
        if (Status == AccountStatus.Frozen)
        {
            return;
        }

        Status = AccountStatus.Frozen;
        UpdatedAt = clock.UtcNow;
    }

    public void Unfreeze(IClock clock)
    {
        EnsureNotClosed();
        if (Status != AccountStatus.Frozen)
        {
            throw new IllegalStateTransitionException(nameof(BankAccount), Status.ToString(), nameof(AccountStatus.Active));
        }

        Status = AccountStatus.Active;
        UpdatedAt = clock.UtcNow;
    }

    public void Close(IClock clock)
    {
        if (ProjectedBalance != 0m)
        {
            throw new DomainException("An account with a non-zero projected balance cannot be closed.", "account_not_empty");
        }

        Status = AccountStatus.Closed;
        UpdatedAt = clock.UtcNow;
    }

    public void ApplyProjectedBalance(decimal balance, IClock clock)
    {
        ProjectedBalance = decimal.Round(balance, Money.Scale, MidpointRounding.AwayFromZero);
        UpdatedAt = clock.UtcNow;
    }

    public void EnsureCanSend(Money amount)
    {
        if (!CanSendMoney)
        {
            throw new DomainException(
                Status == AccountStatus.Frozen
                    ? "Frozen accounts cannot send transfers or withdraw funds."
                    : $"Account {AccountNumber} cannot send funds while {Status}.",
                "account_not_active");
        }

        if (amount.Currency != Currency)
        {
            throw new CurrencyMismatchException(amount.Currency, Currency);
        }

        if (amount.Amount <= 0)
        {
            throw new DomainException("Transfer amount must be greater than zero.", "invalid_amount");
        }
    }

    public void EnsureCanReceive(Money amount)
    {
        if (!CanReceiveMoney)
        {
            throw new DomainException($"Account {AccountNumber} cannot receive funds while {Status}.", "account_cannot_receive");
        }

        if (amount.Currency != Currency)
        {
            throw new CurrencyMismatchException(amount.Currency, Currency);
        }
    }

    private void EnsureNotClosed()
    {
        if (Status == AccountStatus.Closed)
        {
            throw new DomainException("Closed accounts cannot change status.", "account_closed");
        }
    }
}
