namespace BYAMLSharp;

public class BYAMLNode
{
    private object? _value;

    public BYAMLNode(BYAMLNodeType type, bool isMKBYAML = false)
    {
        NodeType = type;

        _value = type switch
        {
            BYAMLNodeType.String => string.Empty,
            BYAMLNodeType.BinaryOrPath
                => isMKBYAML ? Array.Empty<BYAMLMKPathPoint>() : Array.Empty<byte>(),

            BYAMLNodeType.Array => Array.Empty<BYAMLNode>(),
            BYAMLNodeType.Dictionary => new Dictionary<string, BYAMLNode>(),
            BYAMLNodeType.StringTable => Array.Empty<string>(),
            BYAMLNodeType.PathTable => Array.Empty<BYAMLMKPathPoint[]>(),

            BYAMLNodeType.Bool => false,
            BYAMLNodeType.Int => (int)0,
            BYAMLNodeType.Float => (float)0,
            BYAMLNodeType.UInt => (uint)0,
            BYAMLNodeType.Int64 => (long)0,
            BYAMLNodeType.UInt64 => (ulong)0,
            BYAMLNodeType.Double => (double)0,

            _ => null
        };
    }

    public BYAMLNode(BYAMLNodeType type, object? val, bool isMKBYAML = false)
    {
        NodeType = type;
        _value = val;
        if (GetType(val) != NodeType) throw new("BYAMLNodeType was incorrectly assigned!");
    }
    public BYAMLNode(object? val)
    {
        NodeType = GetType(val);
        _value = val;
    }
    public BYAMLNode(Dictionary<string, BYAMLNode> val)
    {
        NodeType = BYAMLNodeType.Dictionary;
        _value = val.OrderBy(x => x.Key, StringComparer.Ordinal).ToDictionary();
    }
    private BYAMLNodeType GetType(object? val)
    {
        return val switch
        {
            string => BYAMLNodeType.String,
            BYAMLMKPathPoint[] => BYAMLNodeType.BinaryOrPath,
            byte[] => BYAMLNodeType.BinaryOrPath,
            BYAMLNode[] => BYAMLNodeType.Array,
            Dictionary<string, BYAMLNode> => BYAMLNodeType.Dictionary,
            string[] => BYAMLNodeType.StringTable,
            BYAMLMKPathPoint[][] => BYAMLNodeType.PathTable,
            bool => BYAMLNodeType.Bool,
            int => BYAMLNodeType.Int,
            float => BYAMLNodeType.Float,
            uint => BYAMLNodeType.UInt,
            long => BYAMLNodeType.Int64,
            ulong => BYAMLNodeType.UInt64,
            double => BYAMLNodeType.Double,
            _ => BYAMLNodeType.Null
        };
    }
    public BYAMLNodeType NodeType { get; }

    public object? Value
    {
        get => _value;
        set
        {
            if (_value is null)
                throw new("The value could not be set as it was null.");

            if (value is null)
                throw new("The value cannot be set to null.");

            if (_value.GetType() == value.GetType())
                _value = value;
            else
                throw new("The value could not be set as the types mismatch.");
        }
    }

    public T? GetValueAs<T>() => (T?)_value;

    public bool TryGetValueAs<T>(out T? value)
    {
        if (_value is not null && _value.GetType() == typeof(T))
        {
            value = (T?)_value;
            return true;
        }
        else
        {
            value = default;
            return false;
        }
    }

    public bool TrySetValue(object value)
    {
        if (_value is null)
            return false;

        if (_value.GetType() == value.GetType())
            _value = value;
        else
            return false;

        return true;
    }

    ///<returns>Whether this Node is a collection of other nodes.
    ///(Either a dictionary or an array)</returns>
    public bool IsNodeCollection() =>
        NodeType == BYAMLNodeType.Dictionary || NodeType == BYAMLNodeType.Array;
}

public enum BYAMLNodeType : byte
{
    String = 0xA0,
    BinaryOrPath = 0xA1,
    Array = 0xC0,
    Dictionary = 0xC1,
    StringTable = 0xC2,
    PathTable = 0xC3,
    Bool = 0xD0,
    Int = 0xD1,
    Float = 0xD2,
    UInt = 0xD3,
    Int64 = 0xD4,
    UInt64 = 0xD5,
    Double = 0xD6,
    Null = 0xFF
}
