using System.Linq.Expressions;

using CTCare.Shared.Interfaces;

using Microsoft.EntityFrameworkCore;

namespace CTCare.Infrastructure.Extensions;

public static class PagingExtensions
{
    private static async Task<PagedResult<TDest>> ToPagedResultAsync<TSource, TDest>(
        this IQueryable<TSource> source,
        int page,
        int pageLength,
        Expression<Func<TSource, TDest>> selector,
        CancellationToken ct = default)
    {
        page = page < 1 ? 1 : page;
        pageLength = pageLength < 1 ? 10 : pageLength;

        var count = await source.CountAsync(ct);
        var pageCount = (int)Math.Ceiling(count / (double)pageLength);

        var items = await source
            .Skip((page - 1) * pageLength)
            .Take(pageLength)
            .Select(selector)
            .ToListAsync(ct);

        return new PagedResult<TDest>
        {
            Items = items,
            CurrentPage = page,
            ItemCount = count,
            PageCount = pageCount,
            PageLength = pageLength
        };
    }

    public static Task<PagedResult<TDest>> ToPagedResultAsync<TSource, TDest>(
        this IQueryable<TSource> source,
        IPagedRequest request,
        Expression<Func<TSource, TDest>> selector,
        int maxPageLength = 50,
        CancellationToken ct = default)
        => source.ToPagedResultAsync(
            request.Page,
            request.PageLength > maxPageLength ? maxPageLength : request.PageLength,
            selector,
            ct);
}
