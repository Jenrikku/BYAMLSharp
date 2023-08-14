namespace BYAMLSharp.Ext;

public static class BYAMLNodeFinder
{
    public static IEnumerable<BYAMLNode> Search(BYAMLNode node, params BYAMLNodeType[] type)
    {
        if (!node.IsNodeCollection())
            yield break;

        foreach (var (subNode, subNodeInfo) in node.IterateFromHere())
            if (type.Contains(subNode.NodeType) && !subNodeInfo.Duplicated)
                yield return subNode;
    }
}
