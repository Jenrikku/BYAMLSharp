using System.Text;

namespace BYAMLSharp;

public struct BYAML
{
    public BYAML(bool isMKBYAML = false) => IsMKBYAML = isMKBYAML;

    public bool IsMKBYAML { get; internal set; }
    public bool IsBigEndian { get; set; } = false;

    public Encoding Encoding { get; set; } = Encoding.UTF8;

    public const ushort Magic = 0x4259;

    public uint Version { get; set; } = 1;

    public BYAMLNode? DictionaryKeyTable { get; internal set; }
    public BYAMLNode? StringTable { get; internal set; }
    public BYAMLNode? PathTable { get; internal set; }
    public BYAMLNode? RootNode { get; set; }
}
