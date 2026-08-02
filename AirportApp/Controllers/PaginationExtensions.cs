using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AirportApp.Models;

namespace AirportApp.Controllers;

public static class PaginationExtensions
{
    public const int DefaultPageSize = 10;

    public static async Task<List<T>> PaginateAsync<T>(
        this Controller controller,
        IQueryable<T> query,
        int page,
        int pageSize = DefaultPageSize)
    {
        var totalItems = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));
        var currentPage = Math.Clamp(page, 1, totalPages);

        var items = await query
            .Skip((currentPage - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        controller.ViewData["Pagination"] = new PaginationInfo
        {
            CurrentPage = currentPage,
            TotalPages = totalPages,
            TotalItems = totalItems,
            PageSize = pageSize
        };

        return items;
    }
}
