using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using Microsoft.Extensions.DependencyInjection;
using SwiftMediator.Core;

namespace SwiftMediator.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class RequestBenchmarks
{
    private IServiceProvider _swiftProvider = null!;
    private IServiceProvider _mediatrProvider = null!;

    [GlobalSetup]
    public void Setup()
    {
        // SwiftMediator
        var swiftServices = new ServiceCollection();
        swiftServices.AddSwiftMediator();
        _swiftProvider = swiftServices.BuildServiceProvider().CreateScope().ServiceProvider;

        // MediatR
        var mediatrServices = new ServiceCollection();
        mediatrServices.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<MediatRPingHandler>());
        _mediatrProvider = mediatrServices.BuildServiceProvider().CreateScope().ServiceProvider;
    }

    // ═══════════════════════════════════════════════════════════════
    // 1. Simple Request/Response
    // ═══════════════════════════════════════════════════════════════

    [Benchmark(Baseline = true)]
    public async Task<PingResponse> Swift_Send()
    {
        var mediator = _swiftProvider.GetRequiredService<IMediator>();
        return await mediator.SendAsync<PingRequest, PingResponse>(new PingRequest { Message = "hi" });
    }

    [Benchmark]
    public async Task<MediatRPingResponse> MediatR_Send()
    {
        var mediator = _mediatrProvider.GetRequiredService<MediatR.IMediator>();
        return await mediator.Send(new MediatRPing { Message = "hi" });
    }

    // ═══════════════════════════════════════════════════════════════
    // 2. Void (Unit) Request
    // ═══════════════════════════════════════════════════════════════

    [Benchmark]
    public async Task Swift_SendVoid()
    {
        var mediator = _swiftProvider.GetRequiredService<IMediator>();
        await mediator.SendAsync(new FireCommand { Value = 1 });
    }

    [Benchmark]
    public async Task MediatR_SendVoid()
    {
        var mediator = _mediatrProvider.GetRequiredService<MediatR.IMediator>();
        await mediator.Send(new MediatRFire { Value = 1 });
    }

    // ═══════════════════════════════════════════════════════════════
    // 3. Dynamic (object) Dispatch
    // ═══════════════════════════════════════════════════════════════

    [Benchmark]
    public async Task<object?> Swift_DynamicSend()
    {
        var mediator = _swiftProvider.GetRequiredService<IMediator>();
        return await mediator.SendAsync((object)new PingRequest { Message = "dyn" });
    }

    [Benchmark]
    public async Task<object?> MediatR_DynamicSend()
    {
        var mediator = _mediatrProvider.GetRequiredService<MediatR.IMediator>();
        return await mediator.Send((object)new MediatRPing { Message = "dyn" });
    }
}

[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class NotificationBenchmarks
{
    private IServiceProvider _swiftProvider = null!;
    private IServiceProvider _mediatrProvider = null!;

    [GlobalSetup]
    public void Setup()
    {
        var swiftServices = new ServiceCollection();
        swiftServices.AddSwiftMediator();
        _swiftProvider = swiftServices.BuildServiceProvider().CreateScope().ServiceProvider;

        var mediatrServices = new ServiceCollection();
        mediatrServices.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<MediatRPingHandler>());
        _mediatrProvider = mediatrServices.BuildServiceProvider().CreateScope().ServiceProvider;
    }

    [Benchmark(Baseline = true)]
    public async Task Swift_Publish()
    {
        var mediator = _swiftProvider.GetRequiredService<IMediator>();
        await mediator.PublishAsync(new PingNotification { Message = "ev" });
    }

    [Benchmark]
    public async Task MediatR_Publish()
    {
        var mediator = _mediatrProvider.GetRequiredService<MediatR.IMediator>();
        await mediator.Publish(new MediatRPingNotification { Message = "ev" });
    }
}

[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class PipelineBenchmarks
{
    private IServiceProvider _swiftProvider = null!;
    private IServiceProvider _mediatrProvider = null!;

    [GlobalSetup]
    public void Setup()
    {
        var swiftServices = new ServiceCollection();
        swiftServices.AddSwiftMediator();
        swiftServices.AddTransient(typeof(IPipelineBehavior<,>), typeof(BenchmarkBehavior<,>));
        _swiftProvider = swiftServices.BuildServiceProvider().CreateScope().ServiceProvider;

        var mediatrServices = new ServiceCollection();
        mediatrServices.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<MediatRPingHandler>();
            cfg.AddOpenBehavior(typeof(MediatRBehavior<,>));
        });
        _mediatrProvider = mediatrServices.BuildServiceProvider().CreateScope().ServiceProvider;
    }

    [Benchmark(Baseline = true)]
    public async Task<PingResponse> Swift_SendWithBehavior()
    {
        var mediator = _swiftProvider.GetRequiredService<IMediator>();
        return await mediator.SendAsync<PingRequest, PingResponse>(new PingRequest { Message = "hi" });
    }

    [Benchmark]
    public async Task<MediatRPingResponse> MediatR_SendWithBehavior()
    {
        var mediator = _mediatrProvider.GetRequiredService<MediatR.IMediator>();
        return await mediator.Send(new MediatRPing { Message = "hi" });
    }
}

[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class StreamBenchmarks
{
    private IServiceProvider _swiftProvider = null!;
    private IServiceProvider _mediatrProvider = null!;

    [GlobalSetup]
    public void Setup()
    {
        var swiftServices = new ServiceCollection();
        swiftServices.AddSwiftMediator();
        _swiftProvider = swiftServices.BuildServiceProvider().CreateScope().ServiceProvider;

        var mediatrServices = new ServiceCollection();
        mediatrServices.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<MediatRPingHandler>());
        _mediatrProvider = mediatrServices.BuildServiceProvider().CreateScope().ServiceProvider;
    }

    [Benchmark(Baseline = true)]
    public async Task<int> Swift_Stream()
    {
        var mediator = _swiftProvider.GetRequiredService<IMediator>();
        int sum = 0;
        await foreach (var item in mediator.CreateStream<RangeStreamRequest, int>(
            new RangeStreamRequest { Count = 100 }))
        {
            sum += item;
        }
        return sum;
    }

    [Benchmark]
    public async Task<int> MediatR_Stream()
    {
        var mediator = _mediatrProvider.GetRequiredService<MediatR.IMediator>();
        int sum = 0;
        await foreach (var item in mediator.CreateStream(new MediatRRangeStream { Count = 100 }))
        {
            sum += item;
        }
        return sum;
    }
}
