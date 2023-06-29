using System.Collections;

namespace BYAMLSharp.Ext;

public class BYAMLNodeIterator : IEnumerable<(BYAMLNode Node, BYAMLNodeIterInfo Info)>
{
    private readonly bool _isInvalid = false;
    private readonly BYAMLNode _root;
    private readonly BYAMLNodeIterInfo _rootInfo;

    /// <summary>
    /// A list used to take track of any recursive values.
    /// </summary>
    private readonly List<int> _seenHashes = new();

    public BYAMLNodeIterator(BYAMLNode root)
    {
        if (!root.IsNodeCollection())
        {
            _isInvalid = true;
            _root = new(BYAMLNodeType.Null);
            _rootInfo = new();
            return;
        }

        _root = root;
        _rootInfo = new(root, _seenHashes);
    }

    public IEnumerator<(BYAMLNode Node, BYAMLNodeIterInfo Info)> GetEnumerator()
    {
        if (_isInvalid)
            yield break;

        foreach ((BYAMLNode, BYAMLNodeIterInfo) tuple in EnumerateNode(_root, _rootInfo))
            yield return tuple;
    }

    private IEnumerable<(BYAMLNode, BYAMLNodeIterInfo)> EnumerateNode(
        BYAMLNode node,
        BYAMLNodeIterInfo info
    )
    {
        if (!node.IsNodeCollection())
            yield break;

        if (node.NodeType == BYAMLNodeType.Array)
        {
            BYAMLNode[] array = node.GetValueAs<BYAMLNode[]>()!;

            foreach (BYAMLNode entry in array)
            {
                BYAMLNodeIterInfo entryInfo =
                    new(entry, _seenHashes) { Parent = node, ParentInfo = info };

                yield return (entry, entryInfo);

                if (entry.IsNodeCollection())
                    foreach ((BYAMLNode, BYAMLNodeIterInfo) tuple in EnumerateNode(node, entryInfo))
                        yield return tuple;
            }
        }

        if (node.NodeType == BYAMLNodeType.Dictionary)
        {
            Dictionary<string, BYAMLNode> dict = node.GetValueAs<Dictionary<string, BYAMLNode>>()!;

            foreach (KeyValuePair<string, BYAMLNode> pair in dict)
            {
                BYAMLNode entry = pair.Value;

                BYAMLNodeIterInfo entryInfo =
                    new(entry, _seenHashes)
                    {
                        Parent = node,
                        ParentInfo = info,
                        Key = pair.Key
                    };

                yield return (entry, entryInfo);

                if (entry.IsNodeCollection())
                    foreach ((BYAMLNode, BYAMLNodeIterInfo) tuple in EnumerateNode(node, entryInfo))
                        yield return tuple;
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public class BYAMLNodeIterInfo
{
    internal BYAMLNodeIterInfo() { }

    internal BYAMLNodeIterInfo(BYAMLNode node, List<int> seenHashes)
    {
        if (node.IsNodeCollection())
        {
            Hash = node.Value!.GetHashCode();

            if (seenHashes.Contains(Hash))
                IsRecursion = true;
            else
                seenHashes.Add(Hash);
        }
    }

    /// <summary>
    /// The HashCode of the node's value. (Only applicable to node collections)
    /// </summary>
    public int Hash;
    public BYAMLNode? Parent;
    public BYAMLNodeIterInfo? ParentInfo;

    /// <summary>
    /// Tells whether this entry has appeared before. (Only applicable to node collections)
    /// </summary>
    public bool IsRecursion = false;

    /// <summary>
    /// If the parent node is a dictionary, this is the key the child has in it.
    /// </summary>
    public string Key = string.Empty;

    public bool IsRoot => Parent is null;
    public BYAMLNodeType ParentType => Parent is null ? BYAMLNodeType.Null : Parent.NodeType;
}
