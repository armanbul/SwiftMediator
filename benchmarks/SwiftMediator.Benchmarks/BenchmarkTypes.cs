// ═══════════════════════════════════════════════════════════════════
// SwiftMediator types
// ═══════════════════════════════════════════════════════════════════
using SwiftMediator.Core;

namespace SwiftMediator.Benchmarks;

// ── Request / Response ────────────────────────────────────────────
public record PingResponse(string Pong);

public class PingRequest : IRequest<PingResponse>
{
    public string Message { get; init; } = "";
}

public class PingHandler : IRequestHandler<PingRequest, PingResponse>
{
    public ValueTask<PingResponse> Handle(PingRequest request, CancellationToken cancellationToken)
        => new(new PingResponse(request.Message));
}

// ── Void (Unit) request ───────────────────────────────────────────
public class FireCommand : IRequest
{
    public int Value { get; init; }
}

public class FireHandler : IRequestHandler<FireCommand, Unit>
{
    public ValueTask<Unit> Handle(FireCommand request, CancellationToken cancellationToken)
        => new(Unit.Value);
}

// ── Notification ──────────────────────────────────────────────────
public class PingNotification : INotification
{
    public string Message { get; init; } = "";
}

public class PingNotificationHandlerA : INotificationHandler<PingNotification>
{
    public ValueTask Handle(PingNotification notification, CancellationToken cancellationToken)
        => default;
}

public class PingNotificationHandlerB : INotificationHandler<PingNotification>
{
    public ValueTask Handle(PingNotification notification, CancellationToken cancellationToken)
        => default;
}

// ── Pipeline Behavior ─────────────────────────────────────────────
public class BenchmarkBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async ValueTask<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        => await next();
}

// ── Stream ────────────────────────────────────────────────────────
public class RangeStreamRequest : IStreamRequest<int>
{
    public int Count { get; init; }
}

public class RangeStreamHandler : IStreamRequestHandler<RangeStreamRequest, int>
{
    public async IAsyncEnumerable<int> Handle(RangeStreamRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        for (int i = 0; i < request.Count; i++)
            yield return i;
    }
}

// ═══════════════════════════════════════════════════════════════════
// MediatR types (same logic, MediatR interfaces)
// ═══════════════════════════════════════════════════════════════════

public record MediatRPingResponse(string Pong);

public class MediatRPing : MediatR.IRequest<MediatRPingResponse>
{
    public string Message { get; init; } = "";
}

public class MediatRPingHandler : MediatR.IRequestHandler<MediatRPing, MediatRPingResponse>
{
    public Task<MediatRPingResponse> Handle(MediatRPing request, CancellationToken cancellationToken)
        => Task.FromResult(new MediatRPingResponse(request.Message));
}

public class MediatRFire : MediatR.IRequest
{
    public int Value { get; init; }
}

public class MediatRFireHandler : MediatR.IRequestHandler<MediatRFire>
{
    public Task Handle(MediatRFire request, CancellationToken cancellationToken)
        => Task.CompletedTask;
}

public class MediatRPingNotification : MediatR.INotification
{
    public string Message { get; init; } = "";
}

public class MediatRPingNotificationHandlerA : MediatR.INotificationHandler<MediatRPingNotification>
{
    public Task Handle(MediatRPingNotification notification, CancellationToken cancellationToken)
        => Task.CompletedTask;
}

public class MediatRPingNotificationHandlerB : MediatR.INotificationHandler<MediatRPingNotification>
{
    public Task Handle(MediatRPingNotification notification, CancellationToken cancellationToken)
        => Task.CompletedTask;
}

public class MediatRBehavior<TRequest, TResponse> : MediatR.IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, MediatR.RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        => await next();
}

public class MediatRRangeStream : MediatR.IStreamRequest<int>
{
    public int Count { get; init; }
}

public class MediatRRangeStreamHandler : MediatR.IStreamRequestHandler<MediatRRangeStream, int>
{
    public async IAsyncEnumerable<int> Handle(MediatRRangeStream request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        for (int i = 0; i < request.Count; i++)
            yield return i;
    }
}
