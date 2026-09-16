using Xtzkt.Api.Exceptions;
using Xtzkt.Api.Filters.Base;
using Xtzkt.Api.Filters.Parameters;
using Xtzkt.Api.Services.ResponseCache;
using Xtzkt.Api.Utils;
using Xtzkt.Api.Utils.Validation;

namespace Xtzkt.Api.Filters;

public class Pagination : INormalizable
{
    /// <summary>
    /// Comma-separated list of fields (with optional sort direction) to sort by.
    ///
    /// Examples: `?sort=id`, `?sort=id.desc`, `?sort=balance.desc,id.asc`.
    /// </summary>
    public SortParameter? Sort { get; set; }

    /// <summary>
    /// Sort field values of the last item from the previous page — one value per `sort` field, comma-separated.
    /// Returns the items that go after it. Faster and safer than `offset` on long lists, because it doesn't
    /// skip or duplicate items when the data changes between requests.
    ///
    /// Examples: `?cursor=1234` with `?sort=id`, `?cursor=1000,1234` with `?sort=balance.desc,id`.
    /// </summary>
    public CursorParameter? Cursor { get; set; }

    /// <summary>
    /// Number of items to skip (0-{MaxOffset}). Simple, but gets slower the deeper you go, and it is capped
    /// for that reason — use `cursor` to page beyond the cap, and for long lists in general.
    ///
    /// Example: `?offset=100`.
    /// </summary>
    [OffsetRange(ErrorMessage = "Must be between {1} and {2}. To page deeper use `cursor` instead (see /#section/Get-Started/Pagination-and-sorting).")]
    public int Offset { get; set; } = 0;

    /// <summary>
    /// Maximum number of items to return (1-{MaxLimit}).
    ///
    /// Example: `?limit=50`.
    /// </summary>
    [LimitRange]
    public int Limit { get; set; } = ApiConfig.DefaultLimit;

    public string Normalize(string name) => ResponseCacheService.BuildKey("",
        ($"{name}.sort", Sort),
        ($"{name}.cursor", Cursor),
        ($"{name}.offset", Offset),
        ($"{name}.limit", Limit));

    public bool SortingBy(string field)
    {
        return Sort?.Cols.Any(x => x.field == field) == true;
    }

    public (long, bool)? GetInt64CursorOrDefault(string field)
    {
        if (Cursor?.Cols.Count is not > 0)
            return null;

        if (Sort?.Cols.FindIndex(x => x.field == field) is not int ind || ind == -1 || ind >= Cursor.Cols.Count)
            return null;

        if (!long.TryParse(Cursor.Cols[ind], out var cursor))
            throw new BadRequestException(nameof(Cursor), "Invalid Int64");

        return (cursor, Sort.Cols[ind].asc);
    }

    public (DateTime, bool)? GetDateTimeCursorOrDefault(string field)
    {
        if (Cursor?.Cols.Count is not > 0)
            return null;

        if (Sort?.Cols.FindIndex(x => x.field == field) is not int ind || ind == -1 || ind >= Cursor.Cols.Count)
            return null;

        if (!DateTimeOffset.TryParse(Cursor.Cols[ind], out var cursor))
            throw new BadRequestException(nameof(Cursor), "Invalid DateTime");

        return (cursor.UtcDateTime, Sort.Cols[ind].asc);
    }

    public void Reduce(SortSpec spec)
    {
        (Sort, Cursor) = Reduce(Sort, Cursor, spec);
    }

    public static (SortParameter Sort, CursorParameter? Cursor) Reduce(SortParameter? sort, CursorParameter? cursor, SortSpec spec)
    {
        if (sort == null)
        {
            return (
                new() { Cols = [(spec.PrimaryKey, true)] },
                cursor?.Cols.Count > 1 ? new() { Cols = [.. cursor.Cols.Take(1)] } : cursor);
        }

        if (sort.Cols.Any(x => !spec.ContainsKey(x.field)))
            throw new BadRequestException(nameof(Sort), $"Sort by the specified fields is not allowed. Allowed fields: {string.Join(", ", spec.Keys)}");

        var sortCols = new List<(string field, bool asc)>(sort.Cols.Count);
        var cursorCols = new List<string>(cursor?.Cols.Count ?? 0);
        var set = new HashSet<string>();

        int i;
        for (i = 0; i < sort.Cols.Count; i++)
        {
            if (set.Add(sort.Cols[i].field))
            {
                sortCols.Add(sort.Cols[i]);

                if (i < cursor?.Cols.Count)
                    cursorCols.Add(cursor.Cols[i]);
            }

            if (sort.Cols[i].field == spec.PrimaryKey)
                break;
        }

        if (sortCols.Count == 0 || sortCols[^1].field != spec.PrimaryKey)
        {
            sortCols.Add((spec.PrimaryKey, sortCols.Count == 0 || sortCols[^1].asc));

            if (i < cursor?.Cols.Count)
                cursorCols.Add(cursor.Cols[i]);
        }

        return (
            new() { Cols = sortCols },
            cursorCols.Count != 0 ? new() { Cols = cursorCols } : null);
    }
}
