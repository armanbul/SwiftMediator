```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26200.8117)
Unknown processor
.NET SDK 10.0.201
  [Host]     : .NET 10.0.5 (10.0.526.15411), X64 RyuJIT AVX2
  Job-JWMSOX : .NET 10.0.5 (10.0.526.15411), X64 RyuJIT AVX2

IterationCount=10  WarmupCount=3  

```
| Type                   | Method                   | Mean        | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|----------------------- |------------------------- |------------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| NotificationBenchmarks | Swift_Publish            |   144.81 ns |  7.896 ns |  5.223 ns |  1.00 |    0.05 | 0.0204 |     384 B |        1.00 |
| PipelineBenchmarks     | Swift_SendWithBehavior   |   288.15 ns |  8.074 ns |  5.340 ns |  1.99 |    0.08 | 0.0296 |     560 B |        1.46 |
| RequestBenchmarks      | Swift_Send               |   105.09 ns |  7.087 ns |  4.688 ns |  0.73 |    0.04 | 0.0076 |     144 B |        0.38 |
| StreamBenchmarks       | Swift_Stream             | 1,110.40 ns | 15.126 ns | 10.005 ns |  7.68 |    0.27 | 0.0134 |     272 B |        0.71 |
| NotificationBenchmarks | MediatR_Publish          |   151.94 ns |  1.639 ns |  0.975 ns |  1.05 |    0.04 | 0.0263 |     496 B |        1.29 |
| PipelineBenchmarks     | MediatR_SendWithBehavior |   172.47 ns |  4.846 ns |  2.884 ns |  1.19 |    0.05 | 0.0352 |     664 B |        1.73 |
| RequestBenchmarks      | MediatR_Send             |    99.62 ns |  3.285 ns |  2.173 ns |  0.69 |    0.03 | 0.0186 |     352 B |        0.92 |
| StreamBenchmarks       | MediatR_Stream           | 2,288.75 ns | 19.013 ns | 12.576 ns | 15.82 |    0.55 | 0.0305 |     632 B |        1.65 |
| RequestBenchmarks      | Swift_SendVoid           |    88.40 ns | 26.530 ns | 17.548 ns |  0.61 |    0.12 | 0.0038 |      72 B |        0.19 |
| RequestBenchmarks      | MediatR_SendVoid         |    55.55 ns |  1.581 ns |  1.046 ns |  0.38 |    0.01 | 0.0098 |     184 B |        0.48 |
| RequestBenchmarks      | Swift_DynamicSend        |    58.38 ns |  1.580 ns |  1.045 ns |  0.40 |    0.02 | 0.0076 |     144 B |        0.38 |
| RequestBenchmarks      | MediatR_DynamicSend      |    76.16 ns |  6.052 ns |  3.165 ns |  0.53 |    0.03 | 0.0224 |     424 B |        1.10 |
