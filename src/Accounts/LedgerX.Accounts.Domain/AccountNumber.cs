using System.Security.Cryptography;

using LedgerX.SharedKernel;

namespace LedgerX.Accounts.Domain;

/// <summary>
/// Simulated UK-style account identifiers. These are not real bank account numbers
/// and must never be used with actual payment schemes.
/// </summary>
public sealed record AccountNumber
{
    public const string DemoSortCode = "04-00-04";

    public string Value { get; }

    public string SortCode { get; }

    private AccountNumber(string value, string sortCode)
    {
        Value = value;
        SortCode = sortCode;
    }

    public static AccountNumber CreateSimulated(string? sortCode = null)
    {
        var digits = new char[8];
        var bytes = RandomNumberGenerator.GetBytes(8);
        for (var i = 0; i < 8; i++)
        {
            digits[i] = (char)('0' + (bytes[i] % 10));
        }

        return new AccountNumber(new string(digits), sortCode ?? DemoSortCode);
    }

    public static AccountNumber Restore(string value, string sortCode)
    {
        Guard.AgainstEmpty(value, nameof(value));
        Guard.AgainstEmpty(sortCode, nameof(sortCode));
        Guard.Against(value.Length != 8 || !value.All(char.IsDigit), "Simulated account numbers must be 8 digits.");
        return new AccountNumber(value, sortCode);
    }

    public override string ToString() => $"{SortCode} {Value}";
}
