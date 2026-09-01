using System;
using System.Collections.Generic;
using System.Numerics;

namespace CsProtocol.Packets;

public enum PlayStatus : int
{
    LoginSuccess = 0, LoginFailedClient = 1, LoginFailedServer = 2, PlayerSpawn = 3,
    LoginFailedInvalidTenant = 4, LoginFailedVanillaEducation = 5, LoginFailedEducationVanilla = 6,
    LoginFailedServerFull = 7, LoginFailedEditorVanilla = 8, LoginFailedVanillaEditor = 9
}

public sealed record PlayStatusPacket(PlayStatus Status) : IBedrockPacket;
public sealed record RequestNetworkSettingsPacket(int ClientProtocol) : IBedrockPacket;
public sealed record NetworkSettingsPacket(ushort CompressionThreshold, ushort CompressionAlgorithm, bool ClientThrottle, byte ClientThrottleThreshold, float ClientThrottleScalar) : IBedrockPacket;
public sealed record DisconnectPacket(int Reason, bool MessageSkipped, string Message = "", string FilteredMessage = "") : IBedrockPacket;
public sealed record NetworkStackLatencyPacket(long Timestamp, bool NeedsResponse) : IBedrockPacket;

public enum MoveMode : byte { Normal = 0, Reset = 1, Teleport = 2, Rotation = 3 }
public readonly record struct TeleportData(int Cause, int SourceEntityType);
public sealed record MovePlayerPacket(ulong EntityRuntimeId, Vector3 Position, Vector3 Rotation, MoveMode Mode, bool OnGround, ulong RiddenEntityRuntimeId, TeleportData? Teleport, ulong Tick) : IBedrockPacket;

public enum TextType : byte
{
    Raw = 0, Chat = 1, Translation = 2, Popup = 3, JukeboxPopup = 4, Tip = 5,
    System = 6, Whisper = 7, Announcement = 8, WhisperJson = 9, Json = 10, AnnouncementJson = 11
}

public sealed record TextPacket(
    TextType Type,
    bool NeedsTranslation,
    string Message,
    string SourceName = "",
    IReadOnlyList<string>? Parameters = null,
    string Xuid = "",
    string PlatformChatId = "",
    string? FilteredMessage = null) : IBedrockPacket;
public sealed record LoginPacket(int ClientProtocol, ReadOnlyMemory<byte> ConnectionRequest) : IBedrockPacket;
public sealed record ServerToClientHandshakePacket(ReadOnlyMemory<byte> Jwt) : IBedrockPacket;
public sealed record ClientToServerHandshakePacket : IBedrockPacket;
public sealed record SetTimePacket(int Time) : IBedrockPacket;
public sealed record RemoveActorPacket(long EntityUniqueId) : IBedrockPacket;
public sealed record SetHealthPacket(int Health) : IBedrockPacket;
public sealed record SetCommandsEnabledPacket(bool Enabled) : IBedrockPacket;
public sealed record SetDifficultyPacket(uint Difficulty) : IBedrockPacket;
public sealed record RequestChunkRadiusPacket(int ChunkRadius, byte MaxChunkRadius) : IBedrockPacket;
public sealed record ChunkRadiusUpdatedPacket(int ChunkRadius) : IBedrockPacket;
