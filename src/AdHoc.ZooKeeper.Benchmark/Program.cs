// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using AdHoc.ZooKeeper.Benchmark;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Running;

Console.WriteLine(BenchmarkRunner.Run<Benchmark>(
    DefaultConfig.Instance
        .WithOptions(ConfigOptions.DisableOptimizationsValidator)
        .AddDiagnoser(MemoryDiagnoser.Default)
        .AddExporter(MarkdownExporter.Default)
));
