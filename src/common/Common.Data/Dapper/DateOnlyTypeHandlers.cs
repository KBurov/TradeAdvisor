using System.Data;
using System.Globalization;

using Dapper;

namespace Common.Data.Dapper;

public sealed class DateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly>
{
    public override void SetValue(IDbDataParameter parameter, DateOnly value)
    {
        parameter.DbType = DbType.Date;
        parameter.Value = value.ToDateTime(TimeOnly.MinValue);
    }

    public override DateOnly Parse(object value) => value switch
    {
        DateTime dt => DateOnly.FromDateTime(dt),
        string s => DateOnly.Parse(s, CultureInfo.InvariantCulture),
        _ => throw new DataException($"Cannot convert {value?.GetType().Name ?? "null"} to DateOnly")
    };
}
