using System.Text;
using BYAMLSharp.Ext;
using BYAMLSharp.Utils;
using static BYAMLSharp.Utils.ValueParser;

namespace BYAMLSharp;

public static class BYAMLParser
{
    private static readonly Dictionary<uint, BYAMLNode> s_byRefNodes = new();

    private static readonly Dictionary<object, uint> s_byRefWrittenValues = new(); // Value, ValuePos
    private static readonly Dictionary<uint, object> s_byRefValues = new(); // NodePos, Value

    public static unsafe BYAML Read(ReadOnlySpan<byte> data, Encoding? encoding = null)
    {
        if (data.IsEmpty)
            return new(encoding);

        fixed (byte* ptr = data)
            return Read(ptr, encoding);
    }

    public static unsafe byte[] Write(BYAML byaml)
    {
        GenerateTables(
            byaml.RootNode,
            byaml.IsMKBYAML,
            out BYAMLNode keyTableNode,
            out BYAMLNode strTableNode,
            out BYAMLNode pathTableNode
        );

        byaml.DictionaryKeyTable = keyTableNode;
        byaml.StringTable = strTableNode;
        byaml.PathTable = pathTableNode;

        uint size = CalculateTotalSize(in byaml);
        byte[] data = new byte[size];

        fixed (byte* ptr = data)
            Write(byaml, ptr);

        return data;
    }

    private static unsafe BYAML Read(byte* start, Encoding? encoding = null)
    {
        BYAML byaml = new(encoding);

        byte* ptr = start;

        ushort magic = ReadValue<ushort>(ref ptr, false);

        byaml.IsBigEndian = magic != BYAML.Magic;

        bool diffByteOrder = BitConverter.IsLittleEndian == byaml.IsBigEndian;

        byaml.Version = ReadValue<ushort>(ref ptr, diffByteOrder);

        uint[] offsets = new uint[4];

        for (byte i = 0; i < 4; i++)
        {
            // MK's BYAML Check:
            if (i == 2)
            {
                uint offset = ReadValue<uint>(ref ptr, diffByteOrder);
                BYAMLNodeType type = ReadNodeTypeByRef(start, offset);

                ptr -= 4;

                if (type != BYAMLNodeType.PathTable)
                    continue;

                byaml.IsMKBYAML = true;
            }

            offsets[i] = ReadValue<uint>(ref ptr, diffByteOrder);
        }

        byaml.DictionaryKeyTable = ReadCollectionByRef(ref byaml, start, offsets[0], diffByteOrder);

        byaml.StringTable = ReadCollectionByRef(ref byaml, start, offsets[1], diffByteOrder);

        byaml.PathTable = ReadCollectionByRef(ref byaml, start, offsets[2], diffByteOrder);

        byaml.RootNode = ReadCollectionByRef(ref byaml, start, offsets[3], diffByteOrder);

        s_byRefNodes.Clear();

        return byaml;
    }

    private static unsafe void Write(BYAML byaml, byte* start)
    {
        byte* ptr = start;

        bool reverse = BitConverter.IsLittleEndian == byaml.IsBigEndian;

        WriteValue(ref ptr, BYAML.Magic, reverse);
        WriteValue(ref ptr, byaml.Version, reverse);

        if (byaml.RootNode.NodeType == BYAMLNodeType.Null)
            return;

        WriteNode(ref ptr, start, byaml.DictionaryKeyTable, in byaml, reverse);
        WriteNode(ref ptr, start, byaml.StringTable, in byaml, reverse);

        if (byaml.IsMKBYAML)
            WriteNode(ref ptr, start, byaml.PathTable, in byaml, reverse);

        WriteNode(ref ptr, start, byaml.RootNode, in byaml, reverse);

        while (s_byRefValues.Count > 0)
        {
            var (pos, value) = s_byRefValues.First();
            s_byRefValues.Remove(pos);

            if (s_byRefWrittenValues.TryGetValue(value, out uint valueOffset))
            {
                byte* valueptr = start + valueOffset;

                WriteNodeOffset(valueptr, start, pos, reverse);
            }
            else
            {
                s_byRefWrittenValues.Add(value, (uint)(ptr - start));

                WriteNodeRefValue(ref ptr, start, pos, value, in byaml, reverse);
            }
        }

        s_byRefWrittenValues.Clear();
        s_byRefValues.Clear();
    }

