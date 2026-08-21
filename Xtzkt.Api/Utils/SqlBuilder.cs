using Dapper;
using System.Numerics;
using Netezos.Encoding;
using Xtzkt.Api.Exceptions;
using Xtzkt.Api.Filters;
using Xtzkt.Api.Filters.Parameters;
using Xtzkt.Api.Utils;
using Xtzkt.Utils;

namespace Xtzkt.Api;

public class SqlBuilder(SqlBuilder? _root = null)
{
    object? _from;
    string? _fromAlias;
    readonly List<string> _cols = [];
    readonly List<string> _joins = [];
    readonly List<string> _filters = [];
    readonly List<(string field, string column, bool asc)> _sorting = [];
    int _offset = 0;
    int _limit = 0;

    readonly DynamicParameters _params = new();
    int _counter = 0;

    public SqlBuilder Select()
    {
        return this;
    }

    public SqlBuilder Select(string col)
    {
        _cols.Add(col);
        return this;
    }

    public SqlBuilder Select(IEnumerable<string> cols)
    {
        _cols.AddRange(cols);
        return this;
    }

    public SqlBuilder From(string table, string? alias = null)
    {
        _from = table;
        _fromAlias = alias;
        return this;
    }

    public SqlBuilder From(SqlBuilder subquery, string? alias = null)
    {
        _from = subquery;
        _fromAlias = alias;
        return this;
    }

    public SqlBuilder From(SqlBuilder[] subqueries, string? alias = null)
    {
        _from = subqueries;
        _fromAlias = alias;
        return this;
    }

    public SqlBuilder InnerJoin(string table, string alias, string joinedCol, string sourceCol)
    {
        _joins.Add($"INNER JOIN {table} AS {alias} ON {alias}.{joinedCol} = {sourceCol}");
        return this;
    }

    public SqlBuilder LeftJoin(string table, string alias, string joinedCol, string sourceCol)
    {
        _joins.Add($"LEFT JOIN {table} AS {alias} ON {alias}.{joinedCol} = {sourceCol}");
        return this;
    }

    public SqlBuilder Where(OrParameter? or)
    {
        if (or == null) return this;

        var expressions = new List<string>(or.ColsAndVals.Length);
        foreach (var (column, values) in or.ColsAndVals)
        {
            if (values == null || values.Count == 0)
                continue;

            expressions.Add(values.Count == 1
                ? $"{column} = {Param(values[0])}"
                : $"{column} = ANY ({Param(values)})");
        }

        if (expressions.Count != 0)
            _filters.Add($"({string.Join(" OR ", expressions)})");

        return this;
    }

    public SqlBuilder Where(AnyOfParameter? anyof, Func<string, string> map)
    {
        if (anyof == null) return this;

        if (anyof.Eq is int eq)
        {
            if (eq == Int32NullParameter.Null)
                _filters.Add($"({string.Join(" OR ", anyof.Fields.Select(x => $"{map(x)} IS NULL"))})");
            else
            {
                var p = Param(eq);
                _filters.Add($"({string.Join(" OR ", anyof.Fields.Select(x => $"{map(x)} = {p}"))})");
            }
        }

        if (anyof.In != null)
        {
            if (anyof.In.Contains(Int32NullParameter.Null))
            {
                var p = Param(anyof.In.Where(x => x != Int32NullParameter.Null).ToArray());
                _filters.Add($"({string.Join(" OR ", anyof.Fields.Select(x => $"({map(x)} IS NULL OR {map(x)} = ANY ({p}))"))})");
            }
            else
            {
                var p = Param(anyof.In);
                _filters.Add($"({string.Join(" OR ", anyof.Fields.Select(x => $"{map(x)} = ANY ({p})"))})");
            }
        }

        return this;
    }

    public SqlBuilder Where(string column, BigIntegerParameter? value)
    {
        if (value == null) return this;

        if (value.Eq != null)
            _filters.Add($"{column} = {Param(value.Eq)}");

        if (value.Ne != null)
            _filters.Add($"{column} != {Param(value.Ne)}");

        if (value.Gt != null)
            _filters.Add($"{column} > {Param(value.Gt)}");

        if (value.Ge != null)
            _filters.Add($"{column} >= {Param(value.Ge)}");

        if (value.Lt != null)
            _filters.Add($"{column} < {Param(value.Lt)}");

        if (value.Le != null)
            _filters.Add($"{column} <= {Param(value.Le)}");

        if (value.In != null)
            _filters.Add($"{column} = ANY ({Param(value.In)}::numeric[])");

        if (value.Ni != null)
            _filters.Add($"NOT ({column} = ANY ({Param(value.Ni)}::numeric[]))");

        return this;
    }

