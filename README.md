# CsProtocol

CsProtocol is an allocation-conscious Minecraft: Bedrock Edition packet-codec foundation for .NET 10. The current codec targets protocol **2192 / Minecraft 1.26.50**, based primarily on CloudburstMC Protocol 3.0 and cross-checked against Gophertunnel 1.26.45 where that older reference still applies.

## Current capabilities

- bounds-checked little-endian primitives, canonical ZigZag VarInts, strict UTF-8, vectors, positions, GUIDs, and `cs-nbt` integration;
- immutable, thread-safe per-version packet registries;
- lossless borrowed-memory `RawPacket` forwarding for every legal 10-bit packet ID;
- typed codecs for the handshake/control hot path: play status, disconnect, text, move player, network settings, request network settings, and network stack latency;
- Bedrock `0xfe` batch framing with packet-count, packet-size, batch-size, string, collection, and decompression limits;
- raw DEFLATE and no-compression batches;
- Bedrock AES-256-CTR plus rolling SHA-256 checksums, checked against an independent Go standard-library vector;
- malformed-input regressions and a deterministic random-input stress pass.

The typed packet model is intentionally not advertised as complete yet. Packets without typed registrations are preserved byte-for-byte as `RawPacket`, which is useful for transparent proxies but does not provide semantic field access. Snappy batches also currently throw `NotSupportedException` instead of silently using an incompatible codec.

## Example

```csharp
using System.Buffers;
using CsProtocol;
using CsProtocol.Packets;

BedrockCodec codec = ProtocolVersions.Current;

var output = new ArrayBufferWriter<byte>();
codec.Encode(output, new PacketEnvelope(
    new PacketHeader(115),
    new NetworkStackLatencyPacket(123456789, NeedsResponse: true)));

PacketEnvelope decoded = codec.Decode(output.WrittenMemory);
```

`RawPacket.Payload` borrows its input memory. The caller must keep that memory alive and unchanged until forwarding or copying it. Codec registries are safe to share across connections. `BatchEncryption` is connection-direction state and must be serialized by its owner; use separate instances for sending and receiving.

## Validation

```console
dotnet build CsProtocol.slnx --configuration Release
dotnet test tests/CsProtocol.Tests --configuration Release
dotnet run --project benchmarks/CsProtocol.Benchmarks --configuration Release
```

The lightweight benchmark harness avoids a runtime dependency on BenchmarkDotNet. Its numbers are diagnostic, not a substitute for representative end-to-end proxy profiling.

## Scope

RakNet transport, Xbox authentication, discovery, server lists, resource-pack storage, and proxy/session policy do not belong in this packet library. They should be layered on top. `cs-nbt` remains the NBT implementation for this ecosystem.
