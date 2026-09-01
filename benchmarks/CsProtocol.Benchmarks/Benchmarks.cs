using System;
using System.Buffers;
using System.Diagnostics;
using CsProtocol.Packets;

namespace CsProtocol.Benchmarks;

internal static class Benchmarks
{
    internal static void Run()
    {
        const int iterations = 1_000_000;
        var encoded = new ArrayBufferWriter<byte>();
        ProtocolVersions.Current.Encode(encoded, new PacketEnvelope(new PacketHeader(115), new NetworkStackLatencyPacket(123456789, true)));
        ReadOnlyMemory<byte> payload = encoded.WrittenMemory;
        for (int i = 0; i < 10_000; i++) _ = ProtocolVersions.Current.Decode(payload);
        long before = GC.GetAllocatedBytesForCurrentThread();
        long start = Stopwatch.GetTimestamp();
        for (int i = 0; i < iterations; i++) _ = ProtocolVersions.Current.Decode(payload);
        TimeSpan elapsed = Stopwatch.GetElapsedTime(start);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Console.WriteLine($"Typed decode: {iterations / elapsed.TotalSeconds:N0} packets/s, {(double)allocated / iterations:N1} B/op");

        byte[] raw = [0xac, 0x02, 1, 2, 3, 4, 5];
        for (int i = 0; i < 10_000; i++) _ = ProtocolVersions.Current.Decode(raw);
        before = GC.GetAllocatedBytesForCurrentThread(); start = Stopwatch.GetTimestamp();
        for (int i = 0; i < iterations; i++) _ = ProtocolVersions.Current.Decode(raw);
        elapsed = Stopwatch.GetElapsedTime(start); allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Console.WriteLine($"Raw decode:   {iterations / elapsed.TotalSeconds:N0} packets/s, {(double)allocated / iterations:N1} B/op");
    }
}