    public SqlBuilder Where(string column, ChainIdParameter? value)
    {
        if (value == null) return this;

        if (value.Eq != null)
            _filters.Add($"{column} = {Param(value.Eq)}");

        if (value.Ne != null)
            _filters.Add($"{column} != {Param(value.Ne)}");

        if (value.In != null)
            _filters.Add($"{column} = ANY ({Param(value.In)})");

        if (value.Ni != null)
            _filters.Add($"NOT ({column} = ANY ({Param(value.Ni)}))");

        return this;
    }

    public SqlBuilder Where(string column, Int32NullParameter? value)
    {
        if (value == null) return this;

        if (value.Eq is int eq)
        {
            if (eq == Int32NullParameter.Null)
                _filters.Add($"{column} IS NULL");
            else
                _filters.Add($"{column} = {Param(eq)}");
        }

        if (value.Ne is int ne)
        {
            if (ne == Int32NullParameter.Null)
                _filters.Add($"{column} IS NOT NULL");
            else
                _filters.Add($"{column} != {Param(ne)}");
        }

        if (value.Gt is int gt && gt != Int32NullParameter.Null)
            _filters.Add($"{column} > {Param(gt)}");

        if (value.Ge is int ge && ge != Int32NullParameter.Null)
            _filters.Add($"{column} >= {Param(ge)}");

        if (value.Lt is int lt && lt != Int32NullParameter.Null)
            _filters.Add($"{column} < {Param(lt)}");

        if (value.Le is int le && le != Int32NullParameter.Null)
            _filters.Add($"{column} <= {Param(le)}");

        if (value.In != null)
        {
            if (value.In.Contains(Int32NullParameter.Null))
                _filters.Add($"({column} IS NULL OR {column} = ANY ({Param(value.In.Where(x => x != Int32NullParameter.Null).ToArray())}))");
            else
                _filters.Add($"{column} = ANY ({Param(value.In)})");
        }

        if (value.Ni != null)
        {
            if (value.Ni.Contains(Int32NullParameter.Null))
                _filters.Add($"({column} IS NOT NULL AND NOT ({column} = ANY ({Param(value.Ni.Where(x => x != Int32NullParameter.Null).ToArray())})))");
            else
                _filters.Add($"NOT ({column} = ANY ({Param(value.Ni)}))");
        }

        return this;
    }

    public SqlBuilder Where(string column, Int64NullParameter? value)
    {
        if (value == null) return this;

        if (value.Eq is long eq)
        {
            if (eq == Int64NullParameter.Null)
                _filters.Add($"{column} IS NULL");
            else
                _filters.Add($"{column} = {Param(eq)}");
        }

        if (value.Ne is long ne)
        {
            if (ne == Int64NullParameter.Null)
                _filters.Add($"{column} IS NOT NULL");
            else
                _filters.Add($"{column} != {Param(ne)}");
        }

        if (value.Gt is long gt && gt != Int64NullParameter.Null)
            _filters.Add($"{column} > {Param(gt)}");

        if (value.Ge is long ge && ge != Int64NullParameter.Null)
            _filters.Add($"{column} >= {Param(ge)}");

        if (value.Lt is long lt && lt != Int64NullParameter.Null)
            _filters.Add($"{column} < {Param(lt)}");

        if (value.Le is long le && le != Int64NullParameter.Null)
            _filters.Add($"{column} <= {Param(le)}");

        if (value.In != null)
        {
            if (value.In.Contains(Int64NullParameter.Null))
                _filters.Add($"({column} IS NULL OR {column} = ANY ({Param(value.In.Where(x => x != Int64NullParameter.Null).ToArray())}))");
            else
                _filters.Add($"{column} = ANY ({Param(value.In)})");
        }

        if (value.Ni != null)
        {
            if (value.Ni.Contains(Int64NullParameter.Null))
                _filters.Add($"({column} IS NOT NULL AND NOT ({column} = ANY ({Param(value.Ni.Where(x => x != Int64NullParameter.Null).ToArray())})))");
            else
                _filters.Add($"NOT ({column} = ANY ({Param(value.Ni)}))");
        }

        return this;
    }

