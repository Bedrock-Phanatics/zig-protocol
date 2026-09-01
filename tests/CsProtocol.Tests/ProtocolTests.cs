using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using CsProtocol.Packets;
using Xunit;

namespace CsProtocol.Tests;

public sealed class ProtocolTests
{
    [Fact]
    public void PrimitivesRoundTrip()
    {
        int[] signed = [int.MinValue, -1, 0, 1, 127, 128, int.MaxValue];
        foreach (int expected in signed)
        {
            var buffer = new ArrayBufferWriter<byte>();
            var writer = new PacketWriter(buffer);
            writer.WriteVarInt32(expected);
            var reader = new PacketReader(buffer.WrittenSpan);
            Assert.Equal(expected, reader.ReadVarInt32());
            Assert.True(reader.End);
        }

        uint[] unsigned = [0, 1, 127, 128, 16384, uint.MaxValue];
        foreach (uint expected in unsigned)
        {
            var buffer = new ArrayBufferWriter<byte>();
            var writer = new PacketWriter(buffer);
            writer.WriteVarUInt32(expected);
            var reader = new PacketReader(buffer.WrittenSpan);
            Assert.Equal(expected, reader.ReadVarUInt32());
            Assert.True(reader.End);
        }

        var strings = new ArrayBufferWriter<byte>();
        var stringWriter = new PacketWriter(strings);
        stringWriter.WriteString("Bedrock ⛏️");
        var stringReader = new PacketReader(strings.WrittenSpan);
        Assert.Equal("Bedrock ⛏️", stringReader.ReadString());
    }

    [Fact]
    public void MalformedVarIntsAreRejected()
    {
        Assert.Throws<ProtocolException>(() => ReadVarUInt32([0x80]));
        Assert.Throws<ProtocolException>(() => ReadVarUInt32([0x80, 0x00]));
        Assert.Throws<ProtocolException>(() => ReadVarUInt32([0xff, 0xff, 0xff, 0xff, 0x10]));
        Assert.Throws<ProtocolException>(() => ReadVarUInt64([0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0x02]));
    }

    [Fact]
    public void PacketHeaderRoundTrips()
    {
        var expected = new PacketHeader(1023, 3, 2);
        Assert.Equal(expected, PacketHeader.Decode(expected.Encode()));
        Assert.Throws<ProtocolException>(() => new PacketHeader(1024).Encode());
        Assert.Throws<ProtocolException>(() => new PacketHeader(1, 4, 0).Encode());
        Assert.Throws<ProtocolException>(() => PacketHeader.Decode(0x4000));
    }

    [Fact]
    public void RawPacketRoundTripIsLossless()
    {
        BedrockCodec codec = ProtocolVersions.Current;
        byte[] expected = [0xac, 0x02, 9, 8, 7];
        PacketEnvelope decoded = codec.Decode(expected);
        Assert.IsType<RawPacket>(decoded.Packet);

        var output = new ArrayBufferWriter<byte>();
        codec.Encode(output, decoded);
        Assert.Equal(expected, output.WrittenSpan);
    }

    [Fact]
    public void TypedPacketsRoundTrip()
    {
        PacketEnvelope[] packets =
        [
            new(new PacketHeader(1), new LoginPacket(2192, new byte[] { 1, 2, 3 })),
            new(new PacketHeader(2), new PlayStatusPacket(PlayStatus.PlayerSpawn)),
            new(new PacketHeader(3), new ServerToClientHandshakePacket(new byte[] { 4, 5, 6 })),
            new(new PacketHeader(4), new ClientToServerHandshakePacket()),
            new(new PacketHeader(5), new DisconnectPacket(58, false, "bye", "filtered")),
            new(new PacketHeader(9), new TextPacket(TextType.Chat, false, "hello", "Steve", Xuid: "123")),
            new(new PacketHeader(10), new SetTimePacket(-6000)),
            new(new PacketHeader(14), new RemoveActorPacket(long.MinValue)),
            new(new PacketHeader(19), new MovePlayerPacket(42, new Vector3(1, 2, 3), new Vector3(4, 5, 6), MoveMode.Teleport, true, 0, new TeleportData(3, 17), 99)),
            new(new PacketHeader(42), new SetHealthPacket(20)),
            new(new PacketHeader(59), new SetCommandsEnabledPacket(true)),
            new(new PacketHeader(60), new SetDifficultyPacket(3)),
            new(new PacketHeader(69), new RequestChunkRadiusPacket(12, 32)),
            new(new PacketHeader(70), new ChunkRadiusUpdatedPacket(10)),
            new(new PacketHeader(115), new NetworkStackLatencyPacket(123456789, true)),
            new(new PacketHeader(143), new NetworkSettingsPacket(256, 0, true, 12, 0.5f)),
            new(new PacketHeader(193), new RequestNetworkSettingsPacket(2192))
        ];

        foreach (PacketEnvelope packet in packets) AssertWireRoundTrip(packet);
    }
    [Fact]
    public void JsonTextUsesCurrentWireLayout()
    {
        var encoded = new ArrayBufferWriter<byte>();
        ProtocolVersions.Current.Encode(encoded, new PacketEnvelope(new PacketHeader(9), new TextPacket(TextType.Json, false, "{}")));
        Assert.Equal(new byte[] { 9, 0, 0, 10, 2, (byte)'{', (byte)'}', 0, 0, 0 }, encoded.WrittenSpan);

        TextPacket decoded = Assert.IsType<TextPacket>(ProtocolVersions.Current.Decode(encoded.WrittenMemory).Packet);
        Assert.Equal(TextType.Json, decoded.Type);
        Assert.Equal("{}", decoded.Message);

        byte[] mismatchedCategory = [9, 0, 0, (byte)TextType.Chat, 1, (byte)'x', 0, 0, 0];
        Assert.Throws<ProtocolException>(() => ProtocolVersions.Current.Decode(mismatchedCategory));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(CompressionAlgorithm.Deflate)]
    public void BatchRoundTrips(CompressionAlgorithm? algorithm)
    {
        ReadOnlyMemory<byte>[] expected =
        [
            new byte[] { 1, 2, 3 },
            new byte[] { 4 },
            new byte[] { 5, 6 }
        ];
        var codec = new BatchCodec(new BatchCodecOptions { Compression = algorithm, CompressionThreshold = 0 });
        byte[] encoded = codec.Encode(expected);
        IReadOnlyList<ReadOnlyMemory<byte>> decoded = codec.Decode(encoded);

        Assert.Equal(expected.Length, decoded.Count);
        for (int i = 0; i < expected.Length; i++) Assert.Equal(expected[i].Span, decoded[i].Span);
    }

