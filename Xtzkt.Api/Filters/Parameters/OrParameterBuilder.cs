namespace Xtzkt.Api.Filters.Parameters;

public class OrParameterBuilder(int limit)
{
    readonly long _threshold = (long)limit + ApiConfig.FlatteningThreshold;
    readonly List<(string column, int value, long weight)> _items = [];

    public bool IsEmpty => _items.Count == 0;

    public void Add(string column, int value, long weight)
    {
        _items.Add((column, value, weight));
    }

    public OrParameter Build()
    {
        var items = _items.OrderByDescending(x => x.weight);
        var flattened = items.Where(x => x.weight > _threshold).Take(ApiConfig.FlatteningLimit).ToList();
        var batched = items.Skip(flattened.Count).GroupBy(x => x.column);

        return new OrParameter([
            .. flattened.Select(x => (x.column, new List<int> { x.value })),
            .. batched.Select(x => (x.Key, x.Select(x => x.value).ToList()))]);
    }
}
