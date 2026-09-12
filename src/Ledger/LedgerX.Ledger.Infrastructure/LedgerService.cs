using LedgerX.Contracts.Api;
using LedgerX.Contracts.Events;
using LedgerX.Eventing.Outbox;
using LedgerX.Ledger.Application;
using LedgerX.Ledger.Domain;
using LedgerX.SharedKernel;
using LedgerX.SharedKernel.Context;
using LedgerX.SharedKernel.Money;
using LedgerX.SharedKernel.Paging;
using LedgerX.SharedKernel.Primitives;
using LedgerX.SharedKernel.Time;

using Microsoft.EntityFrameworkCore;

namespace LedgerX.Ledger.Infrastructure;

public sealed class LedgerService(
    LedgerDbContext db,
    IOutboxWriter outbox,
    IClock clock,
    ICorrelationAccessor correlation) : ILedgerService
{
    public async Task<JournalResponse> PostAsync(PostJournalRequest request, CancellationToken cancellationToken)
    {
        var existing = await FindByIdempotency(request.IdempotencyKey, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return await MapJournal(existing, cancellationToken).ConfigureAwait(false);
        }

        var currency = Currency.Parse(request.Currency);
        var journal = Journal.Draft(request.Reference, request.Description, currency, request.IdempotencyKey, clock, request.SourceType, request.SourceId);
        foreach (var line in request.Lines)
        {
            if (!Enum.TryParse<EntrySide>(line.Side, true, out var side))
            {
                throw new DomainException("Ledger line side must be Debit or Credit.");
            }

            journal.AddLine(LedgerAccountId.From(line.LedgerAccountId), side, new Money(line.Amount, currency), line.Narrative);
        }

        return await PersistPosted(journal, cancellationToken).ConfigureAwait(false);
    }

    public async Task<JournalResponse> PostTransferAsync(
        Guid sourceBankAccountId,
        Guid destinationBankAccountId,
        decimal amount,
        string currencyCode,
        string reference,
        string idempotencyKey,
        Guid sourceId,
        CancellationToken cancellationToken)
    {
        var existing = await FindByIdempotency(idempotencyKey, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return await MapJournal(existing, cancellationToken).ConfigureAwait(false);
        }

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var money = new Money(amount, Currency.Parse(currencyCode));
        var sender = await LockCustomerAccount(sourceBankAccountId, cancellationToken).ConfigureAwait(false);
        var receiver = await LockCustomerAccount(destinationBankAccountId, cancellationToken).ConfigureAwait(false);
        var available = await GetLiabilityBalance(sender.Id, cancellationToken).ConfigureAwait(false);
        if (available < money.Amount)
        {
            throw new InsufficientFundsException(sender.Code);
        }

        var journal = JournalFactory.InternalTransfer(sender, receiver, money, reference, idempotencyKey, sourceId, clock);
        var response = await PersistPosted(journal, cancellationToken).ConfigureAwait(false);
        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        return response;
    }

    public async Task<JournalResponse> PostDepositAsync(
        Guid bankAccountId,
        decimal amount,
        string currencyCode,
        string reference,
        string idempotencyKey,
        Guid sourceId,
        CancellationToken cancellationToken)
    {
        var existing = await FindByIdempotency(idempotencyKey, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return await MapJournal(existing, cancellationToken).ConfigureAwait(false);
        }

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var cash = await LockSystem(ChartOfAccounts.HouseCash, cancellationToken).ConfigureAwait(false);
        var liability = await LockCustomerAccount(bankAccountId, cancellationToken).ConfigureAwait(false);
        var journal = JournalFactory.Deposit(cash, liability, new Money(amount, Currency.Parse(currencyCode)), reference, idempotencyKey, sourceId, clock);
        var response = await PersistPosted(journal, cancellationToken).ConfigureAwait(false);
        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        return response;
    }

    public async Task<JournalResponse> PostWithdrawalAsync(
        Guid bankAccountId,
        decimal amount,
        string currencyCode,
        string reference,
        string idempotencyKey,
        Guid sourceId,
        CancellationToken cancellationToken)
    {
        var existing = await FindByIdempotency(idempotencyKey, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return await MapJournal(existing, cancellationToken).ConfigureAwait(false);
        }

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var cash = await LockSystem(ChartOfAccounts.HouseCash, cancellationToken).ConfigureAwait(false);
        var liability = await LockCustomerAccount(bankAccountId, cancellationToken).ConfigureAwait(false);
        var money = new Money(amount, Currency.Parse(currencyCode));
        var available = await GetLiabilityBalance(liability.Id, cancellationToken).ConfigureAwait(false);
        if (available < money.Amount)
        {
            throw new InsufficientFundsException(liability.Code);
        }

        var journal = JournalFactory.Withdrawal(cash, liability, money, reference, idempotencyKey, sourceId, clock);
        var response = await PersistPosted(journal, cancellationToken).ConfigureAwait(false);
        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        return response;
    }

    public async Task<JournalResponse> ReverseAsync(Guid journalId, CancellationToken cancellationToken)
    {
        var posted = await LoadJournal(journalId, cancellationToken).ConfigureAwait(false);
        var reversal = posted.Reverse($"REV-{posted.Reference}", clock);
        db.Journals.Add(reversal);
        db.LedgerLines.AddRange(reversal.Lines);
        await EnqueuePosted(reversal, cancellationToken).ConfigureAwait(false);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await MapJournal(reversal, cancellationToken).ConfigureAwait(false);
    }

    public async Task<JournalResponse> GetJournalAsync(Guid journalId, CancellationToken cancellationToken) =>
        await MapJournal(await LoadJournal(journalId, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);

    public async Task<PagedResult<JournalResponse>> ListJournalsAsync(string? query, PageRequest page, CancellationToken cancellationToken)
    {
        var q = db.Journals.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim();
            q = q.Where(j => j.Reference.Contains(term) || j.Description.Contains(term));
        }

        var total = await q.CountAsync(cancellationToken).ConfigureAwait(false);
        var journals = await q.OrderByDescending(j => j.CreatedAt).Skip(page.Skip).Take(page.Take).ToListAsync(cancellationToken).ConfigureAwait(false);
        var mapped = new List<JournalResponse>();
        foreach (var journal in journals)
        {
            mapped.Add(await MapJournal(journal, cancellationToken).ConfigureAwait(false));
        }

        return new PagedResult<JournalResponse>(mapped, total, page.Page, page.Take);
    }

    public async Task<IReadOnlyList<LedgerAccountResponse>> ListAccountsAsync(CancellationToken cancellationToken)
    {
        var accounts = await db.LedgerAccounts.AsNoTracking().OrderBy(a => a.Code).ToListAsync(cancellationToken).ConfigureAwait(false);
        var result = new List<LedgerAccountResponse>();
        foreach (var account in accounts)
        {
            var balance = await GetSignedBalance(account, cancellationToken).ConfigureAwait(false);
            result.Add(new LedgerAccountResponse(account.Id.Value, account.Code, account.Name, account.Class.ToString(), account.Currency.Code, account.BankAccountId?.Value, balance));
        }

        return result;
    }

    public async Task<LedgerAccountResponse> EnsureCustomerAccountAsync(EnsureCustomerLedgerAccountRequest request, CancellationToken cancellationToken)
    {
        var code = ChartOfAccounts.CustomerDepositCode(request.BankAccountId);
        var existing = await db.LedgerAccounts.FirstOrDefaultAsync(a => a.Code == code, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            existing = LedgerAccount.CreateCustomerDeposit(
                BankAccountId.From(request.BankAccountId),
                CustomerId.From(request.CustomerId),
                Currency.Parse(request.Currency),
                request.DisplayName,
                clock);
            db.LedgerAccounts.Add(existing);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        var balance = await GetLiabilityBalance(existing.Id, cancellationToken).ConfigureAwait(false);
        return new LedgerAccountResponse(existing.Id.Value, existing.Code, existing.Name, existing.Class.ToString(), existing.Currency.Code, existing.BankAccountId?.Value, balance);
    }

    public async Task<decimal> GetCustomerBalanceAsync(Guid bankAccountId, CancellationToken cancellationToken)
    {
        var account = await db.LedgerAccounts.AsNoTracking()
            .FirstOrDefaultAsync(a => a.BankAccountId == BankAccountId.From(bankAccountId), cancellationToken)
            .ConfigureAwait(false);
        return account is null ? 0m : await GetLiabilityBalance(account.Id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<StatementResponse> GetStatementAsync(Guid bankAccountId, DateTimeOffset fromDate, DateTimeOffset toDate, CancellationToken cancellationToken)
    {
        var account = await db.LedgerAccounts.AsNoTracking()
            .FirstOrDefaultAsync(a => a.BankAccountId == BankAccountId.From(bankAccountId), cancellationToken)
            .ConfigureAwait(false) ?? throw new NotFoundException("LedgerAccount", bankAccountId);

        var period = await db.LedgerLines.AsNoTracking()
            .Join(db.Journals.AsNoTracking(), line => line.JournalId, journal => journal.Id, (line, journal) => new { line, journal })
            .Where(x => x.line.LedgerAccountId == account.Id && x.journal.PostedAt != null && x.journal.PostedAt >= fromDate && x.journal.PostedAt <= toDate)
            .OrderBy(x => x.journal.PostedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var opening = await db.LedgerLines.AsNoTracking()
            .Join(db.Journals.AsNoTracking(), line => line.JournalId, journal => journal.Id, (line, journal) => new { line, journal })
            .Where(x => x.line.LedgerAccountId == account.Id && x.journal.PostedAt != null && x.journal.PostedAt < fromDate)
            .Select(x => x.line)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var running = opening.Sum(l => account.SignedImpact(l.Side, l.Amount.Amount));
        var openingBalance = running;
        var statementLines = new List<StatementLineResponse>();
        foreach (var row in period)
        {
            running += account.SignedImpact(row.line.Side, row.line.Amount.Amount);
            statementLines.Add(new StatementLineResponse(
                row.journal.PostedAt ?? row.journal.CreatedAt,
                row.journal.Reference,
                row.journal.Description,
                row.line.Side.ToString(),
                row.line.Amount.Amount,
                running));
        }

        return new StatementResponse(
            bankAccountId,
            account.Code,
            account.Currency.Code,
            fromDate,
            toDate,
            openingBalance,
            running,
            statementLines);
    }

    private async Task<JournalResponse> PersistPosted(Journal journal, CancellationToken cancellationToken)
    {
        journal.Post(clock);
        db.Journals.Add(journal);
        db.LedgerLines.AddRange(journal.Lines);
        await EnqueuePosted(journal, cancellationToken).ConfigureAwait(false);
        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("idempotency", StringComparison.OrdinalIgnoreCase) == true
                                           || ex.InnerException?.Message.Contains("IX_journals_IdempotencyKey", StringComparison.OrdinalIgnoreCase) == true)
        {
            var replay = await FindByIdempotency(journal.IdempotencyKey, cancellationToken).ConfigureAwait(false)
                         ?? throw new ConflictException("Duplicate journal idempotency key.");
            return await MapJournal(replay, cancellationToken).ConfigureAwait(false);
        }

        return await MapJournal(journal, cancellationToken).ConfigureAwait(false);
    }

    private async Task EnqueuePosted(Journal journal, CancellationToken cancellationToken)
    {
        var evt = IntegrationEvents.Create(
            EventTypes.JournalPosted,
            new JournalPostedV1(
                journal.Id.Value,
                journal.Reference,
                journal.Currency.Code,
                journal.TotalDebits.Amount,
                journal.TotalCredits.Amount,
                journal.SourceType,
                journal.SourceId,
                journal.Lines.Select(l => new JournalLineV1(l.Id.Value, l.LedgerAccountId.Value, l.Side.ToString(), l.Amount.Amount, l.Narrative)).ToList()),
            correlation.Current.CorrelationId);
        await outbox.EnqueueAsync(KafkaTopics.Ledger, evt, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Journal?> FindByIdempotency(string key, CancellationToken cancellationToken) =>
        await db.Journals.FirstOrDefaultAsync(j => j.IdempotencyKey == key, cancellationToken).ConfigureAwait(false);

    private async Task<Journal> LoadJournal(Guid journalId, CancellationToken cancellationToken)
    {
        var journal = await db.Journals.FirstOrDefaultAsync(j => j.Id == JournalId.From(journalId), cancellationToken)
            .ConfigureAwait(false) ?? throw new NotFoundException("Journal", journalId);
        var lines = await db.LedgerLines.Where(l => l.JournalId == journal.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var line in lines)
        {
            journal.AttachExistingLine(line);
        }

        return journal;
    }

    private async Task<JournalResponse> MapJournal(Journal journal, CancellationToken cancellationToken)
    {
        if (journal.Lines.Count == 0)
        {
            var lines = await db.LedgerLines.AsNoTracking().Where(l => l.JournalId == journal.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
            foreach (var line in lines)
            {
                journal.AttachExistingLine(line);
            }
        }

        var accountIds = journal.Lines.Select(l => l.LedgerAccountId).Distinct().ToList();
        var accounts = await db.LedgerAccounts.AsNoTracking()
            .Where(a => accountIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, cancellationToken)
            .ConfigureAwait(false);

        return new JournalResponse(
            journal.Id.Value,
            journal.Reference,
            journal.Description,
            journal.Currency.Code,
            journal.Status.ToString(),
            journal.TotalDebits.Amount,
            journal.TotalCredits.Amount,
            journal.IsBalanced,
            journal.SourceType,
            journal.SourceId,
            journal.CreatedAt,
            journal.PostedAt,
            journal.Lines.Select(l => new JournalLineResponse(
                l.Id.Value,
                l.LedgerAccountId.Value,
                accounts.TryGetValue(l.LedgerAccountId, out var acc) ? acc.Code : l.LedgerAccountId.ToString(),
                l.Side.ToString(),
                l.Amount.Amount,
                l.Narrative)).ToList());
    }

    private async Task<LedgerAccount> LockCustomerAccount(Guid bankAccountId, CancellationToken cancellationToken)
    {
        var account = await db.LedgerAccounts
            .FromSqlInterpolated($"SELECT * FROM ledger.ledger_accounts WHERE bank_account_id = {bankAccountId} FOR UPDATE")
            .AsTracking()
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return account ?? throw new NotFoundException("LedgerAccount", bankAccountId);
    }

    private async Task<LedgerAccount> LockSystem(string code, CancellationToken cancellationToken)
    {
        var account = await db.LedgerAccounts
            .FromSqlInterpolated($"SELECT * FROM ledger.ledger_accounts WHERE code = {code} FOR UPDATE")
            .AsTracking()
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return account ?? throw new NotFoundException("LedgerAccount", code);
    }

    private async Task<decimal> GetLiabilityBalance(LedgerAccountId accountId, CancellationToken cancellationToken)
    {
        var lines = await db.LedgerLines.AsNoTracking().Where(l => l.LedgerAccountId == accountId).ToListAsync(cancellationToken).ConfigureAwait(false);
        var credits = lines.Where(l => l.Side == EntrySide.Credit).Sum(l => l.Amount.Amount);
        var debits = lines.Where(l => l.Side == EntrySide.Debit).Sum(l => l.Amount.Amount);
        return credits - debits;
    }

    private async Task<decimal> GetSignedBalance(LedgerAccount account, CancellationToken cancellationToken)
    {
        var lines = await db.LedgerLines.AsNoTracking().Where(l => l.LedgerAccountId == account.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
        return lines.Sum(l => account.SignedImpact(l.Side, l.Amount.Amount));
    }
}
