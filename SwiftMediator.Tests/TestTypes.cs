using System.Runtime.CompilerServices;
using SwiftMediator.Core;

namespace SwiftMediator.Tests;

// ── Shared state for test assertions ──────────────────────────────────

public class TestState
{
    private readonly List<string> _actions = new();
    public IReadOnlyList<string> Actions
    {
        get { lock (_actions) return _actions.ToList(); }
    }

    public void Record(string action)
    {
        lock (_actions) _actions.Add(action);
    }

    public void Clear()
    {
        lock (_actions) _actions.Clear();
    }
}

// ── Request / Response types ──────────────────────────────────────────

public record TestResponse(string Value);

public class TestRequest : IRequest<TestResponse>
{
    public string Input { get; init; } = "";
}

// ── Void (Unit) request ───────────────────────────────────────────────

public class VoidRequest : IRequest
{
    public string Tag { get; init; } = "";
}

// ── Notification ──────────────────────────────────────────────────────

public class TestNotification : INotification
{
    public string Message { get; init; } = "";
}

// ── Stream request ────────────────────────────────────────────────────

public class NumberStreamRequest : IStreamRequest<int>
{
    public int Count { get; init; }
}

// ── Request Handlers ──────────────────────────────────────────────────

public class TestRequestHandler : IRequestHandler<TestRequest, TestResponse>
{
    private readonly TestState _state;
    public TestRequestHandler(TestState state) => _state = state;

    public ValueTask<TestResponse> Handle(TestRequest request, CancellationToken cancellationToken)
    {
        _state.Record($"Handled:{request.Input}");
        return new ValueTask<TestResponse>(new TestResponse($"Result:{request.Input}"));
    }
}

public class VoidRequestHandler : IRequestHandler<VoidRequest, Unit>
{
    private readonly TestState _state;
    public VoidRequestHandler(TestState state) => _state = state;

    public ValueTask<Unit> Handle(VoidRequest request, CancellationToken cancellationToken)
    {
        _state.Record($"Void:{request.Tag}");
        return new ValueTask<Unit>(Unit.Value);
    }
}

// ── Notification Handlers ─────────────────────────────────────────────

public class TestNotificationHandlerA : INotificationHandler<TestNotification>
{
    private readonly TestState _state;
    public TestNotificationHandlerA(TestState state) => _state = state;

    public ValueTask Handle(TestNotification notification, CancellationToken cancellationToken)
    {
        _state.Record($"A:{notification.Message}");
        return default;
    }
}

public class TestNotificationHandlerB : INotificationHandler<TestNotification>
{
    private readonly TestState _state;
    public TestNotificationHandlerB(TestState state) => _state = state;

    public ValueTask Handle(TestNotification notification, CancellationToken cancellationToken)
    {
        _state.Record($"B:{notification.Message}");
        return default;
    }
}

// ── Stream Handler ────────────────────────────────────────────────────

public class NumberStreamHandler : IStreamRequestHandler<NumberStreamRequest, int>
{
    public async IAsyncEnumerable<int> Handle(NumberStreamRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        for (int i = 1; i <= request.Count; i++)
        {
            yield return i;
        }
    }
}

// ── Pipeline Behaviors ────────────────────────────────────────────────

public class TrackingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    private readonly TestState _state;
    public TrackingBehavior(TestState state) => _state = state;

    public async ValueTask<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        _state.Record($"Behavior:Before:{typeof(TRequest).Name}");
        var response = await next();
        _state.Record($"Behavior:After:{typeof(TRequest).Name}");
        return response;
    }
}

// ── Throwing notification handler for FireAndForget tests ─────────────

public class ThrowingNotification : INotification
{
    public string Message { get; init; } = "";
}

public class ThrowingNotificationHandler : INotificationHandler<ThrowingNotification>
{
    public ValueTask Handle(ThrowingNotification notification, CancellationToken cancellationToken)
    {
        throw new InvalidOperationException("Intentional test exception");
    }
}

public class SafeNotificationHandler : INotificationHandler<ThrowingNotification>
{
    private readonly TestState _state;
    public SafeNotificationHandler(TestState state) => _state = state;

    public ValueTask Handle(ThrowingNotification notification, CancellationToken cancellationToken)
    {
        _state.Record($"Safe:{notification.Message}");
        return default;
    }
}

// ══════════════════════════════════════════════════════════════════════
// #2 — Exception Handler test types
// ══════════════════════════════════════════════════════════════════════

/// <summary>A request whose handler always throws.</summary>
public class FailingRequest : IRequest<TestResponse>
{
    public string Input { get; init; } = "";
}

public class FailingRequestHandler : IRequestHandler<FailingRequest, TestResponse>
{
    public ValueTask<TestResponse> Handle(FailingRequest request, CancellationToken cancellationToken)
    {
        throw new InvalidOperationException($"Boom:{request.Input}");
    }
}

/// <summary>Exception handler that catches the exception and returns a fallback response.</summary>
public class FallbackExceptionHandler : IRequestExceptionHandler<FailingRequest, TestResponse, Exception>
{
    private readonly TestState _state;
    public FallbackExceptionHandler(TestState state) => _state = state;

    public ValueTask Handle(FailingRequest request, Exception exception, RequestExceptionHandlerState<TestResponse> state, CancellationToken cancellationToken)
    {
        _state.Record($"ExHandler:{exception.Message}");
        state.SetHandled(new TestResponse($"Fallback:{request.Input}"));
        return default;
    }
}