    private static unsafe BYAMLNode ReadNode(
        ref BYAML byaml,
        ref byte* ptr,
        byte* start,
        BYAMLNodeType type,
        bool reverse
    )
    {
        if ((byte)type >> 4 == 0xC) // Collections
        {
            uint offset = ReadValue<uint>(ref ptr, reverse);
            return ReadCollectionByRef(ref byaml, start, offset, reverse);
        }

        BYAMLNode node = new(type, byaml.IsMKBYAML);

        switch (type)
        {
            case BYAMLNodeType.String:
            {
                if (
                    byaml.StringTable is null
                    || byaml.StringTable.NodeType != BYAMLNodeType.StringTable
                )
                    break;

                string[] table = byaml.StringTable.GetValueAs<string[]>()!;
                uint index = ReadValue<uint>(ref ptr, reverse);

                if (table.Length <= index)
                    break;

                node.Value = table[index];
                break;
            }

            case BYAMLNodeType.BinaryOrPath:
            {
                if (byaml.IsMKBYAML)
                {
                    if (
                        byaml.PathTable is null
                        || byaml.PathTable.Value is not BYAMLMKPathPoint[][] table
                    )
                        break;

                    uint index = ReadValue<uint>(ref ptr, reverse);

                    node.Value = table[index];
                    break;
                }

                // Version 4+:
                uint offset = ReadValue<uint>(ref ptr, reverse);

                node.Value = ReadBinaryDataByRef(start, offset, reverse);
                break;
            }

            case BYAMLNodeType.Bool:
                node.Value = ReadValue<uint>(ref ptr, reverse) == 1;
                break;

            case BYAMLNodeType.Int:
                node.Value = ReadValue<int>(ref ptr, reverse);
                break;

            case BYAMLNodeType.Float:
                node.Value = ReadValue<float>(ref ptr, reverse);
                break;

            case BYAMLNodeType.UInt:
                node.Value = ReadValue<uint>(ref ptr, reverse);
                break;

            case BYAMLNodeType.Int64:
            {
                uint offset = ReadValue<uint>(ref ptr, reverse);
                node.Value = ReadNodeValueByRef<long>(start, offset, reverse);
                break;
            }

            case BYAMLNodeType.UInt64:
            {
                uint offset = ReadValue<uint>(ref ptr, reverse);
                node.Value = ReadNodeValueByRef<ulong>(start, offset, reverse);
                break;
            }

            case BYAMLNodeType.Double:
            {
                uint offset = ReadValue<uint>(ref ptr, reverse);
                node.Value = ReadNodeValueByRef<double>(start, offset, reverse);
                break;
            }

            case BYAMLNodeType.Null:
                ptr += 4;
                break;

            default:
                throw new NotSupportedException($"Unsupported node type: {(byte)type:X2}");
        }

        return node;
    }

    private static unsafe BYAMLNode ReadCollectionByRef(
        ref BYAML byaml,
        byte* start,
        uint offset,
        bool reverse
    )
    {
        if (offset < 16)
            return new(BYAMLNodeType.Null);

        byte* ptr = start + offset;

        BYAMLNodeType nodeType = ReadValue<BYAMLNodeType>(ref ptr, reverse);

        BYAMLNode? node = new(nodeType);

        if (!s_byRefNodes.TryAdd(offset, node))
            return s_byRefNodes[offset];

        ReadCollectionNode(ref byaml, ref node, ptr, start, reverse);

        return node;
    }

