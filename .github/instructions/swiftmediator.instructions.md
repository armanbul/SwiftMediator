---
description: "Use when: developing with SwiftMediator, implementing mediator pattern, adding request handlers, notification handlers, pipeline behaviors, stream handlers, configuring DI, writing CQRS patterns, debugging mediator dispatch, or any code that references SwiftMediator.Core or SwiftMediator.Contracts namespaces."
applyTo: "**/*.cs"
---

# SwiftMediator — AI Reference Guide

SwiftMediator is a **source-generated**, **zero-allocation**, **AOT-compatible** mediator pattern library for .NET.  
Unlike MediatR (which uses runtime reflection), SwiftMediator uses a Roslyn incremental source generator to produce compile-time dispatch code — no `Dictionary<Type>` lookups, no `Activator.CreateInstance`, no reflection.

## Architecture Overview

```
┌─────────────────────────┐
│  SwiftMediator.Contracts │  ← netstandard2.0 (marker interfaces only)
│  IRequest<T>, INotific.  │
└──────────┬──────────────┘
           │ references
┌──────────▼──────────────┐     ┌──────────────────────────┐
│  SwiftMediator.Core      │     │  SwiftMediator.Generator  │
│  Handlers, Behaviors,    │◄────│  Roslyn IIncrementalGen.  │
│  Config, Publishers      │     │  netstandard2.0           │
│  net10;net8;ns2.0;net462 │     └──────────────────────────┘
└──────────┬──────────────┘
           │ generates at compile time
┌──────────▼──────────────┐
│  GeneratedMediator       │  ← compile-time switch dispatch
│  AddSwiftMediator()      │  ← DI registration extension
└──────────────────────────┘
```

**Target Frameworks:**
- `SwiftMediator.Core`: `net10.0`, `net8.0`, `netstandard2.0`, `net462`
- `SwiftMediator.Contracts`: `netstandard2.0`
- `SwiftMediator.Generator`: `netstandard2.0` (Roslyn analyzer)

---

## 1. Request / Response Pattern

### Define a request and its response:

```csharp
public record GetUserResponse(int Id, string Name);

public class GetUserQuery : IRequest<GetUserResponse>
{
    public int UserId { get; init; }
}
```

### Implement the handler:

```csharp
public class GetUserQueryHandler : IRequestHandler<GetUserQuery, GetUserResponse>
{
    public ValueTask<GetUserResponse> Handle(GetUserQuery request, CancellationToken cancellationToken)
    {
        return new ValueTask<GetUserResponse>(new GetUserResponse(request.UserId, "Alice"));
    }
}
```

### Send the request:

```csharp
var response = await mediator.SendAsync<GetUserQuery, GetUserResponse>(
    new GetUserQuery { UserId = 42 });
```

> **Key difference from MediatR:** SwiftMediator returns `ValueTask<T>` (not `Task<T>`) — zero heap allocation for synchronous completions.

---

## 2. Void (Unit) Requests

For commands that don't return a value, use `IRequest` (shorthand for `IRequest<Unit>`):

```csharp
public class DeleteUserCommand : IRequest
{
    public int UserId { get; init; }
}

public class DeleteUserHandler : IRequestHandler<DeleteUserCommand, Unit>
{
    public ValueTask<Unit> Handle(DeleteUserCommand request, CancellationToken cancellationToken)
    {
        // perform deletion
        return new ValueTask<Unit>(Unit.Value);
    }
}
```

Convenience extension — no need to specify `Unit` as type parameter:
```csharp
await mediator.SendAsync(new DeleteUserCommand { UserId = 42 });
```

---

## 3. Notifications (Pub/Sub)

One event, multiple handlers. All handlers are invoked automatically.

```csharp
public class OrderCreatedEvent : INotification
{
    public int OrderId { get; init; }
}

public class SendEmailHandler : INotificationHandler<OrderCreatedEvent>
{
    public ValueTask Handle(OrderCreatedEvent notification, CancellationToken cancellationToken)
    {
        // send email
        return default;
    }
}
```

### Publish strategies:

```csharp
// Sequential (default) — handlers run one after another
await mediator.PublishAsync(new OrderCreatedEvent { OrderId = 1 }, PublishStrategy.Sequential);

// Parallel — all handlers run concurrently via Task.WhenAll
await mediator.PublishAsync(new OrderCreatedEvent { OrderId = 1 }, PublishStrategy.Parallel);

// FireAndForget — handlers started but not awaited, exceptions suppressed
await mediator.PublishAsync(new OrderCreatedEvent { OrderId = 1 }, PublishStrategy.FireAndForget);
```

