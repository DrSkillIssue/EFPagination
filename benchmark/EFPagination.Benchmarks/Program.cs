using BenchmarkDotNet.Running;
using EFPagination.Benchmarks;

BenchmarkSwitcher.FromAssembly(typeof(CursorBenchmarks).Assembly).Run(args);
