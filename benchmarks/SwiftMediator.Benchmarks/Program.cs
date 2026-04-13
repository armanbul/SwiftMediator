using BenchmarkDotNet.Running;
using SwiftMediator.Benchmarks;

BenchmarkSwitcher.FromAssembly(typeof(RequestBenchmarks).Assembly).Run(args);