    public SqlBuilder Where(string column, BigIntegerNullParameter? value)
    {
        if (value == null) return this;

        if (value.Eq is BigInteger eq)
        {
            if (eq == BigIntegerNullParameter.Null)
                _filters.Add($"{column} IS NULL");
            else
                _filters.Add($"{column} = {Param(eq)}");
        }

        if (value.Ne is BigInteger ne)
        {
            if (ne == BigIntegerNullParameter.Null)
                _filters.Add($"{column} IS NOT NULL");
            else
                _filters.Add($"{column} != {Param(ne)}");
        }

        if (value.Gt is BigInteger gt && gt != BigIntegerNullParameter.Null)
            _filters.Add($"{column} > {Param(gt)}");

        if (value.Ge is BigInteger ge && ge != BigIntegerNullParameter.Null)
            _filters.Add($"{column} >= {Param(ge)}");

        if (value.Lt is BigInteger lt && lt != BigIntegerNullParameter.Null)
            _filters.Add($"{column} < {Param(lt)}");

        if (value.Le is BigInteger le && le != BigIntegerNullParameter.Null)
            _filters.Add($"{column} <= {Param(le)}");

        if (value.In != null)
        {
            if (value.In.Contains(BigIntegerNullParameter.Null))
                _filters.Add($"({column} IS NULL OR {column} = ANY ({Param(value.In.Where(x => x != BigIntegerNullParameter.Null).ToArray())}::numeric[]))");
            else
                _filters.Add($"{column} = ANY ({Param(value.In)}::numeric[])");
        }

        if (value.Ni != null)
        {
            if (value.Ni.Contains(BigIntegerNullParameter.Null))
                _filters.Add($"({column} IS NOT NULL AND NOT ({column} = ANY ({Param(value.Ni.Where(x => x != BigIntegerNullParameter.Null).ToArray())}::numeric[])))");
            else
                _filters.Add($"NOT ({column} = ANY ({Param(value.Ni)}::numeric[]))");
        }

        return this;
    }

    public SqlBuilder Where(string column, StringNullParameter? value)
    {
        if (value == null) return this;

        if (value.Eq != null)
        {
            if (value.Eq == StringNullParameter.Null)
                _filters.Add($"{column} IS NULL");
            else
                _filters.Add($"{column} = {Param(value.Eq)}");
        }

        if (value.Ne != null)
        {
            if (value.Ne == StringNullParameter.Null)
                _filters.Add($"{column} IS NOT NULL");
            else
                _filters.Add($"{column} != {Param(value.Ne)}");
        }

        if (value.In != null)
        {
            if (value.In.Contains(StringNullParameter.Null))
                _filters.Add($"({column} IS NULL OR {column} = ANY ({Param(value.In.Where(x => x != StringNullParameter.Null).ToArray())}))");
            else
                _filters.Add($"{column} = ANY ({Param(value.In)})");
        }

        if (value.Ni != null)
        {
            if (value.Ni.Contains(StringNullParameter.Null))
                _filters.Add($"({column} IS NOT NULL AND NOT ({column} = ANY ({Param(value.Ni.Where(x => x != StringNullParameter.Null).ToArray())})))");
            else
                _filters.Add($"NOT ({column} = ANY ({Param(value.Ni)}))");
        }

        return this;
    }

    public SqlBuilder Where(string column, Int32Parameter? value)
    {
        if (value == null) return this;

        if (value.Eq != null)
            _filters.Add($"{column} = {Param(value.Eq)}");

        if (value.Ne != null)
            _filters.Add($"{column} != {Param(value.Ne)}");

        if (value.Gt != null)
            _filters.Add($"{column} > {Param(value.Gt)}");

        if (value.Ge != null)
            _filters.Add($"{column} >= {Param(value.Ge)}");

        if (value.Lt != null)
            _filters.Add($"{column} < {Param(value.Lt)}");

        if (value.Le != null)
            _filters.Add($"{column} <= {Param(value.Le)}");

        if (value.In != null)
            _filters.Add($"{column} = ANY ({Param(value.In)})");

        if (value.Ni != null)
            _filters.Add($"NOT ({column} = ANY ({Param(value.Ni)}))");

        return this;
    }

    public SqlBuilder Where(string column, Int64Parameter? value)
    {
        if (value == null) return this;

        if (value.Eq != null)
            _filters.Add($"{column} = {Param(value.Eq)}");

        if (value.Ne != null)
            _filters.Add($"{column} != {Param(value.Ne)}");

        if (value.Gt != null)
            _filters.Add($"{column} > {Param(value.Gt)}");

        if (value.Ge != null)
            _filters.Add($"{column} >= {Param(value.Ge)}");

        if (value.Lt != null)
            _filters.Add($"{column} < {Param(value.Lt)}");

        if (value.Le != null)
            _filters.Add($"{column} <= {Param(value.Le)}");

        if (value.In != null)
            _filters.Add($"{column} = ANY ({Param(value.In)})");

        if (value.Ni != null)
            _filters.Add($"NOT ({column} = ANY ({Param(value.Ni)}))");

        return this;
    }

