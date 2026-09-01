using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Numerics;
using CsNbt;

namespace CsProtocol;

public readonly ref struct PacketWriter(IBufferWriter<byte> output)
{
    private readonly IBufferWriter<byte> _output = output ?? throw new ArgumentNullException(nameof(output));
    public void WriteByte(byte value) { Span<byte> span = _output.GetSpan(1); span[0] = value; _output.Advance(1); }
    public void WriteSByte(sbyte value) => WriteByte(unchecked((byte)value));
    public void WriteBoolean(bool value) => WriteByte(value ? (byte)1 : (byte)0);
    public void WriteInt16(short value) { Span<byte> span = _output.GetSpan(2); BinaryPrimitives.WriteInt16LittleEndian(span, value); _output.Advance(2); }
    public void WriteUInt16(ushort value) { Span<byte> span = _output.GetSpan(2); BinaryPrimitives.WriteUInt16LittleEndian(span, value); _output.Advance(2); }
    public void WriteInt32(int value) { Span<byte> span = _output.GetSpan(4); BinaryPrimitives.WriteInt32LittleEndian(span, value); _output.Advance(4); }
    public void WriteUInt32(uint value) { Span<byte> span = _output.GetSpan(4); BinaryPrimitives.WriteUInt32LittleEndian(span, value); _output.Advance(4); }
    public void WriteInt64(long value) { Span<byte> span = _output.GetSpan(8); BinaryPrimitives.WriteInt64LittleEndian(span, value); _output.Advance(8); }
    public void WriteUInt64(ulong value) { Span<byte> span = _output.GetSpan(8); BinaryPrimitives.WriteUInt64LittleEndian(span, value); _output.Advance(8); }
    public void WriteSingle(float value) => WriteInt32(BitConverter.SingleToInt32Bits(value));
    public void WriteDouble(double value) => WriteInt64(BitConverter.DoubleToInt64Bits(value));
    public void WriteInt32BigEndian(int value) { Span<byte> span = _output.GetSpan(4); BinaryPrimitives.WriteInt32BigEndian(span, value); _output.Advance(4); }
    public void WriteVarUInt32(uint value) { Span<byte> span = _output.GetSpan(5); int count = VarInt.WriteUInt32(span, value); _output.Advance(count); }
    public void WriteVarUInt64(ulong value) { Span<byte> span = _output.GetSpan(10); int count = VarInt.WriteUInt64(span, value); _output.Advance(count); }
    public void WriteVarInt32(int value) => WriteVarUInt32(unchecked((uint)((value << 1) ^ (value >> 31))));
    public void WriteVarInt64(long value) => WriteVarUInt64(unchecked((ulong)((value << 1) ^ (value >> 63))));
    public void WriteString(string value) { ArgumentNullException.ThrowIfNull(value); int count = ProtocolEncoding.Utf8.GetByteCount(value); WriteVarUInt32((uint)count); Span<byte> span = _output.GetSpan(count); _output.Advance(ProtocolEncoding.Utf8.GetBytes(value, span)); }
    public void WriteByteSpan(ReadOnlySpan<byte> value) { WriteVarUInt32(checked((uint)value.Length)); WriteRaw(value); }
    public void WriteRaw(ReadOnlySpan<byte> value) { while (!value.IsEmpty) { Span<byte> span = _output.GetSpan(1); int count = Math.Min(span.Length, value.Length); value[..count].CopyTo(span); _output.Advance(count); value = value[count..]; } }
    public void WriteVector2(Vector2 value) { WriteSingle(value.X); WriteSingle(value.Y); }
    public void WriteVector3(Vector3 value) { WriteSingle(value.X); WriteSingle(value.Y); WriteSingle(value.Z); }
    public void WriteBlockPosition(BlockPosition value) { WriteVarInt32(value.X); WriteVarInt32(value.Y); WriteVarInt32(value.Z); }
    public void WriteChunkPosition(ChunkPosition value) { WriteVarInt32(value.X); WriteVarInt32(value.Z); }
    public void WriteNbt(NbtDocument document, NbtOptions? options = null) { using var stream = new BufferWriterStream(_output); new NbtWriter(stream, options ?? NbtOptions.BedrockNetwork).WriteDocument(document); }
}

internal static class VarInt
{
    internal static int WriteUInt32(Span<byte> destination, uint value) { int index = 0; while (value >= 0x80) { destination[index++] = (byte)(value | 0x80); value >>= 7; } destination[index++] = (byte)value; return index; }
    internal static int WriteUInt64(Span<byte> destination, ulong value) { int index = 0; while (value >= 0x80) { destination[index++] = (byte)(value | 0x80); value >>= 7; } destination[index++] = (byte)value; return index; }
}
