using System.Text;

namespace BYAMLSharp;

public struct BYAML
{
    public BYAML()
    {
        IsMKBYAML = false;
        Encoding = Encoding.UTF8;
    }

    public BYAML(BYAMLNode root, Encoding? encoding = null, bool isMKBYAML = false)
    {
        RootNode = root;
        Encoding = encoding ?? Encoding.UTF8;
        IsMKBYAML = isMKBYAML;
    }

    public BYAML(Encoding? encoding = null, bool isMKBYAML = false)
        : this(root: new(BYAMLNodeType.Null), encoding, isMKBYAML) { }

    public bool IsMKBYAML { get; internal set; }
    public bool IsBigEndian { get; set; } = false;

    public Encoding Encoding { get; set; }

    public const ushort Magic = 0x4259;

    public ushort Version { get; set; } = 1;

    public BYAMLNode DictionaryKeyTable { get; internal set; } = new(BYAMLNodeType.Null);
    public BYAMLNode StringTable { get; internal set; } = new(BYAMLNodeType.Null);
    public BYAMLNode PathTable { get; internal set; } = new(BYAMLNodeType.Null);
    public BYAMLNode RootNode { get; set; }
}
