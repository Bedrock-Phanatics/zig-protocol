using System;
using System.Collections.Generic;
using CsProtocol.Packets;

namespace CsProtocol;

internal static class CorePacketCodecs
{
    internal static void Register(BedrockCodec.Builder builder)
    {
        builder.Register(PacketDefinition.Create<PlayStatusPacket>(2, ReadPlayStatus, WritePlayStatus));
        builder.Register(PacketDefinition.Create<DisconnectPacket>(5, ReadDisconnect, WriteDisconnect));
        builder.Register(PacketDefinition.Create<TextPacket>(9, ReadText, WriteText));
        builder.Register(PacketDefinition.Create<MovePlayerPacket>(19, ReadMovePlayer, WriteMovePlayer));
        builder.Register(PacketDefinition.Create<NetworkStackLatencyPacket>(115, ReadLatency, WriteLatency));
        builder.Register(PacketDefinition.Create<NetworkSettingsPacket>(143, ReadNetworkSettings, WriteNetworkSettings));
        builder.Register(PacketDefinition.Create<RequestNetworkSettingsPacket>(193, ReadRequestNetworkSettings, WriteRequestNetworkSettings));
    }

    private static PlayStatusPacket ReadPlayStatus(ref PacketReader reader) => new((PlayStatus)reader.ReadInt32BigEndian());
    private static void WritePlayStatus(ref PacketWriter writer, PlayStatusPacket packet) => writer.WriteInt32BigEndian((int)packet.Status);
    private static RequestNetworkSettingsPacket ReadRequestNetworkSettings(ref PacketReader reader) => new(reader.ReadInt32BigEndian());
    private static void WriteRequestNetworkSettings(ref PacketWriter writer, RequestNetworkSettingsPacket packet) => writer.WriteInt32BigEndian(packet.ClientProtocol);
    private static NetworkSettingsPacket ReadNetworkSettings(ref PacketReader reader) => new(reader.ReadUInt16(), reader.ReadUInt16(), reader.ReadBoolean(), reader.ReadByte(), reader.ReadSingle());
    private static void WriteNetworkSettings(ref PacketWriter writer, NetworkSettingsPacket packet) { writer.WriteUInt16(packet.CompressionThreshold); writer.WriteUInt16(packet.CompressionAlgorithm); writer.WriteBoolean(packet.ClientThrottle); writer.WriteByte(packet.ClientThrottleThreshold); writer.WriteSingle(packet.ClientThrottleScalar); }
    private static DisconnectPacket ReadDisconnect(ref PacketReader reader) { int reason = reader.ReadVarInt32(); bool skipped = reader.ReadVarUInt32() != 0; return skipped ? new(reason, true) : new(reason, false, reader.ReadString(), reader.ReadString()); }
    private static void WriteDisconnect(ref PacketWriter writer, DisconnectPacket packet) { writer.WriteVarInt32(packet.Reason); writer.WriteVarUInt32(packet.MessageSkipped ? 1u : 0u); if (!packet.MessageSkipped) { writer.WriteString(packet.Message); writer.WriteString(packet.FilteredMessage); } }
    private static NetworkStackLatencyPacket ReadLatency(ref PacketReader reader) => new(reader.ReadInt64(), reader.ReadBoolean());
    private static void WriteLatency(ref PacketWriter writer, NetworkStackLatencyPacket packet) { writer.WriteInt64(packet.Timestamp); writer.WriteBoolean(packet.NeedsResponse); }

    private static MovePlayerPacket ReadMovePlayer(ref PacketReader reader)
    {
        ulong runtimeId = reader.ReadVarUInt64();
        System.Numerics.Vector3 position = reader.ReadVector3();
        System.Numerics.Vector3 rotation = reader.ReadVector3();
        MoveMode mode = (MoveMode)reader.ReadByte();
        if ((byte)mode > 3) throw new ProtocolException($"Unknown move mode {(byte)mode}.");
        bool onGround = reader.ReadBoolean();
        ulong ridden = reader.ReadVarUInt64();
        TeleportData? teleport = reader.ReadBoolean() ? new(reader.ReadInt32(), reader.ReadInt32()) : null;
        return new(runtimeId, position, rotation, mode, onGround, ridden, teleport, reader.ReadVarUInt64());
    }

