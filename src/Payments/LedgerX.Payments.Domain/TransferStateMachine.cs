using LedgerX.SharedKernel;

namespace LedgerX.Payments.Domain;

public static class TransferStateMachine
{
    private static readonly Dictionary<TransferStatus, HashSet<TransferStatus>> Allowed = new()
    {
        [TransferStatus.Created] = [TransferStatus.Validated, TransferStatus.Rejected, TransferStatus.Cancelled],
        [TransferStatus.Validated] = [TransferStatus.Processing, TransferStatus.Rejected, TransferStatus.Cancelled],
        [TransferStatus.Processing] = [TransferStatus.Completed, TransferStatus.Failed, TransferStatus.Cancelled],
        [TransferStatus.Completed] = [],
        [TransferStatus.Rejected] = [],
        [TransferStatus.Failed] = [],
        [TransferStatus.Cancelled] = []
    };

    public static bool CanTransition(TransferStatus from, TransferStatus to) =>
        Allowed.TryGetValue(from, out var next) && next.Contains(to);

    public static void Ensure(TransferStatus from, TransferStatus to)
    {
        if (!CanTransition(from, to))
        {
            throw new IllegalStateTransitionException("Transfer", from.ToString(), to.ToString());
        }
    }

    public static bool IsTerminal(TransferStatus status) =>
        status is TransferStatus.Completed or TransferStatus.Rejected or TransferStatus.Failed or TransferStatus.Cancelled;
}