### Polymorphic notifications:

Handlers registered for a **base type/interface** are invoked for ALL derived concrete types:

```csharp
public interface IOrderEvent : INotification { }
public class OrderConfirmedEvent : IOrderEvent { public int OrderId { get; init; } }
public class OrderCancelledEvent : IOrderEvent { public int OrderId { get; init; } }

// This handler runs for EVERY IOrderEvent (confirmed, cancelled, etc.)
public class OrderAuditHandler : INotificationHandler<IOrderEvent>
{
    public ValueTask Handle(IOrderEvent notification, CancellationToken cancellationToken) { ... }
}

// This handler runs ONLY for OrderConfirmedEvent
public class ConfirmedHandler : INotificationHandler<OrderConfirmedEvent>
{
    public ValueTask Handle(OrderConfirmedEvent notification, CancellationToken cancellationToken) { ... }
}
```

When `OrderConfirmedEvent` is published → **both** `OrderAuditHandler` and `ConfirmedHandler` run.  
When `OrderCancelledEvent` is published → **only** `OrderAuditHandler` runs.

---

## 4. Streaming (IAsyncEnumerable)

```csharp
public class SearchQuery : IStreamRequest<SearchResult>
{
    public string Term { get; init; } = "";
}

public class SearchHandler : IStreamRequestHandler<SearchQuery, SearchResult>
{
    public async IAsyncEnumerable<SearchResult> Handle(
        SearchQuery request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var item in database.SearchAsync(request.Term))
            yield return item;
    }
}
```

Consume:
```csharp
await foreach (var result in mediator.CreateStream<SearchQuery, SearchResult>(new SearchQuery { Term = "foo" }))
{
    Console.WriteLine(result);
}
```

---

## 5. Pipeline Behaviors (Middleware)

`IPipelineBehavior<TRequest, TResponse>` wraps handler execution — like ASP.NET middleware.

```csharp
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async ValueTask<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        Console.WriteLine($"Handling {typeof(TRequest).Name}");
        var response = await next(); // call next behavior or handler
        Console.WriteLine($"Handled {typeof(TRequest).Name}");
        return response;
    }
}
```

**Fast-path optimization:** When no behaviors are registered for a request type, the handler is called directly — zero overhead, no delegate allocation.

**Execution order:** Behaviors registered first wrap outermost. Registration order = execution order.

### Stream Pipeline Behaviors:

```csharp
public class StreamLoggingBehavior<TRequest, TResponse> : IStreamPipelineBehavior<TRequest, TResponse>
    where TRequest : IStreamRequest<TResponse>
{
    public async IAsyncEnumerable<TResponse> Handle(
        TRequest request,
        StreamHandlerDelegate<TResponse> next,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Console.WriteLine("Stream starting");
        await foreach (var item in next())
            yield return item;
        Console.WriteLine("Stream completed");
    }
}
```

---

## 6. Pre/Post Processors

Run **before** and **after** the handler (inside behaviors), without the `next()` delegate pattern.

```csharp
public class ValidationPreProcessor<TRequest> : IRequestPreProcessor<TRequest>
    where TRequest : notnull
{
    public ValueTask Process(TRequest request, CancellationToken cancellationToken)
    {
        // validate request, throw if invalid
        return default;
    }
}

public class AuditPostProcessor<TRequest, TResponse> : IRequestPostProcessor<TRequest, TResponse>
    where TRequest : notnull
{
    public ValueTask Process(TRequest request, TResponse response, CancellationToken cancellationToken)
    {
        // log response for auditing
        return default;
    }
}
```

**Execution order:** `PreProcessor → Behavior(s) → Handler → PostProcessor`

---

## 7. Exception Pipeline

### Exception Actions (observe only — cannot suppress):

```csharp
public class MetricsExceptionAction<TRequest> : IRequestExceptionAction<TRequest, Exception>
    where TRequest : notnull
{
    public ValueTask Execute(TRequest request, Exception exception, CancellationToken cancellationToken)
    {
        // log metric, send alert — CANNOT suppress exception
        return default;
    }
}
```

### Exception Handlers (can suppress and provide fallback):

```csharp
public class FallbackExceptionHandler : IRequestExceptionHandler<MyRequest, MyResponse, InvalidOperationException>
{
    public ValueTask Handle(
        MyRequest request,
        InvalidOperationException exception,
        RequestExceptionHandlerState<MyResponse> state,
        CancellationToken cancellationToken)
    {
        state.SetHandled(new MyResponse { /* fallback */ }); // suppress exception
        return default;
    }
}
```

