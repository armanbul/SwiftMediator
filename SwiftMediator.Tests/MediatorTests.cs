using Microsoft.Extensions.DependencyInjection;
using SwiftMediator.Core;
using Xunit;

namespace SwiftMediator.Tests;

public class MediatorTests
{
    private (IMediator Mediator, TestState State) CreateMediator(bool withBehaviors = false)
    {
        var state = new TestState();
        var services = new ServiceCollection();
        services.AddSingleton(state);
        services.AddSwiftMediator();

        if (withBehaviors)
        {
            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(TrackingBehavior<,>));
        }

        var provider = services.BuildServiceProvider();
        return (provider.GetRequiredService<IMediator>(), state);
    }

    // ── P0: Request dispatch ──────────────────────────────────────────

    [Fact]
    public async Task SendAsync_RequestHandler_ReturnsResponse()
    {
        var (mediator, state) = CreateMediator();

        var response = await mediator.SendAsync<TestRequest, TestResponse>(
            new TestRequest { Input = "hello" });

        Assert.Equal("Result:hello", response.Value);
        Assert.Contains("Handled:hello", state.Actions);
    }

    // ── P0: Void (Unit) handler ───────────────────────────────────────

    [Fact]
    public async Task SendAsync_VoidHandler_ReturnsUnit()
    {
        var (mediator, state) = CreateMediator();

        var result = await mediator.SendAsync<VoidRequest, Unit>(
            new VoidRequest { Tag = "test" });

        Assert.Equal(Unit.Value, result);
        Assert.Contains("Void:test", state.Actions);
    }

    [Fact]
    public async Task SendAsync_VoidHandler_ConvenienceExtension()
    {
        var (mediator, state) = CreateMediator();

        // Uses MediatorExtensions.SendAsync<TRequest> (single type param)
        await mediator.SendAsync(new VoidRequest { Tag = "ext" });

        Assert.Contains("Void:ext", state.Actions);
    }

    // ── P0: Dynamic dispatch ──────────────────────────────────────────

    [Fact]
    public async Task SendAsync_DynamicDispatch_Works()
    {
        var (mediator, state) = CreateMediator();

        object request = new TestRequest { Input = "dynamic" };
        var result = await mediator.SendAsync(request);

        Assert.IsType<TestResponse>(result);
        Assert.Equal("Result:dynamic", ((TestResponse)result!).Value);
    }

    [Fact]
    public async Task SendAsync_DynamicDispatch_UnknownType_Throws()
    {
        var (mediator, _) = CreateMediator();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => mediator.SendAsync("not a request").AsTask());
    }

    // ── P1: Fast-path (no behaviors) ──────────────────────────────────

    [Fact]
    public async Task SendAsync_NoBehaviors_DirectHandlerCall()
    {
        var (mediator, state) = CreateMediator(withBehaviors: false);

        var response = await mediator.SendAsync<TestRequest, TestResponse>(
            new TestRequest { Input = "fast" });

        Assert.Equal("Result:fast", response.Value);
        // Only the handler action should be recorded, no behavior actions
        Assert.Single(state.Actions);
        Assert.Contains("Handled:fast", state.Actions);
    }

    // ── P1: Pipeline behaviors ────────────────────────────────────────

    [Fact]
    public async Task SendAsync_WithBehavior_ExecutesPipeline()
    {
        var (mediator, state) = CreateMediator(withBehaviors: true);

        var response = await mediator.SendAsync<TestRequest, TestResponse>(
            new TestRequest { Input = "piped" });

        Assert.Equal("Result:piped", response.Value);
        Assert.Equal(3, state.Actions.Count);
        Assert.Equal("Behavior:Before:TestRequest", state.Actions[0]);
        Assert.Equal("Handled:piped", state.Actions[1]);
        Assert.Equal("Behavior:After:TestRequest", state.Actions[2]);
    }

    // ── Notifications: Sequential ─────────────────────────────────────

    [Fact]
    public async Task PublishAsync_Sequential_AllHandlersCalled()
    {
        var (mediator, state) = CreateMediator();

        await mediator.PublishAsync(
            new TestNotification { Message = "seq" },
            PublishStrategy.Sequential);

        Assert.Equal(2, state.Actions.Count);
        Assert.Contains("A:seq", state.Actions);
        Assert.Contains("B:seq", state.Actions);
    }

    // ── Notifications: Parallel ───────────────────────────────────────

    [Fact]
    public async Task PublishAsync_Parallel_AllHandlersCalled()
    {
        var (mediator, state) = CreateMediator();

        await mediator.PublishAsync(
            new TestNotification { Message = "par" },
            PublishStrategy.Parallel);

        Assert.Equal(2, state.Actions.Count);
        Assert.Contains("A:par", state.Actions);
        Assert.Contains("B:par", state.Actions);
    }

    // ── P1: FireAndForget — exception safety ──────────────────────────

    [Fact]
    public async Task PublishAsync_FireAndForget_DoesNotThrow()
    {
        var (mediator, state) = CreateMediator();

        // ThrowingNotificationHandler throws, SafeNotificationHandler succeeds
        // FireAndForget should suppress exceptions
        await mediator.PublishAsync(
            new ThrowingNotification { Message = "faf" },
            PublishStrategy.FireAndForget);

        // Give fire-and-forget tasks time to complete
        await Task.Delay(200);

        // The safe handler should have run
        Assert.Contains("Safe:faf", state.Actions);
    }

    // ── P3: Stream request ────────────────────────────────────────────

    [Fact]
    public async Task CreateStream_ReturnsAllItems()
    {
        var (mediator, _) = CreateMediator();

        var items = new List<int>();
        await foreach (var item in mediator.CreateStream<NumberStreamRequest, int>(
            new NumberStreamRequest { Count = 5 }))
        {
            items.Add(item);
        }

        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, items);
    }

    [Fact]
    public async Task CreateStream_EmptyRequest_ReturnsNoItems()
    {
        var (mediator, _) = CreateMediator();

        var items = new List<int>();
        await foreach (var item in mediator.CreateStream<NumberStreamRequest, int>(
            new NumberStreamRequest { Count = 0 }))
        {
            items.Add(item);
        }

        Assert.Empty(items);
    }

    // ── P0: Scoped registration validation ────────────────────────────

    [Fact]
    public void AddSwiftMediator_RegistersMediatorAsScoped()
    {
        var services = new ServiceCollection();
        services.AddSwiftMediator();

        var descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IMediator));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Scoped, descriptor!.Lifetime);
    }

    // ── Unknown request throws ────────────────────────────────────────

    [Fact]
    public async Task SendAsync_UnregisteredRequest_Throws()
    {
        // This is a compile-time guarantee from the source generator;
        // any IRequest<T> not registered will throw at runtime
        var (mediator, _) = CreateMediator();

        // Dynamic dispatch with unknown type
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => mediator.SendAsync(new object()).AsTask());
    }

    // ══════════════════════════════════════════════════════════════════
    // #2: Exception Pipeline
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ExceptionHandler_HandledException_ReturnsFallback()
    {
        var state = new TestState();
        var services = new ServiceCollection();
        services.AddSingleton(state);
        services.AddSwiftMediator();
        services.AddTransient<IRequestExceptionHandler<FailingRequest, TestResponse, Exception>, FallbackExceptionHandler>();

        var mediator = services.BuildServiceProvider().GetRequiredService<IMediator>();

        var response = await mediator.SendAsync<FailingRequest, TestResponse>(
            new FailingRequest { Input = "x" });

        Assert.Equal("Fallback:x", response.Value);
        Assert.Contains("ExHandler:Boom:x", state.Actions);
    }

    [Fact]
    public async Task ExceptionHandler_NotHandled_ExceptionPropagates()
    {
        var state = new TestState();
        var services = new ServiceCollection();
        services.AddSingleton(state);
        services.AddSwiftMediator();
        services.AddTransient<IRequestExceptionHandler<FailingRequest, TestResponse, Exception>, NonHandlingExceptionHandler>();

        var mediator = services.BuildServiceProvider().GetRequiredService<IMediator>();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => mediator.SendAsync<FailingRequest, TestResponse>(
                new FailingRequest { Input = "y" }).AsTask());

        Assert.Equal("Boom:y", ex.Message);
        Assert.Contains("NonHandler:Boom:y", state.Actions);
    }

    [Fact]
    public async Task ExceptionHandler_NoHandlerRegistered_ExceptionPropagates()
    {
        var (mediator, _) = CreateMediator();

        // FailingRequest handler throws but no exception handler is registered
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => mediator.SendAsync<FailingRequest, TestResponse>(
                new FailingRequest { Input = "z" }).AsTask());
    }

    // ══════════════════════════════════════════════════════════════════
    // #3: Pre/Post Processors
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task PrePostProcessors_ExecuteInCorrectOrder()
    {
        var state = new TestState();
        var services = new ServiceCollection();
        services.AddSingleton(state);
        services.AddSwiftMediator();
        services.AddTransient<IRequestPreProcessor<TestRequest>, TrackingPreProcessor>();
        services.AddTransient<IRequestPostProcessor<TestRequest, TestResponse>, TrackingPostProcessor>();

        var mediator = services.BuildServiceProvider().GetRequiredService<IMediator>();

        var response = await mediator.SendAsync<TestRequest, TestResponse>(
            new TestRequest { Input = "pp" });

        Assert.Equal("Result:pp", response.Value);

        // Order: Pre → Handler → Post
        Assert.Equal(3, state.Actions.Count);
        Assert.Equal("Pre:pp", state.Actions[0]);
        Assert.Equal("Handled:pp", state.Actions[1]);
        Assert.Equal("Post:Result:pp", state.Actions[2]);
    }

    [Fact]
    public async Task PrePostProcessors_WithBehavior_CorrectOrder()
    {
        var state = new TestState();
        var services = new ServiceCollection();
        services.AddSingleton(state);
        services.AddSwiftMediator();
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(TrackingBehavior<,>));
        services.AddTransient<IRequestPreProcessor<TestRequest>, TrackingPreProcessor>();
        services.AddTransient<IRequestPostProcessor<TestRequest, TestResponse>, TrackingPostProcessor>();

        var mediator = services.BuildServiceProvider().GetRequiredService<IMediator>();

        var response = await mediator.SendAsync<TestRequest, TestResponse>(
            new TestRequest { Input = "all" });

        Assert.Equal("Result:all", response.Value);

        // Order: Pre → Behavior:Before → Handler → Behavior:After → Post
        Assert.Equal(5, state.Actions.Count);
        Assert.Equal("Pre:all", state.Actions[0]);
        Assert.Equal("Behavior:Before:TestRequest", state.Actions[1]);
        Assert.Equal("Handled:all", state.Actions[2]);
        Assert.Equal("Behavior:After:TestRequest", state.Actions[3]);
        Assert.Equal("Post:Result:all", state.Actions[4]);
    }

    // ══════════════════════════════════════════════════════════════════
    // #4: Handler Lifetime Configuration
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public void AddSwiftMediator_DefaultLifetime_IsScoped()
    {
        var services = new ServiceCollection();
        services.AddSwiftMediator();

        var descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IMediator));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Scoped, descriptor!.Lifetime);
    }

    [Fact]
    public void AddSwiftMediator_ConfigureSingleton_RegistersSingleton()
    {
        var services = new ServiceCollection();
        services.AddSwiftMediator(opts =>
        {
            opts.MediatorLifetime = HandlerLifetime.Singleton;
            opts.Lifetime = HandlerLifetime.Singleton;
        });

        var mediatorDescriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IMediator));
        Assert.NotNull(mediatorDescriptor);
        Assert.Equal(ServiceLifetime.Singleton, mediatorDescriptor!.Lifetime);
    }

    [Fact]
    public void AddSwiftMediator_ConfigureScoped_RegistersScoped()
    {
        var services = new ServiceCollection();
        services.AddSwiftMediator(opts =>
        {
            opts.Lifetime = HandlerLifetime.Scoped;
        });

        // Handlers should all be Scoped
        var handlerDescriptors = services.Where(s =>
            s.ImplementationType != null &&
            s.ImplementationType != typeof(GeneratedMediator) &&
            s.ServiceType != typeof(IMediator)).ToList();

        Assert.All(handlerDescriptors, d => Assert.Equal(ServiceLifetime.Scoped, d.Lifetime));
    }

    [Fact]
    public void AddSwiftMediator_ConfigureTransient_RegistersTransient()
    {
        var services = new ServiceCollection();
        services.AddSwiftMediator(opts =>
        {
            opts.MediatorLifetime = HandlerLifetime.Transient;
        });

        var mediatorDescriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IMediator));
        Assert.NotNull(mediatorDescriptor);
        Assert.Equal(ServiceLifetime.Transient, mediatorDescriptor!.Lifetime);
    }

    // ══════════════════════════════════════════════════════════════════
    // #5: Polymorphic Handler
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task SendAsync_ConcreteRequest_DispatchesToHandler()
    {
        var state = new TestState();
        var services = new ServiceCollection();
        services.AddSingleton(state);
        services.AddSwiftMediator();
        var mediator = services.BuildServiceProvider().GetRequiredService<IMediator>();

        var response = await mediator.SendAsync<ConcreteCommand, TestResponse>(
            new ConcreteCommand { Name = "poly" });

        Assert.Equal("Poly:poly", response.Value);
        Assert.Contains("Poly:poly", state.Actions);
    }

    // ══════════════════════════════════════════════════════════════════
    // #1: Exception Actions
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ExceptionAction_AlwaysRuns_EvenWhenHandlerHandles()
    {
        var state = new TestState();
        var services = new ServiceCollection();
        services.AddSingleton(state);
        services.AddSwiftMediator();
        services.AddTransient<IRequestExceptionAction<FailingRequest, Exception>, TrackingExceptionAction>();
        services.AddTransient<IRequestExceptionHandler<FailingRequest, TestResponse, Exception>, FallbackExceptionHandler>();

        var mediator = services.BuildServiceProvider().GetRequiredService<IMediator>();

        var response = await mediator.SendAsync<FailingRequest, TestResponse>(
            new FailingRequest { Input = "act" });

        Assert.Equal("Fallback:act", response.Value);
        // Action runs first, then handler
        Assert.Contains("Action:Boom:act", state.Actions);
        Assert.Contains("ExHandler:Boom:act", state.Actions);
        var actionsList = state.Actions.ToList();
        Assert.True(actionsList.IndexOf("Action:Boom:act") < actionsList.IndexOf("ExHandler:Boom:act"));
    }

    [Fact]
    public async Task ExceptionAction_RunsWhenNoHandlerRegistered()
    {
        var state = new TestState();
        var services = new ServiceCollection();
        services.AddSingleton(state);
        services.AddSwiftMediator();
        services.AddTransient<IRequestExceptionAction<FailingRequest, Exception>, TrackingExceptionAction>();

        var mediator = services.BuildServiceProvider().GetRequiredService<IMediator>();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => mediator.SendAsync<FailingRequest, TestResponse>(
                new FailingRequest { Input = "act2" }).AsTask());

        Assert.Equal("Boom:act2", ex.Message);
        Assert.Contains("Action:Boom:act2", state.Actions);
    }

    // ══════════════════════════════════════════════════════════════════
    // #2: Stream Pipeline Behaviors
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task StreamBehavior_TransformsStream()
    {
        var state = new TestState();
        var services = new ServiceCollection();
        services.AddSingleton(state);
        services.AddSwiftMediator();
        services.AddTransient<IStreamPipelineBehavior<NumberStreamRequest, int>, DoubleStreamBehavior>();

        var mediator = services.BuildServiceProvider().GetRequiredService<IMediator>();

        var items = new List<int>();
        await foreach (var item in mediator.CreateStream<NumberStreamRequest, int>(
            new NumberStreamRequest { Count = 3 }))
        {
            items.Add(item);
        }

        Assert.Equal(new[] { 2, 4, 6 }, items);
        Assert.Contains("StreamBehavior:Before", state.Actions);
        Assert.Contains("StreamBehavior:After", state.Actions);
    }

    [Fact]
    public async Task StreamBehavior_NoBehavior_DirectStream()
    {
        var state = new TestState();
        var services = new ServiceCollection();
        services.AddSingleton(state);
        services.AddSwiftMediator();

        var mediator = services.BuildServiceProvider().GetRequiredService<IMediator>();

        var items = new List<int>();
        await foreach (var item in mediator.CreateStream<NumberStreamRequest, int>(
            new NumberStreamRequest { Count = 3 }))
        {
            items.Add(item);
        }

        Assert.Equal(new[] { 1, 2, 3 }, items);
    }

    // ══════════════════════════════════════════════════════════════════
    // #3: Custom Notification Publisher
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CustomNotificationPublisher_OverridesStrategy()
    {
        var state = new TestState();
        var services = new ServiceCollection();
        services.AddSingleton(state);
        services.AddSwiftMediator();
        services.AddSingleton<INotificationPublisher, TrackingNotificationPublisher>();

        var mediator = services.BuildServiceProvider().GetRequiredService<IMediator>();

        await mediator.PublishAsync(
            new TestNotification { Message = "custom" },
            PublishStrategy.Sequential);

        Assert.Contains("CustomPublisher:Start", state.Actions);
        Assert.Contains("A:custom", state.Actions);
        Assert.Contains("B:custom", state.Actions);
        Assert.Contains("CustomPublisher:End", state.Actions);
    }

    [Fact]
    public async Task CustomNotificationPublisher_NotRegistered_FallsBackToStrategy()
    {
        var (mediator, state) = CreateMediator();

        await mediator.PublishAsync(
            new TestNotification { Message = "fallback" },
            PublishStrategy.Sequential);

        // No custom publisher = standard strategy
        Assert.DoesNotContain("CustomPublisher:Start", state.Actions);
        Assert.Contains("A:fallback", state.Actions);
        Assert.Contains("B:fallback", state.Actions);
    }

    // ══════════════════════════════════════════════════════════════════
    // #4: ISender / IPublisher Interface Segregation
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public void ISender_IsRegistered()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new TestState());
        services.AddSwiftMediator();
        var provider = services.BuildServiceProvider().CreateScope().ServiceProvider;

        var sender = provider.GetRequiredService<ISender>();
        Assert.NotNull(sender);
        Assert.IsType<GeneratedMediator>(sender);
    }

    [Fact]
    public void IPublisher_IsRegistered()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new TestState());
        services.AddSwiftMediator();
        var provider = services.BuildServiceProvider().CreateScope().ServiceProvider;

        var publisher = provider.GetRequiredService<IPublisher>();
        Assert.NotNull(publisher);
        Assert.IsType<GeneratedMediator>(publisher);
    }

    [Fact]
    public async Task ISender_CanSendRequests()
    {
        var state = new TestState();
        var services = new ServiceCollection();
        services.AddSingleton(state);
        services.AddSwiftMediator();
        var provider = services.BuildServiceProvider().CreateScope().ServiceProvider;

        var sender = provider.GetRequiredService<ISender>();

        var response = await sender.SendAsync<TestRequest, TestResponse>(
            new TestRequest { Input = "sender" });

        Assert.Equal("Result:sender", response.Value);
    }

    [Fact]
    public async Task IPublisher_CanPublishNotifications()
    {
        var state = new TestState();
        var services = new ServiceCollection();
        services.AddSingleton(state);
        services.AddSwiftMediator();
        var provider = services.BuildServiceProvider().CreateScope().ServiceProvider;

        var publisher = provider.GetRequiredService<IPublisher>();

        await publisher.PublishAsync(
            new TestNotification { Message = "pub" },
            PublishStrategy.Sequential);

        Assert.Contains("A:pub", state.Actions);
        Assert.Contains("B:pub", state.Actions);
    }

    [Fact]
    public void ISender_IPublisher_SameInstanceAsMediator()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new TestState());
        services.AddSwiftMediator();
        var provider = services.BuildServiceProvider().CreateScope().ServiceProvider;

        var mediator = provider.GetRequiredService<IMediator>();
        var sender = provider.GetRequiredService<ISender>();
        var publisher = provider.GetRequiredService<IPublisher>();

        Assert.Same(mediator, sender);
        Assert.Same(mediator, publisher);
    }

    // ══════════════════════════════════════════════════════════════════
    // Fluent API: Explicit Registration
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task FluentApi_AddOpenBehavior_RegistersBehavior()
    {
        var state = new TestState();
        var services = new ServiceCollection();
        services.AddSingleton(state);
        services.AddSwiftMediator(cfg =>
        {
            cfg.AddOpenBehavior(typeof(TrackingBehavior<,>));
        });

        var mediator = services.BuildServiceProvider().GetRequiredService<IMediator>();
        var response = await mediator.SendAsync<TestRequest, TestResponse>(new TestRequest { Input = "fluent" });

        Assert.Equal("Result:fluent", response.Value);
        Assert.Equal("Behavior:Before:TestRequest", state.Actions[0]);
        Assert.Equal("Handled:fluent", state.Actions[1]);
        Assert.Equal("Behavior:After:TestRequest", state.Actions[2]);
    }

    [Fact]
    public async Task FluentApi_AddPreProcessor_Works()
    {
        var state = new TestState();
        var services = new ServiceCollection();
        services.AddSingleton(state);
        services.AddSwiftMediator(cfg => cfg.AddRequestPreProcessor<TrackingPreProcessor>());

        var mediator = services.BuildServiceProvider().GetRequiredService<IMediator>();
        await mediator.SendAsync<TestRequest, TestResponse>(new TestRequest { Input = "pre" });

        Assert.Equal("Pre:pre", state.Actions[0]);
        Assert.Equal("Handled:pre", state.Actions[1]);
    }

    [Fact]
    public async Task FluentApi_AddPostProcessor_Works()
    {
        var state = new TestState();
        var services = new ServiceCollection();
        services.AddSingleton(state);
        services.AddSwiftMediator(cfg => cfg.AddRequestPostProcessor<TrackingPostProcessor>());

        var mediator = services.BuildServiceProvider().GetRequiredService<IMediator>();
        await mediator.SendAsync<TestRequest, TestResponse>(new TestRequest { Input = "post" });

        Assert.Equal("Handled:post", state.Actions[0]);
        Assert.Equal("Post:Result:post", state.Actions[1]);
    }

    [Fact]
    public async Task FluentApi_AddExceptionHandler_Works()
    {
        var state = new TestState();
        var services = new ServiceCollection();
        services.AddSingleton(state);
        services.AddSwiftMediator(cfg => cfg.AddExceptionHandler<FallbackExceptionHandler>());

        var mediator = services.BuildServiceProvider().GetRequiredService<IMediator>();
        var response = await mediator.SendAsync<FailingRequest, TestResponse>(new FailingRequest { Input = "fex" });

        Assert.Equal("Fallback:fex", response.Value);
        Assert.Contains("ExHandler:Boom:fex", state.Actions);
    }

    [Fact]
    public async Task FluentApi_AddExceptionAction_Works()
    {
        var state = new TestState();
        var services = new ServiceCollection();
        services.AddSingleton(state);
        services.AddSwiftMediator(cfg => cfg.AddExceptionAction<TrackingExceptionAction>());

        var mediator = services.BuildServiceProvider().GetRequiredService<IMediator>();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => mediator.SendAsync<FailingRequest, TestResponse>(new FailingRequest { Input = "act3" }).AsTask());

        Assert.Contains("Action:Boom:act3", state.Actions);
    }

    [Fact]
    public async Task FluentApi_AddStreamBehavior_Works()
    {
        var state = new TestState();
        var services = new ServiceCollection();
        services.AddSingleton(state);
        services.AddSwiftMediator(cfg => cfg.AddStreamBehavior<DoubleStreamBehavior>());

        var mediator = services.BuildServiceProvider().GetRequiredService<IMediator>();

        var items = new List<int>();
        await foreach (var item in mediator.CreateStream<NumberStreamRequest, int>(new NumberStreamRequest { Count = 3 }))
            items.Add(item);

        Assert.Equal(new[] { 2, 4, 6 }, items);
    }

    [Fact]
    public async Task FluentApi_SetNotificationPublisher_Works()
    {
        var state = new TestState();
        var services = new ServiceCollection();
        services.AddSingleton(state);
        services.AddSwiftMediator(cfg => cfg.SetNotificationPublisher<TrackingNotificationPublisher>());

        var mediator = services.BuildServiceProvider().GetRequiredService<IMediator>();
        await mediator.PublishAsync(new TestNotification { Message = "np" }, PublishStrategy.Sequential);

        Assert.Contains("CustomPublisher:Start", state.Actions);
        Assert.Contains("A:np", state.Actions);
        Assert.Contains("B:np", state.Actions);
        Assert.Contains("CustomPublisher:End", state.Actions);
    }

    [Fact]
    public async Task FluentApi_Chaining_RegistersMultipleServices()
    {
        var state = new TestState();
        var services = new ServiceCollection();
        services.AddSingleton(state);
        services.AddSwiftMediator(cfg =>
        {
            cfg.AddOpenBehavior(typeof(TrackingBehavior<,>))
               .AddRequestPreProcessor<TrackingPreProcessor>()
               .AddRequestPostProcessor<TrackingPostProcessor>();
        });

        var mediator = services.BuildServiceProvider().GetRequiredService<IMediator>();
        var response = await mediator.SendAsync<TestRequest, TestResponse>(new TestRequest { Input = "chain" });

        Assert.Equal("Result:chain", response.Value);
        // Order: Pre → Behavior:Before → Handler → Behavior:After → Post
        Assert.Equal(5, state.Actions.Count);
        Assert.Equal("Pre:chain", state.Actions[0]);
        Assert.Equal("Behavior:Before:TestRequest", state.Actions[1]);
        Assert.Equal("Handled:chain", state.Actions[2]);
        Assert.Equal("Behavior:After:TestRequest", state.Actions[3]);
        Assert.Equal("Post:Result:chain", state.Actions[4]);
    }

    // ══════════════════════════════════════════════════════════════════
    // Assembly Scanning
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public void AssemblyScanning_RegistersPipelineServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new TestState());
        services.AddSwiftMediator(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<TestState>();
        });

        // Open generic behavior
        Assert.Contains(services, s =>
            s.ServiceType == typeof(IPipelineBehavior<,>) &&
            s.ImplementationType == typeof(TrackingBehavior<,>));

        // Closed pre-processor
        Assert.Contains(services, s =>
            s.ImplementationType == typeof(TrackingPreProcessor));

        // Closed post-processor
        Assert.Contains(services, s =>
            s.ImplementationType == typeof(TrackingPostProcessor));

        // Exception handler
        Assert.Contains(services, s =>
            s.ImplementationType == typeof(FallbackExceptionHandler));

        // Exception action
        Assert.Contains(services, s =>
            s.ImplementationType == typeof(TrackingExceptionAction));

        // Stream behavior
        Assert.Contains(services, s =>
            s.ImplementationType == typeof(DoubleStreamBehavior));
    }

    [Fact]
    public async Task AssemblyScanning_ScannedBehaviorsWork()
    {
        var state = new TestState();
        var services = new ServiceCollection();
        services.AddSingleton(state);
        services.AddSwiftMediator(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<TestState>();
        });

        var mediator = services.BuildServiceProvider().GetRequiredService<IMediator>();
        var response = await mediator.SendAsync<TestRequest, TestResponse>(new TestRequest { Input = "scan" });

        Assert.Equal("Result:scan", response.Value);
        // TrackingBehavior auto-discovered via scanning
        Assert.Contains("Behavior:Before:TestRequest", state.Actions);
        Assert.Contains("Handled:scan", state.Actions);
        Assert.Contains("Behavior:After:TestRequest", state.Actions);
        // Pre/Post processors auto-discovered
        Assert.Contains("Pre:scan", state.Actions);
        Assert.Contains("Post:Result:scan", state.Actions);
    }

    [Fact]
    public void AssemblyScanning_CombinesWithFluentApi()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new TestState());
        services.AddSwiftMediator(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<TestState>()
               .SetNotificationPublisher<TrackingNotificationPublisher>();
        });

        // Scanning should register pipeline services
        Assert.Contains(services, s =>
            s.ServiceType == typeof(IPipelineBehavior<,>) &&
            s.ImplementationType == typeof(TrackingBehavior<,>));

        // Explicit publisher should be registered
        Assert.Contains(services, s =>
            s.ServiceType == typeof(INotificationPublisher) &&
            s.ImplementationType == typeof(TrackingNotificationPublisher));
    }

    // ══════════════════════════════════════════════════════════════════
    // Polymorphic Notifications
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task PolymorphicNotification_ConcretePublish_InvokesBothHandlers()
    {
        var state = new TestState();
        var services = new ServiceCollection();
        services.AddSingleton(state);
        services.AddSwiftMediator();
        var mediator = services.BuildServiceProvider().GetRequiredService<IMediator>();

        await mediator.PublishAsync(new PolyNotification { Data = "test" });

        Assert.Contains("Concrete:test", state.Actions);
        Assert.Contains("BaseHandler", state.Actions);
    }

    [Fact]
    public async Task PolymorphicNotification_DifferentConcrete_InvokesOnlyBaseHandler()
    {
        var state = new TestState();
        var services = new ServiceCollection();
        services.AddSingleton(state);
        services.AddSwiftMediator();
        var mediator = services.BuildServiceProvider().GetRequiredService<IMediator>();

        await mediator.PublishAsync(new AnotherPolyNotification { Data = "other" });

        Assert.DoesNotContain("Concrete:other", state.Actions);
        Assert.Contains("BaseHandler", state.Actions);
    }

    [Fact]
    public async Task PolymorphicNotification_BaseTypePublish_InvokesConcreteToo()
    {
        var state = new TestState();
        var services = new ServiceCollection();
        services.AddSingleton(state);
        services.AddSwiftMediator();
        var mediator = services.BuildServiceProvider().GetRequiredService<IMediator>();

        // Publish as IBaseEvent but runtime type is PolyNotification
        IBaseEvent baseEvent = new PolyNotification { Data = "base" };
        await mediator.PublishAsync(baseEvent);

        Assert.Contains("Concrete:base", state.Actions);
        Assert.Contains("BaseHandler", state.Actions);
    }

    [Fact]
    public async Task PolymorphicNotification_Parallel_InvokesBothHandlers()
    {
        var state = new TestState();
        var services = new ServiceCollection();
        services.AddSingleton(state);
        services.AddSwiftMediator();
        var mediator = services.BuildServiceProvider().GetRequiredService<IMediator>();

        await mediator.PublishAsync(new PolyNotification { Data = "par" }, PublishStrategy.Parallel);

        Assert.Contains("Concrete:par", state.Actions);
        Assert.Contains("BaseHandler", state.Actions);
    }
}
