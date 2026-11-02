using Xtzkt.Data;

namespace Xtzkt.Api.Filters.Parameters;

public class OrParameterBuilder(int? limit)
{
    readonly long _threshold = limit is int pageLimit ? (long)pageLimit + ApiConfig.FlatteningThreshold : long.MaxValue;
    readonly List<(string column, int value, long weight)> _items = [];

    public bool IsEmpty => _items.Count == 0;

    public void Add(string column, int value, long weight)
    {
        _items.Add((column, value, weight));
    }

    public void Add(IEnumerable<string> columns, int value, long weight)
    {
        foreach (var column in columns)
            _items.Add((column, value, weight));
    }

    public OrParameter Build()
    {
        var items = _items.OrderByDescending(x => x.weight);
        var flattened = items.Where(x => x.weight > _threshold).Take(ApiConfig.FlatteningLimit).ToList();
        var batched = items.Skip(flattened.Count)
            .GroupBy(x => x.value, x => x.column)
            .Select(valueColumns => (value: valueColumns.Key, columns: valueColumns.Distinct().Order().ToArray()))
            .GroupBy(x => (chain: x.value >> IdLayout.Shift32, columns: string.Join('|', x.columns)))
            .Select(chainColumns => (chainColumns.First().columns, chainColumns.Select(x => x.value).ToList()));

        return new OrParameter([
            .. flattened.Select(x => (new[] { x.column }, new List<int> { x.value })),
            .. batched]);
    }
}