    private static unsafe void ReadCollectionNode(
        ref BYAML byaml,
        ref BYAMLNode node,
        byte* ptr,
        byte* start,
        bool reverse
    )
    {
        uint count = ReadValue<UInt24>(ref ptr, reverse).ToUInt32();

        switch (node.NodeType)
        {
            case BYAMLNodeType.Array:
            {
                BYAMLNode[] array = new BYAMLNode[count];
                BYAMLNodeType[] subTypes = ReadValues<BYAMLNodeType>(ref ptr, reverse, count);

                ulong relPos = (ulong)(ptr - start);

                // Align to 4 bytes:
                if (relPos % 4 != 0)
                {
                    ulong multiple = relPos / 4 + 1;
                    ptr = start + (4 * multiple);
                }

                for (int i = 0; i < subTypes.Length; i++)
                    array[i] = ReadNode(ref byaml, ref ptr, start, subTypes[i], reverse);

                node.Value = array;
                break;
            }

            case BYAMLNodeType.Dictionary:
            {
                Dictionary<string, BYAMLNode> dict = new();

                for (uint i = 0; i < count; i++)
                {
                    uint keyIndex = ReadValue<UInt24>(ref ptr, reverse).ToUInt32();

                    string key = string.Empty;

                    if (
                        byaml.DictionaryKeyTable is not null
                        && byaml.DictionaryKeyTable.Value is string[] table
                    )
                        key = table[(int)keyIndex];
                    else
                        key = keyIndex.ToString();

                    if (dict.ContainsKey(key))
                        continue;

                    BYAMLNodeType valueType = ReadValue<BYAMLNodeType>(ref ptr, reverse);
                    BYAMLNode value = ReadNode(ref byaml, ref ptr, start, valueType, reverse);

                    dict.Add(key, value);
                }

                node.Value = dict;
                break;
            }

            case BYAMLNodeType.StringTable:
            {
                string[] array = new string[count];

                byte* tableStart = ptr - 4;

                for (uint i = 0; i < count; i++)
                {
                    uint startOffset = ReadValue<uint>(ref ptr, reverse);
                    uint endOffset = ReadValue<uint>(ref ptr, reverse);

                    uint length = endOffset - startOffset - 1;

                    ptr -= 4;

                    byte* stringPos = tableStart + startOffset;

                    array[i] = byaml.Encoding.GetString(stringPos, (int)length);
                }

                node.Value = array;
                break;
            }

            case BYAMLNodeType.PathTable:
            {
                BYAMLMKPathPoint[][] array = new BYAMLMKPathPoint[count][];

                byte* tableStart = ptr - 4;

                for (uint i = 0; i < count; i++)
                {
                    uint startOffset = ReadValue<uint>(ref ptr, reverse);
                    uint endOffset = ReadValue<uint>(ref ptr, reverse);

                    ptr -= 4;

                    // sizeof(BYAMLMKPathPoint) == 28
                    uint pointCount = (endOffset - startOffset) / 28;

                    BYAMLMKPathPoint[] path = new BYAMLMKPathPoint[pointCount];
                    byte* pathptr = tableStart + startOffset;

                    for (uint j = 0; j < pointCount; j++)
                    {
                        BYAMLMKPathPoint point =
                            new()
                            {
                                Position = new(ReadValues<float>(ref pathptr, reverse, 3)),
                                Normal = new(ReadValues<float>(ref pathptr, reverse, 3)),
                                Unknown = ReadValue<uint>(ref pathptr, reverse)
                            };

                        path[j] = point;
                    }

                    array[i] = path;
                }

                node.Value = array;
                break;
            }
        }
    }

    private static unsafe BYAMLNodeType ReadNodeTypeByRef(byte* start, uint offset)
    {
        byte* ptr = start + offset;

        return ReadValue<BYAMLNodeType>(ref ptr, false);
    }

    private static unsafe T ReadNodeValueByRef<T>(byte* start, uint offset, bool reverse)
        where T : unmanaged
    {
        byte* ptr = start + offset;

        return ReadValue<T>(ref ptr, reverse);
    }

    private static unsafe byte[] ReadBinaryDataByRef(byte* start, uint offset, bool reverse)
    {
        byte* ptr = start + offset;

        int length = ReadValue<int>(ref ptr, reverse);

        return new Span<byte>(ptr, length).ToArray();
    }

    private static void GenerateTables(
        BYAMLNode root,
        bool isMKBYAML,
        out BYAMLNode keyTable,
        out BYAMLNode strTable,
        out BYAMLNode pathTable
    )
    {
        keyTable = new(BYAMLNodeType.Null);
        strTable = new(BYAMLNodeType.Null);
        pathTable = new(BYAMLNodeType.Null);

        if (!root.IsNodeCollection())
            return;

        IEnumerable<BYAMLNode> nodes;

        if (isMKBYAML)
            nodes = root.SearchFromHere(
                BYAMLNodeType.Dictionary,
                BYAMLNodeType.String,
                BYAMLNodeType.BinaryOrPath
            );
        else
            nodes = root.SearchFromHere(BYAMLNodeType.Dictionary, BYAMLNodeType.String);

        List<string> keys = new();
        List<string> strings = new();
        List<BYAMLMKPathPoint[]> paths = new();

        foreach (BYAMLNode node in nodes)
            switch (node.NodeType)
            {
                case BYAMLNodeType.Dictionary:
                    Dictionary<string, BYAMLNode> dict = (Dictionary<string, BYAMLNode>)node.Value!;

                    foreach (string key in dict.Keys)
                        if (!keys.Contains(key))
                            keys.Add(key);

                    break;

                case BYAMLNodeType.String:
                    string str = node.GetValueAs<string>()!;

                    if (!strings.Contains(str))
                        strings.Add(str);

                    break;

                case BYAMLNodeType.BinaryOrPath:
                    BYAMLMKPathPoint[] path = node.GetValueAs<BYAMLMKPathPoint[]>()!;

                    if (!paths.Contains(path))
                        paths.Add(path);

                    break;
            }

        if (keys.Count > 0)
        {
            keyTable = new(
                BYAMLNodeType.StringTable,
                keys.OrderBy(x => x, StringComparer.Ordinal).ToList().ToArray()
            );
        }

        if (strings.Count > 0)
        {
            strTable = new(
                BYAMLNodeType.StringTable,
                strings.OrderBy(x => x, StringComparer.Ordinal).ToList().ToArray()
            );
        }

        if (paths.Count > 0)
            pathTable = new(BYAMLNodeType.PathTable) { Value = paths.ToArray() };
    }

