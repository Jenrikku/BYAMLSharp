using System.Numerics;

namespace BYAMLSharp;

public struct BYAMLMKPathPoint
{
    public BYAMLMKPathPoint() => Normal = Vector3.UnitY;

    public Vector3 Position;
    public Vector3 Normal;
    public uint Unknown;
}