    private static void WriteMovePlayer(ref PacketWriter writer, MovePlayerPacket packet)
    {
        if ((byte)packet.Mode > 3) throw new ProtocolException($"Unknown move mode {(byte)packet.Mode}.");
        if (packet.Mode == MoveMode.Teleport != packet.Teleport.HasValue) throw new ProtocolException("Teleport data presence must match teleport move mode.");
        writer.WriteVarUInt64(packet.EntityRuntimeId); writer.WriteVector3(packet.Position); writer.WriteVector3(packet.Rotation); writer.WriteByte((byte)packet.Mode);
        writer.WriteBoolean(packet.OnGround); writer.WriteVarUInt64(packet.RiddenEntityRuntimeId); writer.WriteBoolean(packet.Teleport.HasValue);
        if (packet.Teleport is TeleportData teleport) { writer.WriteInt32(teleport.Cause); writer.WriteInt32(teleport.SourceEntityType); }
        writer.WriteVarUInt64(packet.Tick);
    }

    private static TextPacket ReadText(ref PacketReader reader)
    {
        bool translation = reader.ReadBoolean();
        byte category = reader.ReadByte();
        TextType type = (TextType)reader.ReadByte();
        if ((byte)type > 11) throw new ProtocolException($"Unknown text type {(byte)type}.");
        if (category != GetTextCategory(type)) throw new ProtocolException($"Text category {category} does not match type {type}.");
        string source = "";
        string message;
        IReadOnlyList<string>? parameters = null;
        switch (category)
        {
            case 0: message = reader.ReadString(); break;
            case 1: source = ReadLimitedString(ref reader, 256, "text source"); message = reader.ReadString(); break;
            case 2:
                message = reader.ReadString();
                int count = reader.ReadCollectionLength("text parameters");
                if (count > 4) throw new ProtocolException("Text packet has more than 4 parameters.");
                var values = new string[count]; for (int i = 0; i < count; i++) values[i] = reader.ReadString(); parameters = values;
                break;
            default: throw new ProtocolException($"Unknown text category {category}.");
        }
        if (message.Length == 0 || message.Length > 65536) throw new ProtocolException("Text message length is outside 1..65536 characters.");
        string xuid = ReadLimitedString(ref reader, 64, "XUID");
        string platform = ReadLimitedString(ref reader, 256, "platform chat ID");
        string? filtered = reader.ReadBoolean() ? reader.ReadString() : null;
        if (filtered?.Length > 65536) throw new ProtocolException("Filtered text exceeds 65536 characters.");
        return new(type, translation, message, source, parameters, xuid, platform, filtered);
    }

    private static void WriteText(ref PacketWriter writer, TextPacket packet)
    {
        if (packet.Message.Length is < 1 or > 65536) throw new ProtocolException("Text message length is outside 1..65536 characters.");
        if (packet.SourceName.Length > 256 || packet.Xuid.Length > 64 || packet.PlatformChatId.Length > 256 || packet.FilteredMessage?.Length > 65536) throw new ProtocolException("Text packet field exceeds its protocol limit.");
        byte category = GetTextCategory(packet.Type);
        writer.WriteBoolean(packet.NeedsTranslation); writer.WriteByte(category); writer.WriteByte((byte)packet.Type);
        if (category == 1) writer.WriteString(packet.SourceName);
        writer.WriteString(packet.Message);
        if (category == 2) { int count = packet.Parameters?.Count ?? 0; if (count > 4) throw new ProtocolException("Text packet has more than 4 parameters."); writer.WriteVarUInt32((uint)count); if (packet.Parameters is not null) foreach (string value in packet.Parameters) writer.WriteString(value); }
        writer.WriteString(packet.Xuid); writer.WriteString(packet.PlatformChatId); writer.WriteBoolean(packet.FilteredMessage is not null); if (packet.FilteredMessage is not null) writer.WriteString(packet.FilteredMessage);
    }

    private static string ReadLimitedString(ref PacketReader reader, int maximumCharacters, string name) { string value = reader.ReadString(); if (value.Length > maximumCharacters) throw new ProtocolException($"{name} exceeds {maximumCharacters} characters."); return value; }

    private static byte GetTextCategory(TextType type) => type switch
    {
        TextType.Raw or TextType.Tip or TextType.System or TextType.WhisperJson or TextType.Json or TextType.AnnouncementJson => 0,
        TextType.Chat or TextType.Whisper or TextType.Announcement => 1,
        TextType.Translation or TextType.Popup or TextType.JukeboxPopup => 2,
        _ => throw new ProtocolException($"Unknown text type {(byte)type}.")
    };
}
