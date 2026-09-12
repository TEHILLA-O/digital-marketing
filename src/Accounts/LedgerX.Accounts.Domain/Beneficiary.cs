using LedgerX.SharedKernel;
using LedgerX.SharedKernel.Money;
using LedgerX.SharedKernel.Primitives;
using LedgerX.SharedKernel.Time;

namespace LedgerX.Accounts.Domain;

public sealed class Beneficiary : AggregateRoot<BeneficiaryId>
{
    public CustomerId OwnerCustomerId { get; private set; }

    public string DisplayName { get; private set; } = string.Empty;

    public string AccountNumber { get; private set; } = string.Empty;

    public string SortCode { get; private set; } = string.Empty;

    public Currency Currency { get; private set; }

    public BankAccountId? LinkedBankAccountId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    private Beneficiary()
    {
    }

    public static Beneficiary CreateInternal(
        CustomerId ownerCustomerId,
        string displayName,
        BankAccount destination,
        IClock clock)
    {
        Guard.Against(destination.CustomerId == ownerCustomerId, "You cannot add one of your own accounts as a beneficiary.", "own_account");

        return new Beneficiary
        {
            Id = BeneficiaryId.New(),
            OwnerCustomerId = ownerCustomerId,
            DisplayName = Guard.AgainstEmpty(displayName, nameof(displayName)),
            AccountNumber = destination.AccountNumber.Value,
            SortCode = destination.AccountNumber.SortCode,
            Currency = destination.Currency,
            LinkedBankAccountId = destination.Id,
            CreatedAt = clock.UtcNow
        };
    }
}