**Execution order:** Actions run **first** (always), then Handlers (can suppress via `state.SetHandled()`). If no handler suppresses → exception re-thrown.

---

## 8. Custom Notification Publisher

Override the built-in publish strategies entirely:

```csharp
public class BatchingPublisher : INotificationPublisher
{
    public async ValueTask Publish(
        IEnumerable<NotificationHandlerExecutor> handlerExecutors,
        INotification notification,
        CancellationToken cancellationToken)
    {
        foreach (var executor in handlerExecutors)
            await executor.HandlerCallback(notification, cancellationToken);
    }
}
```

Built-in publishers: `ForeachAwaitPublisher` (sequential), `TaskWhenAllPublisher` (parallel), `FireAndForgetPublisher`.

When a custom `INotificationPublisher` is registered, the `PublishStrategy` parameter is **ignored**.

---

## 9. ISender / IPublisher Interface Segregation

`IMediator = ISender + IPublisher`. Depend on the narrowest interface:

```csharp
public class MyService
{
    private readonly ISender _sender;  // can only send requests
    public MyService(ISender sender) => _sender = sender;
}

public class MyPublisher
{
    private readonly IPublisher _publisher;  // can only publish notifications
    public MyPublisher(IPublisher publisher) => _publisher = publisher;
}
```

All three resolve to the **same instance** (`GeneratedMediator`).

---

## 10. DI Configuration

### Basic registration:

```csharp
services.AddSwiftMediator(); // defaults: Handler=Transient, Mediator=Scoped
```

### Fluent configuration:

```csharp
services.AddSwiftMediator(cfg =>
{
    // Lifetime settings
    cfg.Lifetime = HandlerLifetime.Scoped;           // all handlers
    cfg.MediatorLifetime = HandlerLifetime.Singleton; // mediator itself

    // Assembly scanning — auto-discovers behaviors, processors, exception handlers
    cfg.RegisterServicesFromAssemblyContaining<Program>();

    // Or explicit registration:
    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>))
       .AddOpenBehavior(typeof(ValidationBehavior<,>))
       .AddRequestPreProcessor<AuditPreProcessor>()
       .AddRequestPostProcessor<CachePostProcessor>()
       .AddExceptionHandler<FallbackExceptionHandler>()
       .AddExceptionAction<MetricsExceptionAction>()
       .AddStreamBehavior<StreamLoggingBehavior>()
       .SetNotificationPublisher<BatchingPublisher>();
});
```

### What `AddSwiftMediator` generates:

1. Creates `MediatorServiceConfiguration`
2. Invokes user callback (if provided)
3. Registers `GeneratedMediator` as `IMediator`, `ISender`, `IPublisher`
4. Registers all **compile-time discovered** handlers
5. Registers open generic handlers
6. Calls `config.Apply()` — applies assembly scanning + fluent pipeline registrations + publisher

### Assembly scanning discovers:
- `IPipelineBehavior<,>`
- `IStreamPipelineBehavior<,>`
- `IRequestPreProcessor<>`
- `IRequestPostProcessor<,>`
- `IRequestExceptionHandler<,,>`
- `IRequestExceptionAction<,>`

> **Note:** Handlers (`IRequestHandler`, `INotificationHandler`, `IStreamRequestHandler`) are NOT scanned — the source generator discovers them at **compile time**.

---

## 11. Polymorphic Request Dispatch

Requests can implement a shared base interface for CQRS patterns:

```csharp
public interface ICommand : IRequest<CommandResult> { }

public class CreateUserCommand : ICommand
{
    public string Name { get; init; } = "";
}

public class CreateUserHandler : IRequestHandler<CreateUserCommand, CommandResult>
{
    public ValueTask<CommandResult> Handle(CreateUserCommand request, CancellationToken cancellationToken)
        => new(new CommandResult("Created"));
}
```

The source generator detects base types that implement `IRequest<T>` and generates additional dispatch branches.

---

## 12. Dynamic Dispatch

Send requests when the type is only known at runtime:

```csharp
object request = DeserializeFromQueue(message);
object? result = await mediator.SendAsync(request); // dynamic dispatch via switch
```

The generated code uses a `switch` statement over known concrete types — no reflection.

---

## Complete Interface Reference

