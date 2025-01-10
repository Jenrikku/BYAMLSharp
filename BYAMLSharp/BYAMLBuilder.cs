namespace BYAMLSharp;

/// <summary>
/// A class that provides methods to build a <see cref="BYAML"/> easily.
/// </summary>
public class BYAMLBuilder
{
    // The root node of the BYAML it is being built.
    private readonly BYAMLNode _rootNode;

    // The previous nodes that are returned to when the current node has been finished.
    private readonly List<BYAMLNode> _previousNodes;

    // The node that is currently being built. All methods will write to it.
    private BYAMLNode _currentNode;

    // See BYAML.IsMKBYAML
    private bool _isMK;

    /// <param name="rootAsArray">
    /// If set to true, the root of the builder will be an array.
    /// Otherwise, the root will be a dictionary.
    /// </param>
    public BYAMLBuilder(bool rootAsArray = false)
    {
        _rootNode = new(rootAsArray ? BYAMLNodeType.Array : BYAMLNodeType.Dictionary);
        _previousNodes = new();
        _currentNode = _rootNode;
        _isMK = false;
    }

    /// <summary>
    /// Adds a <see cref="string"/> to the current node.
    /// </summary>
    /// <param name="key">Only used when the current node is a dictionary.</param>
    public void AddScalarNode(string value, string? key = null) =>
        AddNode(new(BYAMLNodeType.String) { Value = value }, key);

    /// <summary>
    /// Adds a <see cref="bool"/> to the current node.
    /// </summary>
    /// <param name="key">Only used when the current node is a dictionary.</param>
    public void AddScalarNode(bool value, string? key = null) =>
        AddNode(new(BYAMLNodeType.Bool) { Value = value }, key);

    /// <summary>
    /// Adds an <see cref="int"/> to the current node.
    /// </summary>
    /// <param name="key">Only used when the current node is a dictionary.</param>
    public void AddScalarNode(int value, string? key = null) =>
        AddNode(new(BYAMLNodeType.Int) { Value = value }, key);

    /// <summary>
    /// Adds a <see cref="float"/> to the current node.
    /// </summary>
    /// <param name="key">Only used when the current node is a dictionary.</param>
    public void AddScalarNode(float value, string? key = null) =>
        AddNode(new(BYAMLNodeType.Float) { Value = value }, key);

    /// <summary>
    /// Adds an <see cref="uint"/> to the current node.
    /// </summary>
    /// <param name="key">Only used when the current node is a dictionary.</param>
    public void AddScalarNode(uint value, string? key = null) =>
        AddNode(new(BYAMLNodeType.UInt) { Value = value }, key);

    /// <summary>
    /// Adds a <see cref="long"/> to the current node.
    /// </summary>
    /// <param name="key">Only used when the current node is a dictionary.</param>
    public void AddScalarNode(long value, string? key = null) =>
        AddNode(new(BYAMLNodeType.Int64) { Value = value }, key);

    /// <summary>
    /// Adds an <see cref="ulong"/> to the current node.
    /// </summary>
    /// <param name="key">Only used when the current node is a dictionary.</param>
    public void AddScalarNode(ulong value, string? key = null) =>
        AddNode(new(BYAMLNodeType.UInt64) { Value = value }, key);

    /// <summary>
    /// Adds a <see cref="double"/> to the current node.
    /// </summary>
    /// <param name="key">Only used when the current node is a dictionary.</param>
    public void AddScalarNode(double value, string? key = null) =>
        AddNode(new(BYAMLNodeType.Double) { Value = value }, key);

    /// <summary>
    /// Adds a path to the current node.<br />
    /// A path is an array of <see cref="BYAMLMKPathPoint"/>.
    /// </summary>
    /// <param name="key">Only used when the current node is a dictionary.</param>
    public void AddPath(BYAMLMKPathPoint[] path, string? key = null)
    {
        _isMK = true;

        AddNode(new(BYAMLNodeType.BinaryOrPath, _isMK) { Value = path }, key);
    }

    /// <summary>
    /// Adds a <see cref="byte"/> array to the current node.
    /// </summary>
    /// <param name="key">Only used when the current node is a dictionary.</param>
    public void AddBinaryData(byte[] value, string? key = null) =>
        AddNode(new(BYAMLNodeType.BinaryOrPath) { Value = value }, key);

    /// <summary>
    /// Adds a <see cref="null"/> value to the current node.
    /// </summary>
    /// <param name="key">Only used when the current node is a dictionary.</param>
    public void AddNullNode(string? key = null) => AddNode(new(BYAMLNodeType.Null), key);

    /// <summary>
    /// Sets the current node to a new array.<br />
    /// All operations will be done on this new node.
    /// </summary>
    /// <param name="key">Only used when the parent node is a dictionary.</param>
    public void BeginNewArray(string? key = null)
    {
        BYAMLNode newNode = new(BYAMLNodeType.Array);

        AddNode(newNode, key);

        _previousNodes.Add(_currentNode);
        _currentNode = newNode;
    }

    /// <summary>
    /// Sets the current node to a new dictionary.<br />
    /// All operations will be done on this new node.
    /// </summary>
    /// <param name="key">Only used when the parent node is a dictionary.</param>
    public void BeginNewDictionary(string? key = null)
    {
        BYAMLNode newNode = new(BYAMLNodeType.Dictionary);

        AddNode(newNode, key);

        _previousNodes.Add(_currentNode);
        _currentNode = newNode;
    }

    /// <summary>
    /// Ends the construction of the current node and returns to the previous one.
    /// </summary>
    /// <returns>Whether or not there was a previous node to return to.</returns>
    public bool EndCurrentNode()
    {
        int count = _previousNodes.Count;

        if (count <= 0)
            return false;

        _currentNode = _previousNodes[count - 1];

        _previousNodes.RemoveAt(count - 1);

        return true;
    }

    /// <summary>
    /// Creates a new <see cref="BYAML"/> from the added contents.
    /// </summary>
    public BYAML ToBYAML() => new(_rootNode, isMKBYAML: _isMK);

    public void AddNode(BYAMLNode node, string? key)
    {
        switch (_currentNode.NodeType)
        {
            case BYAMLNodeType.Array:
                var array = _currentNode.GetValueAs<BYAMLNode[]>()!;

                Array.Resize(ref array, array.Length + 1);

                array[^1] = node;

                _currentNode.Value = array;
                break;

            case BYAMLNodeType.Dictionary:
                var dictionary = _currentNode.GetValueAs<Dictionary<string, BYAMLNode>>()!;

                if (string.IsNullOrEmpty(key))
                    throw new(
                        "The key must not be null or empty when adding a node to a dictionary."
                    );

                dictionary.Add(key, node);
                break;

            default:
                throw new("Incorrect node type found while building.");
        }
    }
}
