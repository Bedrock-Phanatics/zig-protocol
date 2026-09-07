const std = @import("std");
pub fn build(b: *std.Build) void {
    const target = b.standardTargetOptions(.{});
    const optimize = b.standardOptimizeOption(.{});
    const module = b.addModule("bedrock_protocol", .{ .root_source_file = b.path("src/root.zig"), .target = target, .optimize = optimize });
    const tests = b.addTest(.{ .root_module = module });
    const test_step = b.step("test", "Run protocol tests");
    test_step.dependOn(&b.addRunArtifact(tests).step);
    const bench = b.addExecutable(.{ .name = "protocol-bench", .root_module = b.createModule(.{ .root_source_file = b.path("benchmarks/main.zig"), .target = target, .optimize = optimize, .imports = &.{.{ .name = "bedrock_protocol", .module = module }} }) });
    const bench_step = b.step("bench", "Run microbenchmarks");
    bench_step.dependOn(&b.addRunArtifact(bench).step);
}