    private static uint CalculateTotalSize(in BYAML byaml)
    {
        HashSet<BYAMLNode> references = new();

        uint total = 16;

        if (byaml.IsMKBYAML)
            total += 4;

        if (byaml.DictionaryKeyTable is not null)
            total += CalculateNodeSize(byaml.DictionaryKeyTable, in byaml, references);

        if (byaml.StringTable is not null)
            total += CalculateNodeSize(byaml.StringTable, in byaml, references);

        if (byaml.PathTable is not null)
            total += CalculateNodeSize(byaml.PathTable, in byaml, references);

        if (byaml.RootNode is not null)
            total += CalculateNodeSize(byaml.RootNode, in byaml, references);

        return total;
    }

    private static uint CalculateNodeSize(
        BYAMLNode node,
        in BYAML byaml,
        HashSet<BYAMLNode> references
    )
    {
        uint size = 4;

        switch (node.Value)
        {
            case object v when v is Dictionary<string, BYAMLNode> dict:
                size += ((uint)dict.Count) * 8;

                foreach (BYAMLNode entry in dict.Values)
                {
                    if (!references.Add(entry))
                        continue;

                    size += CalculateNodeSize(entry, in byaml, references);
                }

                break;

            case object v when v is BYAMLNode[] array:
                size += (uint)array.Length;

                AlignTo4Bytes(ref size);

                size += (uint)array.Length * 4;

                foreach (BYAMLNode entry in array)
                {
                    if (!references.Add(entry))
                        continue;

                    size += CalculateNodeSize(entry, in byaml, references);
                }

                break;

            case object v when v is string[] strings:
                size += ((uint)strings.Length + 1) * 4;

                foreach (string entry in strings)
                    size += (uint)byaml.Encoding.GetByteCount(entry) + 1;

                AlignTo4Bytes(ref size);

                break;

            case object v when v is BYAMLMKPathPoint[][] pathTable:
                size += ((uint)pathTable.Length + 1) * 4;

                foreach (BYAMLMKPathPoint[] path in pathTable)
                    size += ((uint)path.Length) * 28;

                break;

            case object v when v is byte[] data:
                if (!references.Add(node))
                    return 0;

                size += (uint)data.Length;

                AlignTo4Bytes(ref size);
                break;

            case object v when v is long || v is ulong || v is double:
                if (!references.Add(node))
                    return 0;

                return 8;

            default:
                return 0;
        }

        return size;

        static void AlignTo4Bytes(ref uint size)
        {
            if (size % 4 == 0)
                return;

            uint last = size / 4;
            size = ++last * 4;
        }
    }

    private static unsafe void WriteNode(
        ref byte* ptr,
        byte* start,
        BYAMLNode? node,
        in BYAML byaml,
        bool reverse
    )
    {
        if (node is null)
            return;

        switch (node.Value)
        {
            case object v when v is string str:
                if (byaml.StringTable is null)
                {
                    ptr += 4;
                    break;
                }

                string[] strTable = byaml.StringTable.GetValueAs<string[]>()!;

                WriteValue(ref ptr, Array.IndexOf(strTable, str), reverse);
                break;

            case object v when v is BYAMLMKPathPoint[] path:
                if (byaml.PathTable is null)
                {
                    ptr += 4;
                    break;
                }

                BYAMLMKPathPoint[][] pathTable =
                    byaml.PathTable.GetValueAs<BYAMLMKPathPoint[][]>()!;

                WriteValue(ref ptr, Array.IndexOf(pathTable, path), reverse);
                break;

            case object v when v is bool cond:
                WriteValue(ref ptr, cond ? 1 : 0, reverse);
                break;

            case object v when v is int int32:
                WriteValue(ref ptr, int32, reverse);
                break;

            case object v when v is float single:
                WriteValue(ref ptr, single, reverse);
                break;

            case object v when v is uint uint32:
                WriteValue(ref ptr, uint32, reverse);
                break;

            case object v
                when v is Array
                    || v is Dictionary<string, BYAMLNode>
                    || v is long
                    || v is ulong
                    || v is double:

                s_byRefValues.Add((uint)(ptr - start), v);
                ptr += 4;
                break;

            case null:
                ptr += 4;
                break;
        }
    }