/// <summary>Exception handler that does NOT handle — lets exception propagate.</summary>
public class NonHandlingExceptionHandler : IRequestExceptionHandler<FailingRequest, TestResponse, Exception>
{
    private readonly TestState _state;
    public NonHandlingExceptionHandler(TestState state) => _state = state;

    public ValueTask Handle(FailingRequest request, Exception exception, RequestExceptionHandlerState<TestResponse> state, CancellationToken cancellationToken)
    {
        _state.Record($"NonHandler:{exception.Message}");
        // Does NOT call state.SetHandled() — exception still propagates
        return default;
    }
}

// ══════════════════════════════════════════════════════════════════════
// #3 — Pre/Post Processor test types
// ══════════════════════════════════════════════════════════════════════

public class TrackingPreProcessor : IRequestPreProcessor<TestRequest>
{
    private readonly TestState _state;
    public TrackingPreProcessor(TestState state) => _state = state;

    public ValueTask Process(TestRequest request, CancellationToken cancellationToken)
    {
        _state.Record($"Pre:{request.Input}");
        return default;
    }
}

public class TrackingPostProcessor : IRequestPostProcessor<TestRequest, TestResponse>
{
    private readonly TestState _state;
    public TrackingPostProcessor(TestState state) => _state = state;

    public ValueTask Process(TestRequest request, TestResponse response, CancellationToken cancellationToken)
    {
        _state.Record($"Post:{response.Value}");
        return default;
    }
}

// ══════════════════════════════════════════════════════════════════════
// #5 — Polymorphic handler test types
// ══════════════════════════════════════════════════════════════════════

/// <summary>Base request interface for polymorphic dispatch.</summary>
public interface IBaseCommand : IRequest<TestResponse> { }

/// <summary>A concrete request that also implements a shared base interface.</summary>
public class ConcreteCommand : IBaseCommand
{
    public string Name { get; init; } = "";
}

public class ConcreteCommandHandler : IRequestHandler<ConcreteCommand, TestResponse>
{
    private readonly TestState _state;
    public ConcreteCommandHandler(TestState state) => _state = state;

    public ValueTask<TestResponse> Handle(ConcreteCommand request, CancellationToken cancellationToken)
    {
        _state.Record($"Poly:{request.Name}");
        return new ValueTask<TestResponse>(new TestResponse($"Poly:{request.Name}"));
    }
}

// ══════════════════════════════════════════════════════════════════════
// #1 — Exception Actions (observe without handling)
// ══════════════════════════════════════════════════════════════════════

public class TrackingExceptionAction : IRequestExceptionAction<FailingRequest, Exception>
{
    private readonly TestState _state;
    public TrackingExceptionAction(TestState state) => _state = state;

    public ValueTask Execute(FailingRequest request, Exception exception, CancellationToken cancellationToken)
    {
        _state.Record($"Action:{exception.Message}");
        return default;
    }
}

// ══════════════════════════════════════════════════════════════════════
// #2 — Stream Pipeline Behaviors
// ══════════════════════════════════════════════════════════════════════

public class DoubleStreamBehavior : IStreamPipelineBehavior<NumberStreamRequest, int>
{
    private readonly TestState _state;
    public DoubleStreamBehavior(TestState state) => _state = state;

    public async IAsyncEnumerable<int> Handle(NumberStreamRequest request, StreamHandlerDelegate<int> next, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        _state.Record("StreamBehavior:Before");
        await foreach (var item in next())
        {
            yield return item * 2;
        }
        _state.Record("StreamBehavior:After");
    }
}

// ══════════════════════════════════════════════════════════════════════
// #3 — Custom Notification Publisher
// ══════════════════════════════════════════════════════════════════════

public class TrackingNotificationPublisher : INotificationPublisher
{
    private readonly TestState _state;
    public TrackingNotificationPublisher(TestState state) => _state = state;

    public async ValueTask Publish(IEnumerable<NotificationHandlerExecutor> handlerExecutors, INotification notification, CancellationToken cancellationToken)
    {
        _state.Record("CustomPublisher:Start");
        foreach (var executor in handlerExecutors)
        {
            await executor.HandlerCallback(notification, cancellationToken).ConfigureAwait(false);
        }
        _state.Record("CustomPublisher:End");
    }
}

// ══════════════════════════════════════════════════════════════════════
// Polymorphic Notification test types
// ══════════════════════════════════════════════════════════════════════

public interface IBaseEvent : INotification { }

public class PolyNotification : IBaseEvent
{
    public string Data { get; init; } = "";
}

public class AnotherPolyNotification : IBaseEvent
{
    public string Data { get; init; } = "";
}

public class ConcretePolyHandler : INotificationHandler<PolyNotification>
{
    private readonly TestState _state;
    public ConcretePolyHandler(TestState state) => _state = state;

    public ValueTask Handle(PolyNotification notification, CancellationToken cancellationToken)
    {
        _state.Record($"Concrete:{notification.Data}");
        return default;
    }
}

public class BaseEventHandler : INotificationHandler<IBaseEvent>
{
    private readonly TestState _state;
    public BaseEventHandler(TestState state) => _state = state;

    public ValueTask Handle(IBaseEvent notification, CancellationToken cancellationToken)
    {
        _state.Record("BaseHandler");
        return default;
    }
}