    public SqlBuilder Where(string column, JsonParameter? json)
    {
        if (json == null) return this;
        
        string isNull(JsonPath[] path)
        {
            if (path.Length == 0)
                return $"{column} IS NULL";

            if (path.Any(x => x.Type == JsonPathType.Any))
                return $"{column} @> {Param(JsonPath.Merge(path, "null"))}::jsonb";

            return $"({column} IS NOT NULL AND {column} #>> {Param(JsonPath.Select(path))} IS NULL)";
        }

        string isNotNull(JsonPath[] path)
        {
            if (path.Length == 0)
                return $"{column} IS NOT NULL";

            if (path.Any(x => x.Type == JsonPathType.Any))
                return $"NOT ({column} @> {Param(JsonPath.Merge(path, "null"))}::jsonb)";

            return $"({column} IS NOT NULL AND {column} #>> {Param(JsonPath.Select(path))} IS NOT NULL)";
        }

        if (json.Eq != null)
        {
            foreach (var (path, value) in json.Eq)
            {
                if (value == JsonParameter.Null)
                {
                    _filters.Add(isNull(path));
                }
                else
                {
                    _filters.Add($"{column} @> {Param(JsonPath.Merge(path, value))}::jsonb");
                    if (path.Any(x => x.Type == JsonPathType.Index))
                        _filters.Add($"{column} #> {Param(JsonPath.Select(path))} = {Param(value)}::jsonb");
                }
            }
        }

        if (json.Ne != null)
        {
            foreach (var (path, value) in json.Ne)
            {
                if (value == JsonParameter.Null)
                {
                    _filters.Add(isNotNull(path));
                }
                else
                {
                    _filters.Add(path.Any(x => x.Type == JsonPathType.Any)
                        ? $"NOT ({column} @> {Param(JsonPath.Merge(path, value))}::jsonb)"
                        : $"NOT ({column} #> {Param(JsonPath.Select(path))} = {Param(value)}::jsonb)");
                }
            }
        }

        if (json.Gt != null)
        {
            foreach (var (path, value) in json.Gt)
            {
                if (value != JsonParameter.Null)
                {
                    var val = Param(value);
                    var fld = $"{column} #>> {Param(JsonPath.Select(path))}";
                    var len = $"greatest(length({fld}), length({val}))";
                    _filters.Add(Regexes.Number().IsMatch(value)
                        ? $"lpad({fld}, {len}, '0') > lpad({val}, {len}, '0')"
                        : $"{fld} > {val}");
                }
            }
        }

        if (json.Ge != null)
        {
            foreach (var (path, value) in json.Ge)
            {
                if (value != JsonParameter.Null)
                {
                    var val = Param(value);
                    var fld = $"{column} #>> {Param(JsonPath.Select(path))}";
                    var len = $"greatest(length({fld}), length({val}))";
                    _filters.Add(Regexes.Number().IsMatch(value)
                        ? $"lpad({fld}, {len}, '0') >= lpad({val}, {len}, '0')"
                        : $"{fld} >= {val}");
                }
            }
        }

        if (json.Lt != null)
        {
            foreach (var (path, value) in json.Lt)
            {
                if (value != JsonParameter.Null)
                {
                    var val = Param(value);
                    var fld = $"{column} #>> {Param(JsonPath.Select(path))}";
                    var len = $"greatest(length({fld}), length({val}))";
                    _filters.Add(Regexes.Number().IsMatch(value)
                        ? $"lpad({fld}, {len}, '0') < lpad({val}, {len}, '0')"
                        : $"{fld} < {val}");
                }
            }
        }

        if (json.Le != null)
        {
            foreach (var (path, value) in json.Le)
            {
                if (value != JsonParameter.Null)
                {
                    var val = Param(value);
                    var fld = $"{column} #>> {Param(JsonPath.Select(path))}";
                    var len = $"greatest(length({fld}), length({val}))";
                    _filters.Add(Regexes.Number().IsMatch(value)
                        ? $"lpad({fld}, {len}, '0') <= lpad({val}, {len}, '0')"
                        : $"{fld} <= {val}");
                }
            }
        }

        if (json.As != null)
        {
            foreach (var (path, value) in json.As)
            {
                if (value != JsonParameter.Null)
                {
                    _filters.Add($"{column} #>> {Param(JsonPath.Select(path))} ILIKE {Param(value)}");
                }
            }
        }

        if (json.Un != null)
        {
            foreach (var (path, value) in json.Un)
            {
                if (value != JsonParameter.Null)
                {
                    _filters.Add($"NOT ({column} #>> {Param(JsonPath.Select(path))} ILIKE {Param(value)})");
                }
            }
        }

        if (json.In != null)
        {
            foreach (var (path, values) in json.In)
            {
                var sqls = new List<string>(values.Length);
                foreach (var value in values)
                {
                    if (value == JsonParameter.Null)
                    {
                        sqls.Add(isNull(path));
                    }
                    else
                    {
                        var sql = $"{column} @> {Param(JsonPath.Merge(path, value))}::jsonb";
                        if (path.Any(x => x.Type == JsonPathType.Index))
                            sql += $" AND {column} #> {Param(JsonPath.Select(path))} = {Param(value)}::jsonb";
                        sqls.Add(sql);
                    }
                }
                _filters.Add($"({string.Join(" OR ", sqls)})");
            }
        }

        if (json.Ni != null)
        {
            foreach (var (path, values) in json.Ni)
            {
                foreach (var value in values)
                {
                    if (value == JsonParameter.Null)
                    {
                        _filters.Add(isNotNull(path));
                    }
                    else
                    {
                        _filters.Add(path.Any(x => x.Type == JsonPathType.Any)
                            ? $"NOT ({column} @> {Param(JsonPath.Merge(path, value))}::jsonb)"
                            : $"NOT ({column} #> {Param(JsonPath.Select(path))} = {Param(value)}::jsonb)");
                    }
                }
            }
        }

        return this;
    }

