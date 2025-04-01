// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using AdHoc.ZooKeeper.Benchmark;

Console.WriteLine("Hello, World!");

var benchmark = new Benchmark();
try
{
    await benchmark.SetupAsync();

    Console.WriteLine("Running CreateDeleteAsync_AdHoc benchmark...");
    await benchmark.RunBenchmark(benchmark.CreateDeleteAsync_AdHoc, 100);

    Console.WriteLine("Running CreateDeleteAsync_Ex benchmark...");
    await benchmark.RunBenchmark(benchmark.CreateDeleteAsync_Ex, 100);

    Console.WriteLine("Running CreateDeleteAsync_AdHoc benchmark...");
    await benchmark.RunBenchmark(benchmark.CreateDeleteAsync_AdHoc, 100);

    Console.WriteLine("Running CreateDeleteAsync_Ex benchmark...");
    await benchmark.RunBenchmark(benchmark.CreateDeleteAsync_Ex, 100);

    Console.WriteLine("Running CreateDeleteAsync_AdHoc benchmark...");
    await benchmark.RunBenchmark(benchmark.CreateDeleteAsync_AdHoc, 100);

    Console.WriteLine("Running CreateDeleteAsync_Ex benchmark...");
    await benchmark.RunBenchmark(benchmark.CreateDeleteAsync_Ex, 100);
}
finally
{
    await benchmark.CleanupAsync();
}
