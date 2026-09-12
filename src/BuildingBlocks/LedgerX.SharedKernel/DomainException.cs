namespace LedgerX.SharedKernel;

/// <summary>
/// Thrown when a domain invariant is violated. Mapped to Problem Details by APIs.
/// </summary>
public class DomainException : Exception
{
    public string Code { get; }

    public DomainException(string message, string code = "domain_error")
        : base(message)
    {
        Code = code;
    }
}

public sealed class CurrencyMismatchException : DomainException
{
    public CurrencyMismatchException(Money.Currency left, Money.Currency right)
        : base($"Cannot combine amounts in {left.Code} and {right.Code}.", "currency_mismatch")
    {
    }
}

public sealed class InsufficientFundsException : DomainException
{
    public InsufficientFundsException(string accountReference)
        : base($"Account {accountReference} does not have sufficient available funds.", "insufficient_funds")
    {
    }
}

public sealed class IllegalStateTransitionException : DomainException
{
    public IllegalStateTransitionException(string entity, string from, string to)
        : base($"Cannot transition {entity} from '{from}' to '{to}'.", "illegal_state_transition")
    {
    }
}

public sealed class ConcurrencyConflictException : DomainException
{
    public ConcurrencyConflictException(string message)
        : base(message, "concurrency_conflict")
    {
    }
}

public sealed class NotFoundException : DomainException
{
    public NotFoundException(string entity, object id)
        : base($"{entity} '{id}' was not found.", "not_found")
    {
    }
}

public sealed class ForbiddenException : DomainException
{
    public ForbiddenException(string message = "You are not allowed to perform this action.")
        : base(message, "forbidden")
    {
    }
}

public sealed class ConflictException : DomainException
{
    public ConflictException(string message, string code = "conflict")
        : base(message, code)
    {
    }
}