    public SqlBuilder Where(string column, DateTimeParameter? value)
    {
        if (value == null) return this;

        if (value.Eq != null)
            _filters.Add($"{column} = {Param(value.Eq)}");

        if (value.Ne != null)
            _filters.Add($"{column} != {Param(value.Ne)}");

        if (value.Gt != null)
            _filters.Add($"{column} > {Param(value.Gt)}");

        if (value.Ge != null)
            _filters.Add($"{column} >= {Param(value.Ge)}");

        if (value.Lt != null)
            _filters.Add($"{column} < {Param(value.Lt)}");

        if (value.Le != null)
            _filters.Add($"{column} <= {Param(value.Le)}");

        if (value.In != null)
            _filters.Add($"{column} = ANY ({Param(value.In)})");

        if (value.Ni != null)
            _filters.Add($"NOT ({column} = ANY ({Param(value.Ni)}))");

        return this;
    }

    public SqlBuilder Where(string column, ExpressionParameter? value)
    {
        if (value == null) return this;

        if (value.Eq != null)
            _filters.Add($"{column} = {Param(value.Eq)}");

        if (value.Ne != null)
            _filters.Add($"{column} != {Param(value.Ne)}");

        if (value.In != null)
            _filters.Add($"{column} = ANY ({Param(value.In)})");

        if (value.Ni != null)
            _filters.Add($"NOT ({column} = ANY ({Param(value.Ni)}))");

        return this;
    }

    public SqlBuilder Where(string column, MichelsonBlockHashParameter? value)
    {
        if (value == null) return this;

        if (value.Eq != null)
            _filters.Add($"{column} = {Param(value.Eq)}");

        if (value.Ne != null)
            _filters.Add($"{column} != {Param(value.Ne)}");

        if (value.In != null)
            _filters.Add($"{column} = ANY ({Param(value.In)})");

        if (value.Ni != null)
            _filters.Add($"NOT ({column} = ANY ({Param(value.Ni)}))");

        return this;
    }

    public SqlBuilder Where(string column, HexBytesParameter? value)
    {
        if (value == null) return this;

        if (value.Eq != null)
            _filters.Add($"{column} = {Param(value.Eq)}");

        if (value.Ne != null)
            _filters.Add($"{column} != {Param(value.Ne)}");

        if (value.In != null)
            _filters.Add($"{column} = ANY ({Param(value.In)})");

        if (value.Ni != null)
            _filters.Add($"NOT ({column} = ANY ({Param(value.Ni)}))");

        return this;
    }

