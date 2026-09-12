using LedgerX.Contracts.Demo;
using LedgerX.Eventing.Persistence;
using LedgerX.Payments.Domain;
using LedgerX.SharedKernel.Money;
using LedgerX.SharedKernel.Primitives;
using LedgerX.SharedKernel.Time;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LedgerX.Payments.Infrastructure;

public sealed class PaymentsSeeder(PaymentsDbContext db, IClock clock, ILogger<PaymentsSeeder> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        await db.MigrateOrCreateAsync(cancellationToken).ConfigureAwait(false);

        if (!await db.Transfers.AnyAsync(t => t.Status == TransferStatus.Failed, cancellationToken).ConfigureAwait(false))
        {
            var failed = Transfer.Create(
                PaymentKind.InternalTransfer,
                CustomerId.From(DemoIds.CarolCustomer),
                BankAccountId.From(DemoIds.CarolCurrent),
                BankAccountId.From(DemoIds.AliceCurrent),
                null,
                Money.Gbp(5000m),
                "Seeded failed payment for operations review",
                "seed:failed-carol",
                clock);
            failed.MarkValidated(clock);
            failed.MarkProcessing(clock);
            failed.Fail("Insufficient funds — seeded demo failure.", clock);
            db.Transfers.Add(failed);
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Payments seed completed.");
    }
}
