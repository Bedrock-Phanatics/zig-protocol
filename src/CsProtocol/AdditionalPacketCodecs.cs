using CsProtocol.Packets;

namespace CsProtocol;

internal static class AdditionalPacketCodecs
{
    internal static void Register(BedrockCodec.Builder builder)
    {
        builder.Register(PacketDefinition.Create<LoginPacket>(1, ReadLogin, WriteLogin));
        builder.Register(PacketDefinition.Create<ServerToClientHandshakePacket>(3, ReadServerHandshake, WriteServerHandshake));
        builder.Register(PacketDefinition.Create<ClientToServerHandshakePacket>(4, ReadClientHandshake, WriteClientHandshake));
        builder.Register(PacketDefinition.Create<SetTimePacket>(10, ReadSetTime, WriteSetTime));
        builder.Register(PacketDefinition.Create<RemoveActorPacket>(14, ReadRemoveActor, WriteRemoveActor));
        builder.Register(PacketDefinition.Create<SetHealthPacket>(42, ReadSetHealth, WriteSetHealth));
        builder.Register(PacketDefinition.Create<SetCommandsEnabledPacket>(59, ReadSetCommandsEnabled, WriteSetCommandsEnabled));
        builder.Register(PacketDefinition.Create<SetDifficultyPacket>(60, ReadSetDifficulty, WriteSetDifficulty));
        builder.Register(PacketDefinition.Create<RequestChunkRadiusPacket>(69, ReadRequestChunkRadius, WriteRequestChunkRadius));
        builder.Register(PacketDefinition.Create<ChunkRadiusUpdatedPacket>(70, ReadChunkRadiusUpdated, WriteChunkRadiusUpdated));
    }

    private static LoginPacket ReadLogin(ref PacketReader reader) => new(reader.ReadInt32BigEndian(), reader.ReadByteArray());
    private static void WriteLogin(ref PacketWriter writer, LoginPacket packet) { writer.WriteInt32BigEndian(packet.ClientProtocol); writer.WriteByteSpan(packet.ConnectionRequest.Span); }
    private static ServerToClientHandshakePacket ReadServerHandshake(ref PacketReader reader) => new(reader.ReadByteArray());
    private static void WriteServerHandshake(ref PacketWriter writer, ServerToClientHandshakePacket packet) => writer.WriteByteSpan(packet.Jwt.Span);
    private static ClientToServerHandshakePacket ReadClientHandshake(ref PacketReader reader) => new();
    private static void WriteClientHandshake(ref PacketWriter writer, ClientToServerHandshakePacket packet)
    {
        // NOOP
    }
    private static SetTimePacket ReadSetTime(ref PacketReader reader) => new(reader.ReadVarInt32());
    private static void WriteSetTime(ref PacketWriter writer, SetTimePacket packet) => writer.WriteVarInt32(packet.Time);
    private static RemoveActorPacket ReadRemoveActor(ref PacketReader reader) => new(reader.ReadVarInt64());
    private static void WriteRemoveActor(ref PacketWriter writer, RemoveActorPacket packet) => writer.WriteVarInt64(packet.EntityUniqueId);
    private static SetHealthPacket ReadSetHealth(ref PacketReader reader) => new(reader.ReadVarInt32());
    private static void WriteSetHealth(ref PacketWriter writer, SetHealthPacket packet) => writer.WriteVarInt32(packet.Health);
    private static SetCommandsEnabledPacket ReadSetCommandsEnabled(ref PacketReader reader) => new(reader.ReadBoolean());
    private static void WriteSetCommandsEnabled(ref PacketWriter writer, SetCommandsEnabledPacket packet) => writer.WriteBoolean(packet.Enabled);
    private static SetDifficultyPacket ReadSetDifficulty(ref PacketReader reader) => new(reader.ReadVarUInt32());
    private static void WriteSetDifficulty(ref PacketWriter writer, SetDifficultyPacket packet) => writer.WriteVarUInt32(packet.Difficulty);
    private static RequestChunkRadiusPacket ReadRequestChunkRadius(ref PacketReader reader) => new(reader.ReadVarInt32(), reader.ReadByte());
    private static void WriteRequestChunkRadius(ref PacketWriter writer, RequestChunkRadiusPacket packet) { writer.WriteVarInt32(packet.ChunkRadius); writer.WriteByte(packet.MaxChunkRadius); }
    private static ChunkRadiusUpdatedPacket ReadChunkRadiusUpdated(ref PacketReader reader) => new(reader.ReadVarInt32());
    private static void WriteChunkRadiusUpdated(ref PacketWriter writer, ChunkRadiusUpdatedPacket packet) => writer.WriteVarInt32(packet.ChunkRadius);
}