    public SqlBuilder Where(string column, HashParameter? value)
    {
        if (value == null) return this;

        if (value.Eq != null)
            _filters.Add($"{column} = {Param(value.Eq)}");

        if (value.Ne != null)
            _filters.Add($"{column} != {Param(value.Ne)}");

        if (value.In != null)
            _filters.Add($"{column} = ANY ({Param(value.In)})");

        if (value.Ni != null)
            _filters.Add($"NOT ({column} = ANY ({Param(value.Ni)}))");

        return this;
    }

    public SqlBuilder Where(string column, BlockHashParameter? value)
    {
        if (value == null) return this;

        if (value.Eq != null)
            _filters.Add($"{column} = {Param(value.Eq)}");

        if (value.Ne != null)
            _filters.Add($"{column} != {Param(value.Ne)}");

        if (value.In != null)
            _filters.Add($"{column} = ANY ({Param(value.In)})");

        if (value.Ni != null)
            _filters.Add($"NOT ({column} = ANY ({Param(value.Ni)}))");

        return this;
    }

    public SqlBuilder Where(string column, ProtocolHashParameter? value)
    {
        if (value == null) return this;

        if (value.Eq != null)
            _filters.Add($"{column} = {Param(value.Eq)}");

        if (value.Ne != null)
            _filters.Add($"{column} != {Param(value.Ne)}");

        if (value.In != null)
            _filters.Add($"{column} = ANY ({Param(value.In)})");

        if (value.Ni != null)
            _filters.Add($"NOT ({column} = ANY ({Param(value.Ni)}))");

        return this;
    }

    public SqlBuilder Where(string column, AddressHashParameter? value)
    {
        if (value == null) return this;

        if (value.Eq != null)
            _filters.Add($"{column} = {Param(value.Eq)}");

        if (value.Ne != null)
            _filters.Add($"{column} != {Param(value.Ne)}");

        if (value.In != null)
            _filters.Add($"{column} = ANY ({Param(value.In)})");

        if (value.Ni != null)
            _filters.Add($"NOT ({column} = ANY ({Param(value.Ni)}))");

        return this;
    }

    public SqlBuilder Where(string column, OperationHashParameter? value, string type = "text")
    {
        if (value == null) return this;

        if (value.Eq != null)
            _filters.Add($"{column} = {Param(value.Eq)}::{type}");

        if (value.Ne != null)
            _filters.Add($"{column} != {Param(value.Ne)}::{type}");

        if (value.In != null)
            _filters.Add($"{column} = ANY ({Param(value.In)}::{type}[])");

        if (value.Ni != null)
            _filters.Add($"NOT ({column} = ANY ({Param(value.Ni)}::{type}[]))");

        return this;
    }

    public SqlBuilder Where(string column, DepositTypeParameter? value)
    {
        if (value == null) return this;

        if (value.Eq != null)
            _filters.Add($"{column} = {Param(value.Eq)}");

        if (value.Ne != null)
            _filters.Add($"{column} != {Param(value.Ne)}");

        return this;
    }

    public SqlBuilder Where(string column, TokenStandardParameter? value)
    {
        if (value == null) return this;

        if (value.Eq != null)
        {
            var p = Param(value.Eq);
            _filters.Add($"({column} & {p}) = {p}");
        }

        if (value.Ne != null)
        {
            var p = Param(value.Ne);
            _filters.Add($"({column} & {p}) != {p}");
        }

        return this;
    }

    public SqlBuilder Where(string column, MigrationKindParameter? value)
    {
        if (value == null) return this;

        if (value.Eq != null)
            _filters.Add($"{column} = {Param(value.Eq)}");

        if (value.Ne != null)
            _filters.Add($"{column} != {Param(value.Ne)}");

        if (value.In != null)
            _filters.Add($"{column} = ANY ({Param(value.In)})");

        if (value.Ni != null)
            _filters.Add($"NOT ({column} = ANY ({Param(value.Ni)}))");

        return this;
    }

    public SqlBuilder Where(string column, BigMapActionParameter? value)
    {
        if (value == null) return this;

        if (value.Eq != null)
            _filters.Add($"{column} = {Param(value.Eq)}");

        if (value.Ne != null)
            _filters.Add($"{column} != {Param(value.Ne)}");

        if (value.In != null)
            _filters.Add($"{column} = ANY ({Param(value.In)})");

        if (value.Ni != null)
            _filters.Add($"NOT ({column} = ANY ({Param(value.Ni)}))");

        return this;
    }

