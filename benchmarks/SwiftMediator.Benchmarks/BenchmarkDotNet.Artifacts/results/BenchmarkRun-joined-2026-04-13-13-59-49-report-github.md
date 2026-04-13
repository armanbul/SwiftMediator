```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26200.8117)
Unknown processor
.NET SDK 10.0.201
  [Host]     : .NET 10.0.5 (10.0.526.15411), X64 RyuJIT AVX2
  Job-RDNZLK : .NET 10.0.5 (10.0.526.15411), X64 RyuJIT AVX2

IterationCount=10  WarmupCount=3  

```
| Type                   | Method                   | Mean        | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|----------------------- |------------------------- |------------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| NotificationBenchmarks | Swift_Publish            |    85.98 ns |  1.071 ns |  0.637 ns |  1.00 |    0.01 | 0.0204 |     384 B |        1.00 |
| PipelineBenchmarks     | Swift_SendWithBehavior   |   167.03 ns | 12.254 ns |  8.105 ns |  1.94 |    0.09 | 0.0284 |     536 B |        1.40 |
| RequestBenchmarks      | Swift_Send               |    83.51 ns |  3.518 ns |  2.327 ns |  0.97 |    0.03 | 0.0101 |     192 B |        0.50 |
| StreamBenchmarks       | Swift_Stream             | 1,080.40 ns | 31.941 ns | 21.127 ns | 12.57 |    0.25 | 0.0153 |     304 B |        0.79 |
| NotificationBenchmarks | MediatR_Publish          |    91.83 ns |  5.932 ns |  3.923 ns |  1.07 |    0.04 | 0.0263 |     496 B |        1.29 |
| PipelineBenchmarks     | MediatR_SendWithBehavior |   116.93 ns | 18.738 ns | 12.394 ns |  1.36 |    0.14 | 0.0350 |     664 B |        1.73 |
| RequestBenchmarks      | MediatR_Send             |    59.35 ns |  2.859 ns |  1.891 ns |  0.69 |    0.02 | 0.0186 |     352 B |        0.92 |
| StreamBenchmarks       | MediatR_Stream           | 2,333.16 ns | 19.486 ns | 11.596 ns | 27.14 |    0.23 | 0.0305 |     632 B |        1.65 |
| RequestBenchmarks      | Swift_SendVoid           |    81.04 ns |  6.642 ns |  4.393 ns |  0.94 |    0.05 | 0.0063 |     120 B |        0.31 |
| RequestBenchmarks      | MediatR_SendVoid         |    51.47 ns |  0.730 ns |  0.483 ns |  0.60 |    0.01 | 0.0098 |     184 B |        0.48 |
| RequestBenchmarks      | Swift_DynamicSend        |    80.15 ns |  1.565 ns |  0.818 ns |  0.93 |    0.01 | 0.0101 |     192 B |        0.50 |
| RequestBenchmarks      | MediatR_DynamicSend      |    56.44 ns |  1.009 ns |  0.601 ns |  0.66 |    0.01 | 0.0224 |     424 B |        1.10 |
