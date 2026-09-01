using System.Numerics;

namespace CsProtocol;

public readonly record struct BlockPosition(int X, int Y, int Z);
public readonly record struct ChunkPosition(int X, int Z);
public readonly record struct PacketHeader(uint PacketId, byte SenderSubClientId = 0, byte TargetSubClientId = 0)
{
    public const uint MaxPacketId = 0x3ff;
    private const uint MaxEncodedValue = 0x3fff;

    public uint Encode()
    {
        if (PacketId > MaxPacketId) throw new ProtocolException($"Packet ID {PacketId} exceeds {MaxPacketId}.");
        if (SenderSubClientId > 3 || TargetSubClientId > 3) throw new ProtocolException("Sub-client IDs must be in the range 0..3.");
        return PacketId | ((uint)SenderSubClientId << 10) | ((uint)TargetSubClientId << 12);
    }

    public static PacketHeader Decode(uint value)
    {
        if (value > MaxEncodedValue) throw new ProtocolException($"Packet header {value} contains reserved bits.");
        return new(value & MaxPacketId, (byte)((value >> 10) & 3), (byte)((value >> 12) & 3));
    }
}

public static class BedrockVectors
{
    public static Vector3 SoundPosition(BlockPosition value) => new(value.X / 8f, value.Y / 8f, value.Z / 8f);
}