    public SqlBuilder Where(string column, BigMapTagsParameter? value)
    {
        if (value == null) return this;

        if (value.Eq != null)
        {
            var p = Param(value.Eq);
            _filters.Add($"({column} & {p}) = {p}");
        }

        if (value.Ne != null)
        {
            var p = Param(value.Ne);
            _filters.Add($"({column} & {p}) != {p}");
        }

        if (value.Any != null)
            _filters.Add($"({column} & {Param(value.Any)}) != 0");

        if (value.All != null)
        {
            var p = Param(value.All);
            _filters.Add($"({column} & {p}) = {p}");
        }

        return this;
    }

    public SqlBuilder Where(string column, bool? value)
    {
        if (value == null) return this;

        _filters.Add($"{column} = {Param(value.Value)}");

        return this;
    }

    public SqlBuilder Where(string column, Utf8BytesParameter? value)
    {
        if (value == null) return this;

        if (value.Eq != null)
            _filters.Add($"{column} = {Param(value.Eq)}");

        if (value.Ne != null)
            _filters.Add($"{column} != {Param(value.Ne)}");

        if (value.In != null)
            _filters.Add($"{column} = ANY ({Param(value.In)})");

        if (value.Ni != null)
            _filters.Add($"NOT ({column} = ANY ({Param(value.Ni)}))");

        return this;
    }

    public SqlBuilder Where(string column, MichelineParameter? value)
    {
        if (value == null) return this;

        if (value.Eq != null)
            _filters.Add($"{column} = {Param(value.Eq.ToBytes())}");

        if (value.Ne != null)
            _filters.Add($"{column} != {Param(value.Ne.ToBytes())}");

        return this;
    }

    public SqlBuilder Where(string column, AddressTypeParameter? value)
    {
        if (value == null) return this;

        if (value.Eq != null)
            _filters.Add($"{column} = {Param(value.Eq)}");

        if (value.Ne != null)
            _filters.Add($"{column} != {Param(value.Ne)}");

        if (value.In != null)
            _filters.Add($"{column} = ANY ({Param(value.In)})");

        if (value.Ni != null)
            _filters.Add($"NOT ({column} = ANY ({Param(value.Ni)}))");

        return this;
    }

    public SqlBuilder Where(string column, OperationStatusParameter? value)
    {
        if (value == null) return this;

        if (value.Eq != null)
            _filters.Add($"{column} = {Param(value.Eq)}");

        if (value.Ne != null)
            _filters.Add($"{column} != {Param(value.Ne)}");

        if (value.In != null)
            _filters.Add($"{column} = ANY ({Param(value.In)})");

        if (value.Ni != null)
            _filters.Add($"NOT ({column} = ANY ({Param(value.Ni)}))");

        return this;
    }

    public SqlBuilder Where(string column, StringParameter? value)
    {
        if (value == null) return this;

        if (value.Eq != null)
            _filters.Add($"{column} = {Param(value.Eq)}");

        if (value.Ne != null)
            _filters.Add($"{column} != {Param(value.Ne)}");

        if (value.In != null)
            _filters.Add($"{column} = ANY ({Param(value.In)})");

        if (value.Ni != null)
            _filters.Add($"NOT ({column} = ANY ({Param(value.Ni)}))");

        return this;
    }

    public SqlBuilder Where(string column, LayerParameter? value)
    {
        if (value == null) return this;

        if (value.Eq != null)
            _filters.Add($"{column} = {Param(value.Eq)}");

        if (value.Ne != null)
            _filters.Add($"{column} != {Param(value.Ne)}");

        return this;
    }

    public SqlBuilder Where(string column, RuntimeParameter? value)
    {
        if (value == null) return this;

        if (value.Eq != null)
            _filters.Add($"{column} = {Param(value.Eq)}");

        if (value.Ne != null)
            _filters.Add($"{column} != {Param(value.Ne)}");

        return this;
    }

    public SqlBuilder OrderBy(SortParameter? sort, SortSpec spec)
    {
        if (sort?.Cols.Count > 0)
        {
            foreach (var (field, asc) in sort.Cols)
            {
                if (!spec.TryGetValue(field, out var item))
                    throw new BadRequestException(nameof(Pagination.Sort), $"Sort by {field} is not allowed. Allowed fields: {string.Join(", ", spec.Keys)}");

                _sorting.Add((field, item.column, asc));
            }

            if (sort.Cols[^1].field != spec.PrimaryKey)
                _sorting.Add((spec.PrimaryKey, spec[spec.PrimaryKey].column, sort.Cols[^1].asc));
        }
        else
        {
            _sorting.Add((spec.PrimaryKey, spec[spec.PrimaryKey].column, true));
        }
        return this;
    }

