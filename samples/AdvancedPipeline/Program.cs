using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using SwiftMediator;
using SwiftMediator.Core;

namespace AdvancedPipeline;

// ---------------------------------------------------------------------------
//  Request types
// ---------------------------------------------------------------------------

public record TransferResult(bool Success, string Detail);

public class TransferFundsRequest : IRequest<TransferResult>
{
    public string FromAccount { get; init; } = "";
    public string ToAccount { get; init; } = "";
    public decimal Amount { get; init; }
}

public class TransferFundsHandler : IRequestHandler<TransferFundsRequest, TransferResult>
{
    public ValueTask<TransferResult> Handle(TransferFundsRequest request, CancellationToken ct)
    {
        if (request.Amount > 50_000)
            throw new InvalidOperationException($"Transfer limit exceeded: ${request.Amount}");

        Console.WriteLine($"    [Handler] Transferred ${request.Amount}: {request.FromAccount} -> {request.ToAccount}");
        return new ValueTask<TransferResult>(
            new TransferResult(true, $"${request.Amount} transferred successfully"));
    }
}

// ---------------------------------------------------------------------------
//  Pipeline behavior: Performance timing
// ---------------------------------------------------------------------------

public class PerformanceBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async ValueTask<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var response = await next();
        sw.Stop();
        Console.WriteLine($"    [Perf] {typeof(TRequest).Name} completed in {sw.ElapsedMilliseconds}ms");
        return response;
    }
}

// ---------------------------------------------------------------------------
//  Pre-processor: Audit logging
// ---------------------------------------------------------------------------

public class AuditPreProcessor<TRequest> : IRequestPreProcessor<TRequest>
    where TRequest : notnull
{
    public ValueTask Process(TRequest request, CancellationToken ct)
    {
        Console.WriteLine($"    [Audit] Processing {typeof(TRequest).Name} at {DateTime.UtcNow:HH:mm:ss}");
        return default;
    }
}

// ---------------------------------------------------------------------------
//  Post-processor: Response caching hint
// ---------------------------------------------------------------------------

public class CachePostProcessor<TRequest, TResponse> : IRequestPostProcessor<TRequest, TResponse>
    where TRequest : notnull
{
    public ValueTask Process(TRequest request, TResponse response, CancellationToken ct)
    {
        Console.WriteLine($"    [Cache] Response cached for {typeof(TRequest).Name}");
        return default;
    }
}

// ---------------------------------------------------------------------------
//  Exception action: Metrics (observe only, cannot suppress)
// ---------------------------------------------------------------------------

public class MetricsExceptionAction<TRequest, TException> : IRequestExceptionAction<TRequest, TException>
    where TRequest : notnull
    where TException : Exception
{
    public ValueTask Execute(TRequest request, TException exception, CancellationToken ct)
    {
        Console.WriteLine($"    [Metrics] Error recorded: {exception.GetType().Name} in {typeof(TRequest).Name}");
        return default;
    }
}

// ---------------------------------------------------------------------------
//  Exception handler: Fallback response (can suppress)
// ---------------------------------------------------------------------------

public class TransferExceptionHandler
    : IRequestExceptionHandler<TransferFundsRequest, TransferResult, InvalidOperationException>
{
    public ValueTask Handle(
        TransferFundsRequest request, InvalidOperationException exception,
        RequestExceptionHandlerState<TransferResult> state, CancellationToken ct)
    {
        Console.WriteLine($"    [Recovery] Handling error: {exception.Message}");
        state.SetHandled(new TransferResult(false, $"Declined: {exception.Message}"));
        return default;
    }
}

// ---------------------------------------------------------------------------
//  Polymorphic notifications
// ---------------------------------------------------------------------------

public interface IAuditEvent : INotification
{
    string Description { get; }
}

public class TransferCompletedEvent : IAuditEvent
{
    public string Description => "Transfer completed";
    public decimal Amount { get; init; }
}

public class TransferDeclinedEvent : IAuditEvent
{
    public string Description => "Transfer declined";
    public string Reason { get; init; } = "";
}

// Base handler: runs for ALL IAuditEvent types
public class AuditLogHandler : INotificationHandler<IAuditEvent>
{
    public ValueTask Handle(IAuditEvent notification, CancellationToken ct)
    {
        Console.WriteLine($"    [AuditLog] {notification.Description}");
        return default;
    }
}

// Concrete handler: only TransferCompletedEvent
public class TransferSuccessHandler : INotificationHandler<TransferCompletedEvent>
{
    public ValueTask Handle(TransferCompletedEvent notification, CancellationToken ct)
    {
        Console.WriteLine($"    [Success] ${notification.Amount} transfer confirmed");
        return default;
    }
}

// Concrete handler: only TransferDeclinedEvent
public class TransferDeclinedAlertHandler : INotificationHandler<TransferDeclinedEvent>
{
    public ValueTask Handle(TransferDeclinedEvent notification, CancellationToken ct)
    {
        Console.WriteLine($"    [Alert] Transfer declined: {notification.Reason}");
        return default;
    }
}

// ---------------------------------------------------------------------------
//  Streaming with pipeline behavior
// ---------------------------------------------------------------------------

