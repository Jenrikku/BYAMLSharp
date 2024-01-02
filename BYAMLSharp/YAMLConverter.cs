using System.Globalization;
using System.Text;

namespace BYAMLSharp;

public static class YAMLConverter
{
    /// <summary>
    /// Writes a YAML from a BYAML.
    /// </summary>
    /// <param name="indentation">The number of spaces that will be added per indentation.</param>
    /// <returns>A string representing the YAML.</returns>
    public static string FromBYAML(in BYAML byaml, bool writePreamble = true, uint indentation = 2)
    {
        StringBuilder builder = new();
        uint currentIndentation = 0;

        if (writePreamble)
        {
            builder.AppendLine("# BYAML");

            builder.Append($"Version: {byaml.Version}");

            if (byaml.IsMKBYAML)
                builder.Append("-MK");

            builder.AppendLine();

            builder.AppendLine("ByteOrder: " + (byaml.IsBigEndian ? "BigEndian" : "LittleEndian"));
            builder.AppendLine("Encoding: " + byaml.Encoding.WebName);
            builder.AppendLine("Root:");

            currentIndentation += indentation;
        }

        WriteNode(byaml.RootNode, builder, indentation, ref currentIndentation);

        return builder.ToString();
    }

    private static void WriteNode(
        BYAMLNode node,
        StringBuilder builder,
        uint indentation,
        ref uint currentIndentation
    )
    {
        switch (node.NodeType)
        {
            case BYAMLNodeType.Array:
                var array = node.GetValueAs<BYAMLNode[]>()!;

                WriteNodeArray(array, builder, indentation, ref currentIndentation);
                break;

            case BYAMLNodeType.Dictionary:
                var dict = node.GetValueAs<Dictionary<string, BYAMLNode>>()!;

                WriteNodeDictionary(dict, builder, indentation, ref currentIndentation);
                break;

            case BYAMLNodeType.BinaryOrPath:
                if (node.Value is byte[] data)
                    WriteBinaryData(data, builder);
                else if (node.Value is BYAMLMKPathPoint[] path)
                    WritePath(path, builder, indentation, ref currentIndentation);

                break;

            default:
                WriteScalarNode(node.Value, builder);
                break;
        }
    }

    private static void WriteNodeArray(
        BYAMLNode[] array,
        StringBuilder builder,
        uint indentation,
        ref uint currentIndentation
    )
    {
        currentIndentation += indentation;

        foreach (BYAMLNode child in array)
        {
            int hyphenPos = (int)(builder.Length + currentIndentation - indentation);
            bool isNotCollection = !IsCollection(child);

            if (isNotCollection)
                WriteIndentation(builder, currentIndentation);

            WriteNode(child, builder, indentation, ref currentIndentation);

            if (isNotCollection)
                builder.AppendLine();

            builder[hyphenPos] = '-';
        }

        currentIndentation -= indentation;
    }

    private static void WriteNodeDictionary(
        Dictionary<string, BYAMLNode> dictionary,
        StringBuilder builder,
        uint indentation,
        ref uint currentIndentation
    )
    {
        foreach (var (key, value) in dictionary)
        {
            WriteIndentation(builder, currentIndentation);

            builder.Append(key + ':');

            if (IsCollection(value))
            {
                builder.AppendLine();

                currentIndentation += indentation;

                WriteNode(value, builder, indentation, ref currentIndentation);

                currentIndentation -= indentation;
            }
            else
            {
                builder.Append(' ');

                if (value.Value is byte[] data)
                    WriteBinaryData(data, builder);
                else
                    WriteScalarNode(value.Value, builder);

                builder.AppendLine();
            }
        }
    }

    private static void WriteBinaryData(byte[] data, StringBuilder builder)
    {
        builder.Append("!x ");

        for (int i = 0; i < data.Length; i++)
        {
            byte b = data[i];
            string byteStr = b.ToString("X2");

            builder.Append(byteStr);
        }
    }

    private static void WritePath(
        BYAMLMKPathPoint[] path,
        StringBuilder builder,
        uint indentation,
        ref uint currentIndentation
    )
    {
        currentIndentation += indentation;

        foreach (BYAMLMKPathPoint point in path)
        {
            int hyphenPos = (int)(builder.Length + currentIndentation - indentation);

            WriteIndentation(builder, currentIndentation);
            builder.Append("Position: [ ");
            builder.Append(point.Position.X);
            builder.Append(", ");
            builder.Append(point.Position.Y);
            builder.Append(", ");
            builder.Append(point.Position.Z);
            builder.Append(" ]");
            builder.AppendLine();

            WriteIndentation(builder, currentIndentation);
            builder.Append("Normal: [ ");
            builder.Append(point.Normal.X);
            builder.Append(", ");
            builder.Append(point.Normal.Y);
            builder.Append(", ");
            builder.Append(point.Normal.Z);
            builder.Append(" ]");
            builder.AppendLine();

            WriteIndentation(builder, currentIndentation);
            builder.Append("Unknown: ");
            builder.Append(point.Unknown);
            builder.AppendLine();

            builder[hyphenPos] = '-';
        }

        currentIndentation -= indentation;
    }

    private static void WriteScalarNode(object? value, StringBuilder builder)
    {
        bool needsQuotes = value is string str && StringNeedsQuotes(str);

        // null
        if (value is null)
        {
            builder.Append("null");
            return;
        }

        // Tags:
        switch (value)
        {
            case object o when o is float:
                builder.Append("!f ");
                break;
            case object o when o is uint:
                builder.Append("!u ");
                break;
            case object o when o is long:
                builder.Append("!l ");
                break;
            case object o when o is ulong:
                builder.Append("!ul ");
                break;
            case object o when o is double:
                builder.Append("!d ");
                break;
        }

        // Start with quotes if needed.
        if (needsQuotes)
            builder.Append('"');

        // Special formating for floating point values:
        if (value is float f)
            value = f.ToString("g", CultureInfo.InvariantCulture);

        if (value is double d)
            value = d.ToString("g", CultureInfo.InvariantCulture);

        // Append the actual value.
        builder.Append(value);

        // End with quotes if needed.
        if (needsQuotes)
            builder.Append('"');
    }

    private static bool StringNeedsQuotes(string str)
    {
        bool res = false;

        res |= bool.TryParse(str, out _); // D0
        res |= int.TryParse(str, out _); // D1
        res |= float.TryParse(str, out _); // D2
        res |= uint.TryParse(str, out _); // D3
        res |= long.TryParse(str, out _); // D4
        res |= ulong.TryParse(str, out _); // D5
        res |= double.TryParse(str, out _); // D6

        res |= str.Equals("null", StringComparison.InvariantCultureIgnoreCase); // FF

        res |= string.IsNullOrEmpty(str);

        return res;
    }

    private static void WriteIndentation(StringBuilder builder, uint currentIndentation)
    {
        for (uint i = 0; i < currentIndentation; i++)
            builder.Append(' ');
    }

    private static bool IsCollection(BYAMLNode node) =>
        node.IsNodeCollection()
        || (node.NodeType == BYAMLNodeType.BinaryOrPath && node.Value is not byte[]);
}
