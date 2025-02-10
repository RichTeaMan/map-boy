using Microsoft.Data.Sqlite;

public static class SqliteDataReaderUtils
{
    public static T? Val<T>(this SqliteDataReader r, int ord) where T : struct
    {
        var t = r.GetValue(ord);
        if (t == DBNull.Value)
        {
            return null;
        }
        return (T)t;
    }

        public static string? NullableString(this SqliteDataReader r, int ord)
    {
        var t = r.GetValue(ord);
        if (t == DBNull.Value)
        {
            return null;
        }
        return (string)t;
    }
}
