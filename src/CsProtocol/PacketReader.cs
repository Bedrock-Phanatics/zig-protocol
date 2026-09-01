using System;
using System.Buffers.Binary;
using System.Numerics;
using System.Text;
using CsNbt;

namespace CsProtocol;

/// <summary>Bounds-checked, allocation-conscious reader over one contiguous packet payload.</summary>
public ref struct PacketReader
{
    private ReadOnlySpan<byte> _remaining;
    private readonly ProtocolLimits _limits;

    public PacketReader(ReadOnlySpan<byte> payload, ProtocolLimits? limits = null)
    {
        _remaining = payload;
        _limits = limits ?? ProtocolLimits.Default;
        _limits.Validate();
    }

    internal PacketReader(ReadOnlySpan<byte> payload, ProtocolLimits limits, bool limitsValidated)
    {
        System.Diagnostics.Debug.Assert(limitsValidated);
        _remaining = payload;
        _limits = limits;
    }

    public readonly int Remaining => _remaining.Length;
    public readonly bool End => _remaining.IsEmpty;
    public readonly ReadOnlySpan<byte> UnreadSpan => _remaining;

    public byte ReadByte()
    {
        Ensure(1);
        byte value = _remaining[0];
        _remaining = _remaining[1..];
        return value;
    }

    public sbyte ReadSByte() => unchecked((sbyte)ReadByte());

    public bool ReadBoolean() => ReadByte() switch
    {
        0 => false,
        1 => true,
        byte value => throw new ProtocolException($"Invalid Boolean byte {value}; expected 0 or 1.")
    };

    public short ReadInt16() { short value = BinaryPrimitives.ReadInt16LittleEndian(Take(2)); return value; }
    public ushort ReadUInt16() { ushort value = BinaryPrimitives.ReadUInt16LittleEndian(Take(2)); return value; }
    public int ReadInt32() { int value = BinaryPrimitives.ReadInt32LittleEndian(Take(4)); return value; }
    public uint ReadUInt32() { uint value = BinaryPrimitives.ReadUInt32LittleEndian(Take(4)); return value; }
    public long ReadInt64() { long value = BinaryPrimitives.ReadInt64LittleEndian(Take(8)); return value; }
    public ulong ReadUInt64() { ulong value = BinaryPrimitives.ReadUInt64LittleEndian(Take(8)); return value; }
    public float ReadSingle() => BitConverter.Int32BitsToSingle(ReadInt32());
    public double ReadDouble() => BitConverter.Int64BitsToDouble(ReadInt64());
    public int ReadInt32BigEndian() { int value = BinaryPrimitives.ReadInt32BigEndian(Take(4)); return value; }

    public uint ReadVarUInt32()
    {
        uint value = 0;
        for (int index = 0; index < 5; index++)
        {
            byte current = ReadByte();
            if (index == 4 && (current & 0xf0) != 0) throw new ProtocolException("VarUInt32 overflow.");
            value |= (uint)(current & 0x7f) << (index * 7);
            if ((current & 0x80) == 0)
            {
                if (index != 0 && current == 0) throw new ProtocolException("Non-canonical VarUInt32 encoding.");
                return value;
            }
        }
        throw new ProtocolException("VarUInt32 exceeds 5 bytes.");
    }

    public ulong ReadVarUInt64()
    {
        ulong value = 0;
        for (int index = 0; index < 10; index++)
        {
            byte current = ReadByte();
            if (index == 9 && (current & 0xfe) != 0) throw new ProtocolException("VarUInt64 overflow.");
            value |= (ulong)(current & 0x7f) << (index * 7);
            if ((current & 0x80) == 0)
            {
                if (index != 0 && current == 0) throw new ProtocolException("Non-canonical VarUInt64 encoding.");
                return value;
            }
        }
        throw new ProtocolException("VarUInt64 exceeds 10 bytes.");
    }

    public int ReadVarInt32()
    {
        uint value = ReadVarUInt32();
        return unchecked((int)(value >> 1) ^ -unchecked((int)(value & 1)));
    }

    public long ReadVarInt64()
    {
        ulong value = ReadVarUInt64();
        return unchecked((long)(value >> 1) ^ -unchecked((long)(value & 1)));
    }

    public string ReadString()
    {
        int length = ReadBoundedLength(ReadVarUInt32(), _limits.MaxStringBytes, "string");
        ReadOnlySpan<byte> bytes = Take(length);
        try { return ProtocolEncoding.Utf8.GetString(bytes); }
        catch (DecoderFallbackException exception) { throw new ProtocolException("String contains invalid UTF-8.", exception); }
    }

    public ReadOnlySpan<byte> ReadByteSpan()
    {
        int length = ReadBoundedLength(ReadVarUInt32(), _limits.MaxByteArrayBytes, "byte array");
        return Take(length);
    }

    public byte[] ReadByteArray() => ReadByteSpan().ToArray();

    public Vector2 ReadVector2() => new(ReadSingle(), ReadSingle());
    public Vector3 ReadVector3() => new(ReadSingle(), ReadSingle(), ReadSingle());
    public BlockPosition ReadBlockPosition() => new(ReadVarInt32(), ReadVarInt32(), ReadVarInt32());
    public ChunkPosition ReadChunkPosition() => new(ReadVarInt32(), ReadVarInt32());

    public Guid ReadGuid()
    {
        ReadOnlySpan<byte> source = Take(16);
        Span<byte> canonical = stackalloc byte[16];
        for (int i = 0; i < 8; i++) canonical[i] = source[7 - i];
        for (int i = 0; i < 8; i++) canonical[i + 8] = source[15 - i];
        return new Guid(canonical, bigEndian: true);
    }

    public NbtDocument ReadNbt(NbtOptions? options = null)
    {
        var stream = new ReadOnlySpanStream(_remaining.ToArray());
        try
        {
            NbtDocument document = new NbtReader(stream, options ?? NbtOptions.BedrockNetwork).ReadDocument();
            _remaining = _remaining[checked((int)stream.Position)..];
            return document;
        }
        catch (Exception exception) when (exception is not ProtocolException)
        {
            throw new ProtocolException("Invalid NBT payload.", exception);
        }
    }

    public int ReadCollectionLength(string fieldName = "collection") => ReadBoundedLength(ReadVarUInt32(), _limits.MaxCollectionElements, fieldName);

    public ReadOnlySpan<byte> Take(int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        Ensure(length);
        ReadOnlySpan<byte> value = _remaining[..length];
        _remaining = _remaining[length..];
        return value;
    }

    public readonly void EnsureFullyConsumed(string packetName)
    {
        if (!_remaining.IsEmpty) throw new ProtocolException($"{packetName} has {_remaining.Length} trailing byte(s).");
    }

    private readonly void Ensure(int length)
    {
        if (_remaining.Length < length) throw new ProtocolException($"Truncated payload: requested {length} byte(s), only {_remaining.Length} remain.");
    }

    private static int ReadBoundedLength(uint value, int maximum, string name)
    {
        if (value > int.MaxValue || value > (uint)maximum) throw new ProtocolException($"{name} length {value} exceeds limit {maximum}.");
        return (int)value;
    }
}
