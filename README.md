# SwiftMediator

**High-performance, source-generated mediator pattern for .NET** — a drop-in replacement for MediatR with compile-time dispatch, zero-allocation fast paths, and full AOT/Trim compatibility.

[![CI](https://github.com/armanbul/SwiftMediator/actions/workflows/ci.yml/badge.svg)](https://github.com/armanbul/SwiftMediator/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/SwiftMediator)](https://www.nuget.org/packages/SwiftMediator)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://opensource.org/licenses/MIT)

## Why SwiftMediator?

| | MediatR | SwiftMediator |
|:---|:---:|:---:|
| Dispatch | Runtime reflection + `Dictionary<Type>` | **Compile-time `switch`** (source-generated) |
| Return type | `Task<T>` | **`ValueTask<T>`** (zero alloc for sync) |
| No-behavior path | Allocates pipeline | **Fast-path** — direct call, zero overhead |
| AOT / Trim | Not compatible | **Fully compatible** (net8.0+) |
| Error detection | Runtime exceptions | **Compile-time** diagnostics |
| Frameworks | net8.0, netstandard2.0 | **net10.0, net8.0, netstandard2.0, net462** |
| License | **Paid license key required** | **MIT — free forever** |

**Full MediatR feature parity** — 20/20 features including polymorphic notifications.

## Installation

```bash
dotnet add package SwiftMediator
```

For shared/API projects that only need marker interfaces:

```bash
dotnet add package SwiftMediator.Contracts
```

## Quick Start

### 1. Define a Request and Handler

```csharp
using SwiftMediator.Core;

public record PongResponse(string Reply);

public class PingRequest : IRequest<PongResponse>
{
    public string Message { get; init; } = "";
}

public class PingHandler : IRequestHandler<PingRequest, PongResponse>
{
    public ValueTask<PongResponse> Handle(PingRequest request, CancellationToken ct)
    {
        return new ValueTask<PongResponse>(new PongResponse($"Pong: {request.Message}"));
    }
}
```

### 2. Register and Use

```csharp
var services = new ServiceCollection();

services.AddSwiftMediator(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();
});

var provider = services.BuildServiceProvider();
var mediator = provider.GetRequiredService<IMediator>();

var response = await mediator.SendAsync<PingRequest, PongResponse>(
    new PingRequest { Message = "Hello!" });
// response.Reply == "Pong: Hello!"
```

## Examples

| Sample | Description |
|:---|:---|
| [BasicUsage](samples/BasicUsage) | Request/response, void commands, notifications, streaming, dynamic dispatch |
| [CqrsWithValidation](samples/CqrsWithValidation) | CQRS pattern with commands, queries, validation pipeline, domain events |
| [AdvancedPipeline](samples/AdvancedPipeline) | Full pipeline behaviors, exception handling, polymorphic notifications, stream pipeline, custom publisher |

## Features

### Void (Unit) Requests

```csharp
public class DeleteUserCommand : IRequest
{
    public int UserId { get; init; }
}

public class DeleteUserHandler : IRequestHandler<DeleteUserCommand, Unit>
{
    public ValueTask<Unit> Handle(DeleteUserCommand request, CancellationToken ct)
    {
        // perform deletion
        return new ValueTask<Unit>(Unit.Value);
    }
}

// Convenience — no need to specify Unit:
await mediator.SendAsync(new DeleteUserCommand { UserId = 42 });
```

### Notifications (Pub/Sub)

```csharp
public class OrderCreatedEvent : INotification
{
    public int OrderId { get; init; }
}

public class EmailHandler : INotificationHandler<OrderCreatedEvent>
{
    public ValueTask Handle(OrderCreatedEvent notification, CancellationToken ct)
    {
        // send email
        return default;
    }
}

// Publish strategies:
await mediator.PublishAsync(evt, PublishStrategy.Sequential);   // one by one (default)
await mediator.PublishAsync(evt, PublishStrategy.Parallel);     // Task.WhenAll
await mediator.PublishAsync(evt, PublishStrategy.FireAndForget); // fire & forget
```

### Polymorphic Notifications

Handlers registered for a base type/interface are invoked for **all** derived types:

```csharp
public interface IOrderEvent : INotification { }
public class OrderConfirmedEvent : IOrderEvent { }
public class OrderCancelledEvent : IOrderEvent { }

// Runs for ALL IOrderEvent types:
public class AuditHandler : INotificationHandler<IOrderEvent> { ... }

// Runs only for OrderConfirmedEvent:
public class ConfirmedHandler : INotificationHandler<OrderConfirmedEvent> { ... }
```

### Streaming (IAsyncEnumerable)

```csharp
public class SearchQuery : IStreamRequest<SearchResult>
{
    public string Term { get; init; } = "";
}

public class SearchHandler : IStreamRequestHandler<SearchQuery, SearchResult>
{
    public async IAsyncEnumerable<SearchResult> Handle(
        SearchQuery request, [EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var item in db.SearchAsync(request.Term))
            yield return item;
    }
}

await foreach (var result in mediator.CreateStream<SearchQuery, SearchResult>(query))
    Console.WriteLine(result);
```

### Pipeline Behaviors (Middleware)

```csharp
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async ValueTask<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        Console.WriteLine($"Handling {typeof(TRequest).Name}");
        var response = await next();
        Console.WriteLine($"Handled {typeof(TRequest).Name}");
        return response;
    }
}
```

> **Fast-path:** When no behaviors are registered, the handler is called directly — zero overhead.

### Pre/Post Processors

```csharp
public class ValidationPreProcessor<TRequest> : IRequestPreProcessor<TRequest>
    where TRequest : notnull
{
    public ValueTask Process(TRequest request, CancellationToken ct) { /* validate */ return default; }
}

public class AuditPostProcessor<TReq, TRes> : IRequestPostProcessor<TReq, TRes>
    where TReq : notnull
{
    public ValueTask Process(TReq request, TRes response, CancellationToken ct) { /* audit */ return default; }
}
```

**Execution order:** `PreProcessor → Behavior(s) → Handler → PostProcessor`

### Exception Pipeline

```csharp
// Actions — observe only (logging, metrics), cannot suppress
public class MetricsAction<TReq> : IRequestExceptionAction<TReq, Exception>
    where TReq : notnull
{
    public ValueTask Execute(TReq request, Exception ex, CancellationToken ct)
    {
        // record metrics
        return default;
    }
}

// Handlers — can suppress exception and return fallback
public class FallbackHandler : IRequestExceptionHandler<MyRequest, MyResponse, InvalidOperationException>
{
    public ValueTask Handle(MyRequest req, InvalidOperationException ex,
        RequestExceptionHandlerState<MyResponse> state, CancellationToken ct)
    {
        state.SetHandled(new MyResponse { /* fallback */ });
        return default;
    }
}
```

**Order:** Actions run first (always), then Handlers (can suppress via `SetHandled()`).

### Stream Pipeline Behaviors

```csharp
public class StreamLogging<TReq, TRes> : IStreamPipelineBehavior<TReq, TRes>
    where TReq : IStreamRequest<TRes>
{
    public async IAsyncEnumerable<TRes> Handle(
        TReq request, StreamHandlerDelegate<TRes> next,
        [EnumeratorCancellation] CancellationToken ct)
    {
        Console.WriteLine("Stream starting");
        await foreach (var item in next())
            yield return item;
        Console.WriteLine("Stream completed");
    }
}
```

### Custom Notification Publisher

```csharp
public class BatchPublisher : INotificationPublisher
{
    public async ValueTask Publish(
        IEnumerable<NotificationHandlerExecutor> executors,
        INotification notification, CancellationToken ct)
    {
        foreach (var executor in executors)
            await executor.HandlerCallback(notification, ct);
    }
}
```

Built-in: `ForeachAwaitPublisher`, `TaskWhenAllPublisher`, `FireAndForgetPublisher`.

### ISender / IPublisher Segregation

```csharp
// Depend on the narrowest interface:
public class MyService(ISender sender) { }    // can only send requests
public class MyPub(IPublisher publisher) { }  // can only publish notifications
```

All three (`IMediator`, `ISender`, `IPublisher`) resolve to the same instance.

## DI Configuration

### Fluent API

```csharp
services.AddSwiftMediator(cfg =>
{
    // Lifetime
    cfg.Lifetime = HandlerLifetime.Scoped;           // handlers (default: Transient)
    cfg.MediatorLifetime = HandlerLifetime.Singleton; // mediator (default: Scoped)

    // Assembly scanning — auto-discovers behaviors, processors, exception handlers
    cfg.RegisterServicesFromAssemblyContaining<Program>();

    // Or register explicitly:
    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>))
       .AddOpenBehavior(typeof(ValidationBehavior<,>))
       .AddRequestPreProcessor<AuditPreProcessor>()
       .AddRequestPostProcessor<CachePostProcessor>()
       .AddExceptionHandler<FallbackExceptionHandler>()
       .AddExceptionAction<MetricsAction>()
       .AddStreamBehavior<StreamLoggingBehavior>()
       .SetNotificationPublisher<TaskWhenAllPublisher>();
});
```

### Assembly Scanning Discovers

- `IPipelineBehavior<,>` / `IStreamPipelineBehavior<,>`
- `IRequestPreProcessor<>` / `IRequestPostProcessor<,>`
- `IRequestExceptionHandler<,,>` / `IRequestExceptionAction<,>`

> **Handlers** (`IRequestHandler`, `INotificationHandler`, `IStreamRequestHandler`) are discovered at **compile time** by the source generator — not by assembly scanning.

## Compile-Time Diagnostics

| ID | Severity | Description |
|:---|:---|:---|
| `SWIFT001` | Error | Duplicate request handler |
| `SWIFT002` | Error | Duplicate stream handler |
| `SWIFT003` | Warning | No handlers found |
| `SWIFT004` | Info | Open generic handler registered |

## Supported Frameworks

| Package | Targets |
|:---|:---|
| `SwiftMediator` | `net10.0`, `net8.0`, `netstandard2.0`, `net462` |
| `SwiftMediator.Contracts` | `netstandard2.0` |

AOT and trim compatible on `net8.0+`.

## Feature List

- [x] Request/Response (`IRequest<T>` → `IRequestHandler<T, R>`)
- [x] Void requests (`IRequest` → `Unit`)
- [x] Notifications (`INotification` → `INotificationHandler<T>`)
- [x] Polymorphic notifications (base type handlers invoked for derived types)
- [x] Streaming (`IStreamRequest<T>` → `IAsyncEnumerable<T>`)
- [x] Pipeline behaviors (`IPipelineBehavior<T, R>`)
- [x] Stream pipeline behaviors (`IStreamPipelineBehavior<T, R>`)
- [x] Pre/Post processors
- [x] Exception handlers (suppress + fallback)
- [x] Exception actions (observe only)
- [x] Custom notification publisher (`INotificationPublisher`)
- [x] ISender / IPublisher interface segregation
- [x] Fluent DI configuration
- [x] Assembly scanning
- [x] Open generic handler support
- [x] Polymorphic request dispatch
- [x] Dynamic dispatch (`SendAsync(object)`)
- [x] Handler lifetime configuration (Transient / Scoped / Singleton)
- [x] Compile-time diagnostics
- [x] Multi-framework support (net10.0, net8.0, netstandard2.0, net462)

## Migrating from MediatR

SwiftMediator uses the **same API patterns** as MediatR — migration is straightforward:

| MediatR | SwiftMediator |
|:---|:---|
| `services.AddMediatR(cfg => ...)` | `services.AddSwiftMediator(cfg => ...)` |
| `cfg.RegisterServicesFromAssemblyContaining<T>()` | Same |
| `cfg.AddOpenBehavior(typeof(T<,>))` | Same |
| `cfg.AddRequestPreProcessor<T>()` | Same |
| `cfg.AddRequestPostProcessor<T>()` | Same |
| `cfg.AddStreamBehavior<T>()` | Same |
| `IRequestHandler<TReq, TRes>` returns `Task<T>` | Returns `ValueTask<T>` |
| `cfg.LicenseKey = "..."` | **Not needed — MIT licensed** |

> **Note:** MediatR now requires a [paid license key](https://mediatr.io). SwiftMediator is and will remain **free and open source** under the MIT license.

## Support

If you find SwiftMediator useful, consider buying me a coffee:

[![Buy Me a Coffee](https://camo.githubusercontent.com/0cf29a542375e1a46e84d8bf5805a4e5c0a6ee98b6547ccdc0c55eed49d99c69/68747470733a2f2f63646e2e6275796d6561636f666665652e636f6d2f627574746f6e732f76322f64656661756c742d79656c6c6f772e706e67)](https://www.buymeacoffee.com/armanbulk)

## License

MIT
