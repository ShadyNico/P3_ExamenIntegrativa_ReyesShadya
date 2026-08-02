namespace AirportApp.Models;

public sealed class PaginationInfo
{
    public int CurrentPage { get; init; }
    public int TotalPages { get; init; }
    public int TotalItems { get; init; }
    public int PageSize { get; init; }

    public int FirstItem => TotalItems == 0 ? 0 : ((CurrentPage - 1) * PageSize) + 1;
    public int LastItem => Math.Min(CurrentPage * PageSize, TotalItems);
}
