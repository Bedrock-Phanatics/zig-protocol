namespace CsProtocol;

public static class ProtocolVersions
{
    public const int CurrentProtocol = 2192;
    public const string CurrentMinecraftVersion = "1.26.50";
    public static BedrockCodec V2192 { get; } = Create2192();
    public static BedrockCodec Current => V2192;

    private static BedrockCodec Create2192()
    {
        var builder = new BedrockCodec.Builder(CurrentProtocol, CurrentMinecraftVersion);
        CorePacketCodecs.Register(builder);
        AdditionalPacketCodecs.Register(builder);
        return builder.Build();
    }
}
