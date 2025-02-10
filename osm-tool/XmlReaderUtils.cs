using System.Xml;

public static class XmlReaderUtils {

    public static Nullable<T> GetAttributeNullableValue<T>(this XmlReader reader, string name) where T : struct
    {
        var value = reader.GetAttribute(name);
        if (value == null) {
            return null;
        }
        return (Nullable<T>)Convert.ChangeType(value, typeof(T));
    }
}
