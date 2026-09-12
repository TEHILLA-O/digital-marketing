namespace LedgerX.SharedKernel.Paging;

public sealed record PageRequest(int Page = 1, int PageSize = 25)
{
    public int Skip => Math.Max(0, (Math.Max(Page, 1) - 1) * PageSize);

    public int Take => Math.Clamp(PageSize, 1, 200);

    public static PageRequest Default => new();
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasNext => Page < TotalPages;

    public bool HasPrevious => Page > 1;
}