    public SqlBuilder Cursor(CursorParameter? cursor, SortSpec spec)
    {
        if (cursor?.Cols?.Count > 0)
        {
            if (cursor.Cols.Count > _sorting.Count)
                throw new BadRequestException(nameof(Pagination.Cursor), "Cursor must match sort");

            var values = new List<(object Value, string Type)>(cursor.Cols.Count);
            for (int i = 0; i < cursor.Cols.Count; i++)
            {
                var (field, _, _) = _sorting[i];
                var (_, type) = spec[field];
                var value = type switch
                {
                    "integer" => int.TryParse(cursor.Cols[i], out var _value) ? (object)_value : throw new BadRequestException(nameof(Pagination.Cursor), "Invalid cursor value"),
                    "bigint" => long.TryParse(cursor.Cols[i], out var _value) ? (object)_value : throw new BadRequestException(nameof(Pagination.Cursor), "Invalid cursor value"),
                    "numeric" => BigInteger.TryParse(cursor.Cols[i], out var _value) ? (object)_value : throw new BadRequestException(nameof(Pagination.Cursor), "Invalid cursor value"),
                    "timestamptz" => DateTimeOffset.TryParse(cursor.Cols[i], out var _value) ? (object)_value.UtcDateTime : throw new BadRequestException(nameof(Pagination.Cursor), "Invalid cursor value"),
                    _ => cursor.Cols[i],
                };
                values.Add((value, type));
            }

            string BuildFilter(List<(object, string)> cursor, int i)
            {
                var (_, c, a) = _sorting[i];
                var (v, t) = values[i];
                var o = a ? ">" : "<";
                var p = Param(v);

                return ++i < values.Count
                    ? $"({c} {o} {p}::{t} OR {c} = {p}::{t} and {BuildFilter(cursor, i)})"
                    : $"{c} {o} {p}::{t}";
            }

            var asc = _sorting[0].asc;
            if (values.Count == 1 || _sorting.Take(values.Count).All(x => x.asc == asc))
            {
                var cols = string.Join(", ", _sorting.Take(values.Count).Select(x => x.column));
                var vals = string.Join(", ", values.Select(x => $"{Param(x.Value)}::{x.Type}"));
                _filters.Add($"({cols}) {(asc ? ">" : "<")} ({vals})");
            }
            else
            {
                _filters.Add(BuildFilter(values, 0));
            }
        }
        return this;
    }

    public SqlBuilder Offset(int offset)
    {
        _offset = offset;
        return this;
    }

    public SqlBuilder Limit(int limit)
    {
        _limit = limit;
        return this;
    }

    public (string, DynamicParameters) Build()
    {
        return (Build(0), _params);
    }

    string Build(int padding)
    {
        var select = _cols.Count != 0
            ? string.Join(", ", _cols)
            : "*";

        var from = _from switch
        {
            SqlBuilder[] subqueries => $"""
                (
                {string.Join($"\n\n{Pad(padding + 4)}UNION ALL\n\n", subqueries.Select(x => x.Build(padding + 4)))}
                {Pad(padding)})
                """,
            SqlBuilder subquery => $"""
                (
                {subquery.Build(padding + 4)}
                {Pad(padding)})
                """,
            string table => table,
            _ => throw new InvalidOperationException()
        };

        if (_fromAlias != null)
            from += $" AS {_fromAlias}";

        var sql = $"""
            {Pad(padding)}SELECT {select}
            {Pad(padding)}FROM {from}
            """;

        if (_joins.Count != 0)
            sql += $"\n{string.Join('\n', _joins.Select(x => $"{Pad(padding)}{x}"))}";

        if (_filters.Count != 0)
            sql += $"\n{Pad(padding)}WHERE {string.Join($"\n{Pad(padding)}AND ", _filters)}";

        if (_sorting.Count != 0)
            sql += $"\n{Pad(padding)}ORDER BY {string.Join(", ", _sorting.Select(x => x.column + (x.asc ? " ASC" : " DESC")))}";

        if (_offset != 0)
            sql += $"\n{Pad(padding)}OFFSET {_offset}";

        if (_limit != 0)
            sql += $"\n{Pad(padding)}LIMIT {_limit}";

        return sql;
    }

    string Param(object value)
    {        
        if (_root != null)
        {
            var name = $"@p{_root._counter++}";
            _root._params.Add(name, value);
            return name;
        }
        else
        {
            var name = $"@p{_counter++}";
            _params.Add(name, value);
            return name;
        }
    }

    static string Pad(int size) => new(' ', size);
}