    private static unsafe void WriteNodeRefValue(
        ref byte* ptr,
        byte* start,
        uint offset,
        object value,
        in BYAML byaml,
        bool reverse
    )
    {
        WriteNodeOffset(ptr, start, offset, reverse);

        switch (value)
        {
            case object v when v is BYAMLNode[] array:
                WriteValue(ref ptr, BYAMLNodeType.Array, reverse);
                WriteValue(ref ptr, new UInt24(array.Length), reverse);

                byte* typesPtr = ptr;

                ptr += array.Length;
                AlignPtr(ref ptr, start, 4);

                foreach (BYAMLNode node in array)
                {
                    WriteValue(ref typesPtr, node.NodeType, reverse);

                    WriteNode(ref ptr, start, node, in byaml, reverse);
                }

                break;

            case object v when v is Dictionary<string, BYAMLNode> dict:
                if (byaml.DictionaryKeyTable is null)
                    break;

                string[] dictKeyTable = byaml.DictionaryKeyTable.GetValueAs<string[]>()!;

                WriteValue(ref ptr, BYAMLNodeType.Dictionary, reverse);
                WriteValue(ref ptr, new UInt24(dict.Count), reverse);

                foreach (KeyValuePair<string, BYAMLNode> pair in dict)
                {
                    int index = Array.IndexOf(dictKeyTable, pair.Key);

                    WriteValue(ref ptr, new UInt24(index), reverse);
                    WriteValue(ref ptr, pair.Value.NodeType, reverse);

                    WriteNode(ref ptr, start, pair.Value, in byaml, reverse);
                }

                break;

            case object v when v is string[] array:
                byte* strTableStart = ptr;
                byte* strOffsetsPtr = ptr + 4;

                WriteValue(ref ptr, BYAMLNodeType.StringTable, reverse);
                WriteValue(ref ptr, new UInt24(array.Length), reverse);

                ptr += (array.Length + 1) * 4;

                foreach (string str in array)
                {
                    WriteValue(ref strOffsetsPtr, (uint)(ptr - strTableStart), reverse);

                    WriteValues(ref ptr, byaml.Encoding.GetBytes(str), reverse);
                    WriteValue<byte>(ref ptr, 0, reverse);
                }

                WriteValue(ref strOffsetsPtr, (uint)(ptr - strTableStart), reverse);

                break;

            case object v when v is BYAMLMKPathPoint[][] array:
                byte* pathTableStart = ptr;
                byte* pathOffsetsPtr = ptr + 4;

                WriteValue(ref ptr, BYAMLNodeType.StringTable, reverse);
                WriteValue(ref ptr, new UInt24(array.Length), reverse);

                ptr += (array.Length + 1) * 4;

                foreach (BYAMLMKPathPoint[] path in array)
                {
                    WriteValue(ref pathOffsetsPtr, (uint)(ptr - pathTableStart), reverse);

                    foreach (BYAMLMKPathPoint point in path)
                    {
                        WriteValue(ref ptr, point.Position.X, reverse);
                        WriteValue(ref ptr, point.Position.Y, reverse);
                        WriteValue(ref ptr, point.Position.Z, reverse);

                        WriteValue(ref ptr, point.Normal.X, reverse);
                        WriteValue(ref ptr, point.Normal.Y, reverse);
                        WriteValue(ref ptr, point.Normal.Z, reverse);

                        WriteValue(ref ptr, point.Unknown, reverse);
                    }
                }

                WriteValue(ref pathOffsetsPtr, (uint)(ptr - pathTableStart), reverse);

                break;

            case object v when v is long int64:
                WriteValue(ref ptr, int64, reverse);
                return;

            case object v when v is ulong uint64:
                WriteValue(ref ptr, uint64, reverse);
                return;

            case object v when v is double num:
                WriteValue(ref ptr, num, reverse);
                return;

            case object v when v is byte[] data:
                WriteValue(ref ptr, data.Length, reverse);
                WriteValues(ref ptr, data, false);
                return;
        }

        AlignPtr(ref ptr, start, 4);
    }

    private static unsafe void WriteNodeOffset(byte* ptr, byte* start, uint offset, bool reverse)
    {
        byte* nodeptr = start + offset;
        WriteValue(ref nodeptr, (uint)(ptr - start), reverse);
    }

    private static unsafe void AlignPtr(ref byte* ptr, byte* start, uint value)
    {
        uint position = (uint)(ptr - start);

        if (position % value == 0)
            return;

        uint last = position / value;
        position = ++last * value;

        ptr = start + position;
    }
}
