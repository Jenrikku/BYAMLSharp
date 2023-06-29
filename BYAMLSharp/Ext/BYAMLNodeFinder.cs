namespace BYAMLSharp.Ext;

public static class BYAMLNodeFinder
{
    public static IEnumerable<BYAMLNode> Search(BYAMLNode node, params BYAMLNodeType[] type)
    {
        if (!node.IsNodeCollection())
            yield break;

        foreach (var (subNode, _) in node.IterateFromHere())
            if (type.Contains(subNode.NodeType))
                yield return subNode;
    }
}
