using System.Data;
using System.Globalization;

using Dapper;

namespace Common.Data.Dapper;

public sealed class NullableDateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly?>
{
    public override void SetValue(IDbDataParameter parameter, DateOnly? value)
    {
        parameter.DbType = DbType.Date;
        parameter.Value = value.HasValue ? value.Value.ToDateTime(TimeOnly.MinValue) : DBNull.Value;
    }

    public override DateOnly? Parse(object value)
    {
        if (value is null || value is DBNull) return null;
        return value switch
        {
            DateTime dt => DateOnly.FromDateTime(dt),
            string s => DateOnly.Parse(s, CultureInfo.InvariantCulture),
            _ => throw new DataException($"Cannot convert {value.GetType().Name} to DateOnly?")
        };
    }
}