| Interface | Namespace | Generic Args | Return |
|:---|:---|:---|:---|
| `IRequest<TResponse>` | `SwiftMediator.Core` | `out TResponse` | — |
| `IRequest` | `SwiftMediator.Core` | — | (= `IRequest<Unit>`) |
| `INotification` | `SwiftMediator.Core` | — | — |
| `IStreamRequest<TResponse>` | `SwiftMediator.Core` | `out TResponse` | — |
| `IMediator` | `SwiftMediator.Core` | — | (= `ISender + IPublisher`) |
| `ISender` | `SwiftMediator.Core` | — | `SendAsync`, `CreateStream` |
| `IPublisher` | `SwiftMediator.Core` | — | `PublishAsync` |
| `IRequestHandler<TRequest, TResponse>` | `SwiftMediator.Core` | 2 | `ValueTask<TResponse>` |
| `IRequestHandler<TRequest>` | `SwiftMediator.Core` | 1 | (= `IRequestHandler<T, Unit>`) |
| `INotificationHandler<TNotification>` | `SwiftMediator.Core` | 1 | `ValueTask` |
| `IStreamRequestHandler<TRequest, TResponse>` | `SwiftMediator.Core` | 2 | `IAsyncEnumerable<TResponse>` |
| `IPipelineBehavior<TRequest, TResponse>` | `SwiftMediator.Core` | 2 | `ValueTask<TResponse>` |
| `IStreamPipelineBehavior<TRequest, TResponse>` | `SwiftMediator.Core` | 2 | `IAsyncEnumerable<TResponse>` |
| `IRequestPreProcessor<TRequest>` | `SwiftMediator.Core` | 1 | `ValueTask` |
| `IRequestPostProcessor<TRequest, TResponse>` | `SwiftMediator.Core` | 2 | `ValueTask` |
| `IRequestExceptionHandler<TReq, TRes, TEx>` | `SwiftMediator.Core` | 3 | `ValueTask` |
| `IRequestExceptionAction<TRequest, TException>` | `SwiftMediator.Core` | 2 | `ValueTask` |
| `INotificationPublisher` | `SwiftMediator.Core` | — | `ValueTask` |

---

## Delegates

| Delegate | Signature |
|:---|:---|
| `RequestHandlerDelegate<TResponse>` | `ValueTask<TResponse> RequestHandlerDelegate<TResponse>()` |
| `StreamHandlerDelegate<TResponse>` | `IAsyncEnumerable<TResponse> StreamHandlerDelegate<out TResponse>()` |

---

## Enums

| Enum | Values | Usage |
|:---|:---|:---|
| `PublishStrategy` | `Sequential`, `Parallel`, `FireAndForget` | `PublishAsync()` parameter |
| `HandlerLifetime` | `Transient`, `Scoped`, `Singleton` | `MediatorServiceConfiguration.Lifetime` |

---

## Helper Types

| Type | Purpose |
|:---|:---|
| `Unit` | `readonly struct` — void response surrogate, `Unit.Value` |
| `RequestExceptionHandlerState<TResponse>` | `Handled: bool`, `Response: TResponse?`, `SetHandled(T)` |
| `NotificationHandlerExecutor` | `HandlerInstance: object`, `HandlerCallback: Func<INotification, CancellationToken, ValueTask>` |
| `MediatorExtensions` | `SendAsync<TRequest>(IMediator, TRequest, CT)` — void convenience |

---

## Compile-Time Diagnostics

| ID | Severity | Meaning |
|:---|:---|:---|
| `SWIFT001` | Error | Duplicate request handler for same `IRequest<T>` type |
| `SWIFT002` | Error | Duplicate stream handler for same `IStreamRequest<T>` type |
| `SWIFT003` | Warning | No handlers found in the assembly |
| `SWIFT004` | Info | Open generic handler detected and registered |

---

## Pipeline Execution Order

```
Request arrives at SendAsync<TRequest, TResponse>()
│
├── 1. IRequestPreProcessor<TRequest>.Process()        ← all pre-processors (sequential)
│
├── 2. IPipelineBehavior<TRequest, TResponse>          ← outermost behavior
│      └── next()
│          └── IPipelineBehavior (inner)               ← next behavior
│              └── next()
│                  └── IRequestHandler<TReq, TRes>     ← actual handler
│
├── 3. IRequestPostProcessor<TRequest, TResponse>      ← all post-processors (sequential)
│
└── [on exception]:
    ├── IRequestExceptionAction<TReq, Exception>       ← always runs (observe only)
    └── IRequestExceptionHandler<TReq, TRes, Exception>← can suppress via SetHandled()
```

---

## Common Patterns

### CQRS with SwiftMediator:

```csharp
// Commands
public interface ICommand : IRequest { }
public interface ICommand<TResult> : IRequest<TResult> { }

// Queries
public interface IQuery<TResult> : IRequest<TResult> { }

// Usage
public class CreateOrderCommand : ICommand { public int ProductId { get; init; } }
public class GetOrderQuery : IQuery<OrderDto> { public int OrderId { get; init; } }
```

