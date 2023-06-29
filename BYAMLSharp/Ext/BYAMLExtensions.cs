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
}