    [Fact]
    public void CompressionBombIsBounded()
    {
        byte[] large = new byte[4096];
        var encoder = new BatchCodec(new BatchCodecOptions
        {
            Compression = CompressionAlgorithm.Deflate,
            CompressionThreshold = 0,
            Limits = ProtocolLimits.Default with { MaxDecompressedBatchBytes = 8192 }
        });
        byte[] encoded = encoder.Encode([large]);
        var decoder = new BatchCodec(new BatchCodecOptions
        {
            Compression = CompressionAlgorithm.Deflate,
            Limits = ProtocolLimits.Default with { MaxDecompressedBatchBytes = 128 }
        });

        Assert.Throws<ProtocolException>(() => decoder.Decode(encoded));
    }

    [Fact]
    public void MaximumDecompressionLimitDoesNotOverflow()
    {
        var encoder = new BatchCodec(new BatchCodecOptions { Compression = CompressionAlgorithm.Deflate, CompressionThreshold = 0 });
        byte[] encoded = encoder.Encode([new byte[] { 1, 2, 3 }]);
        var decoder = new BatchCodec(new BatchCodecOptions
        {
            Compression = CompressionAlgorithm.Deflate,
            Limits = ProtocolLimits.Default with { MaxDecompressedBatchBytes = int.MaxValue }
        });

        IReadOnlyList<ReadOnlyMemory<byte>> decoded = decoder.Decode(encoded);
        Assert.Single(decoded);
        Assert.Equal(new byte[] { 1, 2, 3 }, decoded[0].Span);
    }

    [Fact]
    public void OversizedEncryptedBatchDoesNotAdvanceCipherState()
    {
        byte[] key = new byte[32];
        ProtocolLimits limits = ProtocolLimits.Default with { MaxBatchBytes = 32 };
        var options = new BatchCodecOptions { Limits = limits };
        using var attemptedEncryption = new BatchEncryption(key);
        var attemptedCodec = new BatchCodec(options, attemptedEncryption);
        Assert.Throws<ProtocolException>(() => attemptedCodec.Encode([new byte[23]]));
        byte[] actual = attemptedCodec.Encode([new byte[] { 1, 2, 3 }]);

        using var freshEncryption = new BatchEncryption(key);
        var freshCodec = new BatchCodec(options, freshEncryption);
        byte[] expected = freshCodec.Encode([new byte[] { 1, 2, 3 }]);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void EncryptionMatchesGoVector()
    {
        byte[] key = [.. Enumerable.Range(0, 32).Select(static value => (byte)value)];
        using var sender = new BatchEncryption(key);
        byte[] encrypted = sender.Encrypt([0, 1, 2, 3, 4, 5, 254, 255]);
        Assert.Equal("4703d418c1e03ce4463fd8b631724149", Convert.ToHexStringLower(encrypted));

        using var receiver = new BatchEncryption(key);
        Assert.Equal(new byte[] { 0, 1, 2, 3, 4, 5, 254, 255 }, receiver.DecryptAndVerify(encrypted));
    }

    [Fact]
    public void EncryptionTamperingIsRejected()
    {
        byte[] key = new byte[32];
        using var sender = new BatchEncryption(key);
        byte[] encrypted = sender.Encrypt([1, 2, 3]);
        encrypted[2] ^= 0x80;
        using var receiver = new BatchEncryption(key);
        Assert.Throws<ProtocolException>(() => receiver.DecryptAndVerify(encrypted));
    }

    [Fact]
    public void RandomMalformedInputTerminates()
    {
        var random = new Random(1337);
        for (int i = 0; i < 20_000; i++)
        {
            byte[] data = new byte[random.Next(0, 32)];
            random.NextBytes(data);
            try
            {
                _ = ProtocolVersions.Current.Decode(data);
            }
            catch (ProtocolException)
            {
                // NOOP
            }
        }
    }

    private static void AssertWireRoundTrip(PacketEnvelope expected)
    {
        var encoded = new ArrayBufferWriter<byte>();
        ProtocolVersions.Current.Encode(encoded, expected);
        PacketEnvelope decoded = ProtocolVersions.Current.Decode(encoded.WrittenMemory);
        var reencoded = new ArrayBufferWriter<byte>();
        ProtocolVersions.Current.Encode(reencoded, decoded);
        Assert.Equal(encoded.WrittenSpan, reencoded.WrittenSpan);
    }

    private static uint ReadVarUInt32(byte[] data)
    {
        var reader = new PacketReader(data);
        return reader.ReadVarUInt32();
    }

    private static ulong ReadVarUInt64(byte[] data)
    {
        var reader = new PacketReader(data);
        return reader.ReadVarUInt64();
    }
}
