namespace BYAMLSharp.Ext;

public static class BYAMLExtensions
{
    public static BYAMLNodeIterator? AsIterator(this BYAML byaml) =>
        byaml.RootNode is not null ? new(byaml.RootNode) : null;

    public static BYAMLNodeIterator IterateFromHere(this BYAMLNode node) => new(node);

    public static IEnumerable<BYAMLNode> SearchFromHere(
        this BYAMLNode node,
        params BYAMLNodeType[] types
    ) => BYAMLNodeFinder.Search(node, types);

    /// <returns>null if the node is not a dictionary, otherwise it converts all nodes recursively to objects.</returns>
    public static Dictionary<string, object?>? AsObjectDictionary(this BYAMLNode node) =>
        new BYAMLNodeConverter(node).ToDictionary();

    /// <returns>null if the node is not a list, otherwise it converts all nodes recursively to objects.</returns>
    public static List<object?>? AsObjectList(this BYAMLNode node) =>
        new BYAMLNodeConverter(node).ToList();
}
