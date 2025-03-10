namespace OsmTool;

public static class DictUtils
{
    private static readonly char[] DICT_SEPARATORS = [';', '|', '#', '@', '~', '!'];


    public static string ArrayToString(this string[] list)
    {
        if (list == null || list.Length == 0)
        {
            return "";
        }

        char? separator = null;
        foreach (var sep in DICT_SEPARATORS)
        {
            bool hasSeparator = false;
            foreach (var part in list)
            {
                if (part.Contains(sep))
                {
                    hasSeparator = true;
                    break;
                }
            }
            if (!hasSeparator)
            {
                separator = sep;
                break;
            }
        }

        if (separator == null)
        {
            throw new Exception("No suitable separator found for list");
        }

        return $"{separator}{string.Join(separator.ToString(), list)}";
    }

    public static string[] StringToArray(this string str)
    {
        if (str == null || str.Length <= 1)
        {
            return [];
        }

        char separator = str[0];

        string[] parts = str.Substring(1).Split(separator);
        return parts;
    }



    public static string DictToString(this Dictionary<string, string> dict)
    {
        if (dict == null || dict.Count == 0)
        {
            return "";
        }
        List<string> parts = new List<string>();
        foreach (var kv in dict)
        {
            parts.Add(kv.Key);
            parts.Add(kv.Value);
        }

        char? separator = null;
        foreach (var sep in DICT_SEPARATORS)
        {
            bool hasSeparator = false;
            foreach (var part in parts)
            {
                if (part.Contains(sep))
                {
                    hasSeparator = true;
                    break;
                }
            }
            if (!hasSeparator)
            {
                separator = sep;
                break;
            }
        }

        if (separator == null)
        {
            throw new Exception("No suitable separator found for dictionary");
        }

        return $"{separator}{string.Join(separator.ToString(), parts)}";
    }

    public static Dictionary<string, string> StringToDict(this string str)
    {
        if (str == null || str.Length <= 1)
        {
            return new Dictionary<string, string>();
        }

        char separator = str[0];

        string[] parts = str.Substring(1).Split(separator);
        if (parts.Length % 2 != 0)
        {
            throw new Exception($"Invalid dictionary string: '{str}'");
        }

        Dictionary<string, string> dict = new Dictionary<string, string>();
        for (int i = 0; i < parts.Length; i += 2)
        {
            dict[parts[i]] = parts[i + 1];
        }

        return dict;
    }

    public static bool TryGetValueFromKeys<K, V>(this Dictionary<K, V> dict, IEnumerable<K> keys, out V? value) where K : notnull
    {
        foreach (var k in keys)
        {
            if (dict.TryGetValue(k, out V? kValue))
            {
                value = kValue;
                return true;
            }
        }
        value = default;
        return false;
    }
}