### Validation Behavior:

```csharp
public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
        => _validators = validators;

    public async ValueTask<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var failures = _validators
            .Select(v => v.Validate(request))
            .SelectMany(r => r.Errors)
            .Where(f => f != null)
            .ToList();

        if (failures.Count > 0)
            throw new ValidationException(failures);

        return await next();
    }
}
```

### Caching Behavior:

```csharp
public interface ICacheable { string CacheKey { get; } TimeSpan? Expiration { get; } }

public class CachingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IDistributedCache _cache;

    public CachingBehavior(IDistributedCache cache) => _cache = cache;

    public async ValueTask<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (request is not ICacheable cacheable)
            return await next();

        var cached = await _cache.GetStringAsync(cacheable.CacheKey, ct);
        if (cached != null)
            return JsonSerializer.Deserialize<TResponse>(cached)!;

        var response = await next();
        await _cache.SetStringAsync(cacheable.CacheKey, JsonSerializer.Serialize(response),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = cacheable.Expiration ?? TimeSpan.FromMinutes(5) }, ct);
        return response;
    }
}
```

### Retry with Polly:

```csharp
public interface IRetryable { int MaxRetries { get; } }

public class RetryBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async ValueTask<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (request is not IRetryable retryable)
            return await next();

        var pipeline = new ResiliencePipelineBuilder<TResponse>()
            .AddRetry(new RetryStrategyOptions<TResponse> { MaxRetryAttempts = retryable.MaxRetries })
            .Build();

        return await pipeline.ExecuteAsync(async _ => await next(), ct);
    }
}
```

---

## MediatorServiceConfiguration Full API

```csharp
services.AddSwiftMediator(cfg =>
{
    // ── Lifetime ──
    cfg.Lifetime = HandlerLifetime.Transient;        // default: Transient
    cfg.MediatorLifetime = HandlerLifetime.Scoped;   // default: Scoped

    // ── Assembly Scanning ──
    cfg.RegisterServicesFromAssembly(assembly);
    cfg.RegisterServicesFromAssemblyContaining<T>();
    cfg.RegisterServicesFromAssemblyContaining(typeof(T));

    // ── Pipeline Behaviors ──
    cfg.AddOpenBehavior(typeof(MyBehavior<,>));              // open generic
    cfg.AddOpenBehavior(typeof(MyBehavior<,>), ServiceLifetime.Singleton); // custom lifetime
    cfg.AddBehavior<ClosedBehavior>();                        // closed

    // ── Stream Pipeline Behaviors ──
    cfg.AddOpenStreamBehavior(typeof(MyStreamBehavior<,>));
    cfg.AddStreamBehavior<ClosedStreamBehavior>();

    // ── Pre/Post Processors ──
    cfg.AddRequestPreProcessor<MyPreProcessor>();
    cfg.AddOpenRequestPreProcessor(typeof(OpenPreProcessor<>));
    cfg.AddRequestPostProcessor<MyPostProcessor>();
    cfg.AddOpenRequestPostProcessor(typeof(OpenPostProcessor<,>));

    // ── Exception Pipeline ──
    cfg.AddExceptionHandler<MyExceptionHandler>();
    cfg.AddExceptionAction<MyExceptionAction>();

    // ── Notification Publisher ──
    cfg.SetNotificationPublisher<TaskWhenAllPublisher>();

    // All methods are chainable:
    cfg.RegisterServicesFromAssemblyContaining<Program>()
       .AddOpenBehavior(typeof(LoggingBehavior<,>))
       .SetNotificationPublisher<TaskWhenAllPublisher>();
});
```

---

## Key Differences from MediatR

| Aspect | MediatR | SwiftMediator |
|:---|:---|:---|
| Dispatch | Runtime `Dictionary<Type>` + reflection | Compile-time `switch` (source-generated) |
| Return type | `Task<T>` | `ValueTask<T>` (zero alloc for sync) |
| No-behavior path | Still allocates pipeline | **Fast-path** — direct handler call |
| AOT / Trim | Not compatible | Compatible (`net8.0+`) |
| Error detection | Runtime `InvalidOperationException` | **Compile-time** `SWIFT001`/`SWIFT002` errors |
| Handler discovery | Runtime assembly scan | Compile-time Roslyn analysis |
| `Send` inference | `Send(request)` infers response | `SendAsync<TReq, TRes>(request)` — explicit |
| Void send | `Send(request)` returns `Unit` | `SendAsync(request)` convenience extension |
