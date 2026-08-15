using System.Data;
using System.Globalization;
using System.Numerics;
using Dapper;
using Npgsql;
using NpgsqlTypes;

namespace Xtzkt.Api;

public class BigIntegerTypeHandler : SqlMapper.TypeHandler<BigInteger>
{
    public override void SetValue(IDbDataParameter parameter, BigInteger value)
    {
        if (parameter is NpgsqlParameter npg)
            npg.NpgsqlDbType = NpgsqlDbType.Numeric;
        parameter.Value = value;
    }

    public override BigInteger Parse(object value) => value switch
    {
        BigInteger b => b,
        string s => BigInteger.Parse(s, CultureInfo.InvariantCulture),
        _ => throw new NotSupportedException("Cannot parse BigInteger from this type"),
    };
}
