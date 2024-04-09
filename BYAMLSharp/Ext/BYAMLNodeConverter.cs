using System.Diagnostics;

namespace BYAMLSharp.Ext;

public class BYAMLNodeConverter
{
    private BYAMLNode _root;
    private Dictionary<BYAMLNode, object> _convertedCollectionNodes = new();

    public BYAMLNodeConverter(BYAMLNode rootNode) => _root = rootNode;

    public Dictionary<string, object?>? ToDictionary() => ToDictionary(_root);

    private Dictionary<string, object?>? ToDictionary(BYAMLNode node)
    {
        if (node.NodeType != BYAMLNodeType.Dictionary)
            return null;

        if (_convertedCollectionNodes.TryGetValue(node, out object? foundDuplicate))
        {
            Debug.Assert(foundDuplicate is Dictionary<string, object?>);
            return (Dictionary<string, object?>)foundDuplicate;
        }

        Dictionary<string, object?> result = new();

        _convertedCollectionNodes.Add(node, result);

        var dict = node.GetValueAs<Dictionary<string, BYAMLNode>>()!;

        foreach (var (key, value) in dict)
        {
            object? converted = ConvertNode(value);

            result.Add(key, converted);
        }

        return result;
    }

    public List<object?>? ToList() => ToList(_root);

    private List<object?>? ToList(BYAMLNode node)
    {
        if (node.NodeType != BYAMLNodeType.Array)
            return null;

        if (_convertedCollectionNodes.TryGetValue(node, out object? foundDuplicate))
        {
            Debug.Assert(foundDuplicate is List<object?>);
            return (List<object?>)foundDuplicate;
        }

        List<object?> result = new();

        _convertedCollectionNodes.Add(node, result);

        var array = node.GetValueAs<BYAMLNode[]>()!;

        foreach (BYAMLNode child in array)
        {
            object? converted = ConvertNode(child);

            result.Add(converted);
        }

        return result;
    }

    private object? ConvertNode(BYAMLNode node)
    {
        return node.NodeType switch
        {
            BYAMLNodeType.String => node.GetValueAs<string>(),
            BYAMLNodeType.Dictionary => ToDictionary(node),
            BYAMLNodeType.Array => ToList(node),
            BYAMLNodeType.Bool => node.GetValueAs<bool>(),
            BYAMLNodeType.Int => node.GetValueAs<int>(),
            BYAMLNodeType.Float => node.GetValueAs<float>(),
            BYAMLNodeType.UInt => node.GetValueAs<uint>(),
            BYAMLNodeType.Int64 => node.GetValueAs<long>(),
            BYAMLNodeType.UInt64 => node.GetValueAs<ulong>(),
            BYAMLNodeType.Double => node.GetValueAs<double>(),
            _ => null
        };
    }
}
