```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26200.8117)
Unknown processor
.NET SDK 10.0.201
  [Host]     : .NET 10.0.5 (10.0.526.15411), X64 RyuJIT AVX2
  Job-KPEQPV : .NET 10.0.5 (10.0.526.15411), X64 RyuJIT AVX2

IterationCount=10  WarmupCount=3  

```
| Type                   | Method                   | Mean        | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|----------------------- |------------------------- |------------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| NotificationBenchmarks | Swift_Publish            |    83.32 ns | 11.717 ns |  6.972 ns |  1.01 |    0.11 | 0.0204 |     384 B |        1.00 |
| PipelineBenchmarks     | Swift_SendWithBehavior   |   148.09 ns |  7.474 ns |  3.909 ns |  1.79 |    0.15 | 0.0296 |     560 B |        1.46 |
| RequestBenchmarks      | Swift_Send               |    61.30 ns | 11.907 ns |  7.876 ns |  0.74 |    0.11 | 0.0076 |     144 B |        0.38 |
| StreamBenchmarks       | Swift_Stream             | 1,091.88 ns | 13.407 ns |  8.868 ns | 13.19 |    1.05 | 0.0134 |     272 B |        0.71 |
| NotificationBenchmarks | MediatR_Publish          |    76.23 ns |  3.279 ns |  2.169 ns |  0.92 |    0.08 | 0.0263 |     496 B |        1.29 |
| PipelineBenchmarks     | MediatR_SendWithBehavior |    94.54 ns |  0.549 ns |  0.327 ns |  1.14 |    0.09 | 0.0352 |     664 B |        1.73 |
| RequestBenchmarks      | MediatR_Send             |    52.46 ns |  0.239 ns |  0.142 ns |  0.63 |    0.05 | 0.0187 |     352 B |        0.92 |
| StreamBenchmarks       | MediatR_Stream           | 2,309.60 ns | 34.580 ns | 22.872 ns | 27.89 |    2.23 | 0.0305 |     632 B |        1.65 |
| RequestBenchmarks      | Swift_SendVoid           |    50.26 ns |  1.951 ns |  1.290 ns |  0.61 |    0.05 | 0.0038 |      72 B |        0.19 |
| RequestBenchmarks      | MediatR_SendVoid         |    60.49 ns |  3.041 ns |  2.011 ns |  0.73 |    0.06 | 0.0098 |     184 B |        0.48 |
| RequestBenchmarks      | Swift_DynamicSend        |    61.59 ns |  4.906 ns |  3.245 ns |  0.74 |    0.07 | 0.0076 |     144 B |        0.38 |
| RequestBenchmarks      | MediatR_DynamicSend      |    59.27 ns |  3.594 ns |  1.880 ns |  0.72 |    0.06 | 0.0224 |     424 B |        1.10 |
