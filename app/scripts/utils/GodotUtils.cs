using Godot;
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;

public static class GodotUtils
{
    /// <summary>
    /// Hopefully very temporary measure to copy a C# object into a godot variant.
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static Variant ToGodotVariant(object obj)
    {
        if (obj == null)
        {
            return new Variant();
        }

        Type type = obj.GetType();

        // 1. Handle Primitives, Strings, and Godot Native Types
        if (obj is string)
        {
            return Variant.From(obj as string);
        }
        if (obj is DateTimeOffset)
        {
            return Variant.From(((DateTimeOffset)obj).ToString());
        }
        if (type.IsPrimitive)
        {
            if (type == typeof(long))
            {
                return Variant.From((long)obj);
            }
            if (type == typeof(int))
            {
                return Variant.From((int)obj);
            }
            if (type == typeof(double))
            {
                return Variant.From((double)obj);
            }
            if (type == typeof(float))
            {
                return Variant.From((float)obj);
            }
            if (type == typeof(bool))
            {
                return Variant.From((bool)obj);
            }
            else
            {
                throw new Exception($"Unknown type: {type}");
            }
        }
        if (obj is Variant)
        {
            return Variant.From((Variant)obj);
        }

        // 2. Handle Enums (Convert to int or string based on preference)
        if (type.IsEnum)
        {
            return (int)obj;
        }

        // 3. Handle Collections (Arrays, Lists)
        if (obj is IEnumerable enumerable)
        {
            var gArray = new Godot.Collections.Array();
            foreach (var item in enumerable)
            {
                gArray.Add(ToGodotVariant(item));
            }
            return gArray;
        }

        // 4. Handle Objects/Classes (Reflection)
        var gDict = new Godot.Collections.Dictionary();

        // Get all public instance properties
        PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var prop in properties)
        {
            if (prop.CanRead)
            {
                var godotVar = ToGodotVariant(prop.GetValue(obj));
                setGodotDict(gDict, prop.Name, godotVar);
            }
        }

        // Get all public instance fields
        FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
        foreach (var field in fields)
        {
            var godotVar = ToGodotVariant(field.GetValue(obj));
            setGodotDict(gDict, field.Name, godotVar);
        }

        return gDict;
    }

    private static void setGodotDict(Godot.Collections.Dictionary gDict, string name, Variant godotVar)
    {
        gDict[name] = godotVar;
        gDict[toSnakeCase(name)] = godotVar;
        string lowerStart = $"{name.First().ToString().ToLower()}{new string(name.Skip(1).ToArray())}";
        gDict[lowerStart] = godotVar;
    }

    private static string toSnakeCase(string name)
    {
        string result = name;
        foreach (var c in name.ToCharArray().Where(c => char.IsAsciiLetterUpper(c) || char.IsAsciiDigit(c)))
        {
            result = result.Replace(c.ToString(), $"_{c}");
        }
        return result.ToLower().Trim('_');
    }
}