public class StockPriceQuery : IStreamRequest<decimal>
{
    public string Symbol { get; init; } = "";
    public int Ticks { get; init; }
}

public class StockPriceHandler : IStreamRequestHandler<StockPriceQuery, decimal>
{
    public async IAsyncEnumerable<decimal> Handle(
        StockPriceQuery request,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var rng = new Random(42);
        decimal price = 150.00m;
        for (int i = 0; i < request.Ticks; i++)
        {
            price += (decimal)(rng.NextDouble() * 4 - 2);
            yield return Math.Round(price, 2);
            await Task.Yield();
        }
    }
}

public class StreamLoggingBehavior<TRequest, TResponse> : IStreamPipelineBehavior<TRequest, TResponse>
    where TRequest : IStreamRequest<TResponse>
{
    public async IAsyncEnumerable<TResponse> Handle(
        TRequest request, StreamHandlerDelegate<TResponse> next,
        [EnumeratorCancellation] CancellationToken ct)
    {
        Console.WriteLine($"    [StreamLog] Starting {typeof(TRequest).Name}");
        int count = 0;
        await foreach (var item in next())
        {
            count++;
            yield return item;
        }
        Console.WriteLine($"    [StreamLog] Completed: {count} items yielded");
    }
}

// ---------------------------------------------------------------------------
//  Custom notification publisher
// ---------------------------------------------------------------------------

public class PriorityPublisher : INotificationPublisher
{
    public async ValueTask Publish(
        IEnumerable<NotificationHandlerExecutor> executors,
        INotification notification,
        CancellationToken ct)
    {
        // Execute base-type handlers first, then concrete handlers
        var sorted = executors.OrderBy(e =>
            e.HandlerInstance.GetType().Name.Contains("Audit") ? 0 : 1);

        foreach (var executor in sorted)
        {
            Console.WriteLine($"    [Publisher] Dispatching to {executor.HandlerInstance.GetType().Name}");
            await executor.HandlerCallback(notification, ct);
        }
    }
}

// ---------------------------------------------------------------------------
//  Program
// ---------------------------------------------------------------------------

class Program
{
    static async Task Main()
    {
        var services = new ServiceCollection();
        services.AddSwiftMediator(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<Program>();
            cfg.SetNotificationPublisher<PriorityPublisher>();
        });

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        // 1. Full pipeline: pre-processor -> behavior -> handler -> post-processor
        Console.WriteLine("[1] Full Pipeline (successful transfer)");
        Console.WriteLine("    Order: Audit -> Performance -> Handler -> Cache");
        var result = await mediator.SendAsync<TransferFundsRequest, TransferResult>(
            new TransferFundsRequest
            {
                FromAccount = "ACC-001",
                ToAccount = "ACC-002",
                Amount = 1_500.00m
            });
        Console.WriteLine($"    Result: {result.Detail}");
        Console.WriteLine();

        // 2. Exception pipeline: action (observe) -> handler (suppress + fallback)
        Console.WriteLine("[2] Exception Pipeline (transfer over limit)");
        Console.WriteLine("    Order: Metrics Action -> Exception Handler -> Fallback");
        var declined = await mediator.SendAsync<TransferFundsRequest, TransferResult>(
            new TransferFundsRequest
            {
                FromAccount = "ACC-001",
                ToAccount = "ACC-003",
                Amount = 100_000.00m
            });
        Console.WriteLine($"    Result: {declined.Detail}");
        Console.WriteLine();

        // 3. Polymorphic notifications
        Console.WriteLine("[3] Polymorphic Notifications");
        Console.WriteLine("    AuditLogHandler listens to ALL IAuditEvent types");
        Console.WriteLine();

        Console.WriteLine("    Publishing TransferCompletedEvent:");
        await mediator.PublishAsync(new TransferCompletedEvent { Amount = 1_500.00m });
        Console.WriteLine();

        Console.WriteLine("    Publishing TransferDeclinedEvent:");
        await mediator.PublishAsync(new TransferDeclinedEvent { Reason = "Limit exceeded" });
        Console.WriteLine();

        // 4. Streaming with pipeline
        Console.WriteLine("[4] Streaming with Pipeline (stock prices)");
        Console.Write("    ACME: ");
        await foreach (var price in mediator.CreateStream<StockPriceQuery, decimal>(
            new StockPriceQuery { Symbol = "ACME", Ticks = 8 }))
        {
            Console.Write($"${price} ");
        }
        Console.WriteLine();
        Console.WriteLine();

        // 5. Handler lifetime configuration
        Console.WriteLine("[5] Configuration Options");
        Console.WriteLine("    services.AddSwiftMediator(cfg => {");
        Console.WriteLine("        cfg.Lifetime = HandlerLifetime.Scoped;");
        Console.WriteLine("        cfg.MediatorLifetime = HandlerLifetime.Singleton;");
        Console.WriteLine("        cfg.RegisterServicesFromAssemblyContaining<Program>();");
        Console.WriteLine("        cfg.SetNotificationPublisher<PriorityPublisher>();");
        Console.WriteLine("    });");
    }
}
