using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Frozen;

namespace CsProtocol;

public interface IBedrockPacket { }
public sealed record RawPacket(uint PacketId, ReadOnlyMemory<byte> Payload) : IBedrockPacket;
public readonly record struct PacketEnvelope(PacketHeader Header, IBedrockPacket Packet);
public delegate IBedrockPacket UntypedPacketDecoder(ref PacketReader reader);
public delegate void UntypedPacketEncoder(ref PacketWriter writer, IBedrockPacket packet);
public delegate T PacketDecoder<out T>(ref PacketReader reader) where T : class, IBedrockPacket;
public delegate void PacketEncoder<in T>(ref PacketWriter writer, T packet) where T : class, IBedrockPacket;

public sealed class PacketDefinition
{
    private PacketDefinition(uint id, Type packetType, UntypedPacketDecoder decode, UntypedPacketEncoder encode)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(id, PacketHeader.MaxPacketId);
        Id = id; PacketType = packetType; Decode = decode; Encode = encode;
    }
    public uint Id { get; }
    public Type PacketType { get; }
    internal UntypedPacketDecoder Decode { get; }
    internal UntypedPacketEncoder Encode { get; }

    public static PacketDefinition Create<T>(uint id, PacketDecoder<T> decode, PacketEncoder<T> encode) where T : class, IBedrockPacket
    {
        ArgumentNullException.ThrowIfNull(decode); ArgumentNullException.ThrowIfNull(encode);
        return new PacketDefinition(id, typeof(T), DecodePacket, EncodePacket);
        IBedrockPacket DecodePacket(ref PacketReader reader) => decode(ref reader);
        void EncodePacket(ref PacketWriter writer, IBedrockPacket packet) { if (packet is not T typed) throw new ArgumentException($"Expected {typeof(T).Name}.", nameof(packet)); encode(ref writer, typed); }
    }
}

/// <summary>Immutable, thread-safe packet registry for one Bedrock protocol version.</summary>
public sealed class BedrockCodec
{
    private readonly PacketDefinition?[] _byId;
    private readonly FrozenDictionary<Type, PacketDefinition> _byType;
    private BedrockCodec(int version, string gameVersion, PacketDefinition?[] byId, FrozenDictionary<Type, PacketDefinition> byType) { ProtocolVersion = version; MinecraftVersion = gameVersion; _byId = byId; _byType = byType; }
    public int ProtocolVersion { get; }
    public string MinecraftVersion { get; }

    public bool TryGetPacket(uint id, out PacketDefinition? definition) { definition = id < _byId.Length ? _byId[id] : null; return definition is not null; }

    public PacketEnvelope Decode(ReadOnlyMemory<byte> encodedPacket, ProtocolLimits? limits = null, bool preserveUnknown = true)
    {
        if (encodedPacket.IsEmpty) throw new ProtocolException("Packet is empty.");
        var reader = new PacketReader(encodedPacket.Span, limits);
        PacketHeader header = PacketHeader.Decode(reader.ReadVarUInt32());
        if (!TryGetPacket(header.PacketId, out PacketDefinition? definition))
        {
            if (!preserveUnknown) throw new ProtocolException($"Packet ID {header.PacketId} is not registered for protocol {ProtocolVersion}.");
            int offset = encodedPacket.Length - reader.Remaining;
            return new PacketEnvelope(header, new RawPacket(header.PacketId, encodedPacket[offset..]));
        }
        IBedrockPacket packet = definition!.Decode(ref reader);
        reader.EnsureFullyConsumed(definition.PacketType.Name);
        return new PacketEnvelope(header, packet);
    }

    public void Encode(IBufferWriter<byte> output, PacketEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(output);
        var writer = new PacketWriter(output);
        writer.WriteVarUInt32(envelope.Header.Encode());
        if (envelope.Packet is RawPacket raw) { if (raw.PacketId != envelope.Header.PacketId) throw new ProtocolException("Raw packet ID does not match envelope."); writer.WriteRaw(raw.Payload.Span); return; }
        if (!_byType.TryGetValue(envelope.Packet.GetType(), out PacketDefinition? definition)) throw new ProtocolException($"Packet type {envelope.Packet.GetType().Name} is not registered for protocol {ProtocolVersion}.");
        if (definition.Id != envelope.Header.PacketId) throw new ProtocolException($"Packet type maps to ID {definition.Id}, not {envelope.Header.PacketId}.");
        definition.Encode(ref writer, envelope.Packet);
    }

    public sealed class Builder
    {
        private readonly Dictionary<uint, PacketDefinition> _definitions = [];
        private readonly Dictionary<Type, PacketDefinition> _types = [];
        private readonly int _version;
        private readonly string _gameVersion;
        public Builder(int protocolVersion, string minecraftVersion) { ArgumentOutOfRangeException.ThrowIfNegative(protocolVersion); ArgumentException.ThrowIfNullOrWhiteSpace(minecraftVersion); _version = protocolVersion; _gameVersion = minecraftVersion; }
        public Builder Register(PacketDefinition definition) { ArgumentNullException.ThrowIfNull(definition); if (!_definitions.TryAdd(definition.Id, definition)) throw new InvalidOperationException($"Duplicate packet ID {definition.Id}."); if (!_types.TryAdd(definition.PacketType, definition)) { _definitions.Remove(definition.Id); throw new InvalidOperationException($"Duplicate packet type {definition.PacketType.Name}."); } return this; }
        public BedrockCodec Build() { var byId = new PacketDefinition?[PacketHeader.MaxPacketId + 1]; foreach ((uint id, PacketDefinition definition) in _definitions) byId[id] = definition; return new BedrockCodec(_version, _gameVersion, byId, _types.ToFrozenDictionary()); }
    }
}
