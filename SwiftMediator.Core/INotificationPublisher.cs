namespace SwiftMediator.Core;

/// <summary>
/// Defines a custom strategy for publishing notifications to a set of handlers.
/// Register as a service to override the default notification dispatching behavior.
/// </summary>
public interface INotificationPublisher
{
    /// <summary>
    /// Publishes a notification to the given handlers.
    /// </summary>
    /// <param name="handlerExecutors">The set of handler executors to invoke</param>
    /// <param name="notification">The notification to publish</param>
    /// <param name="cancellationToken">Cancellation token</param>
    ValueTask Publish(IEnumerable<NotificationHandlerExecutor> handlerExecutors, INotification notification, CancellationToken cancellationToken);
}

/// <summary>
/// Wraps a notification handler instance with its execution delegate.
/// Provides access to the handler instance for custom publisher implementations
/// that may need to inspect, order, or filter handlers.
/// </summary>
public sealed class NotificationHandlerExecutor
{
    /// <summary>
    /// The handler instance.
    /// </summary>
    public object HandlerInstance { get; }

    /// <summary>
    /// The delegate that invokes the handler.
    /// </summary>
    public Func<INotification, CancellationToken, ValueTask> HandlerCallback { get; }

    /// <summary>
    /// Initializes a new <see cref="NotificationHandlerExecutor"/>.
    /// </summary>
    /// <param name="handlerInstance">The handler instance.</param>
    /// <param name="handlerCallback">The delegate that invokes the handler.</param>
    public NotificationHandlerExecutor(object handlerInstance, Func<INotification, CancellationToken, ValueTask> handlerCallback)
    {
        HandlerInstance = handlerInstance;
        HandlerCallback = handlerCallback;
    }
}

/// <summary>
/// Default notification publisher that executes handlers sequentially (foreach-await).
/// This is the default when no custom <see cref="INotificationPublisher"/> is registered.
/// </summary>
public class ForeachAwaitPublisher : INotificationPublisher
{
    /// <inheritdoc/>
    public async ValueTask Publish(IEnumerable<NotificationHandlerExecutor> handlerExecutors, INotification notification, CancellationToken cancellationToken)
    {
        foreach (var executor in handlerExecutors)
        {
            await executor.HandlerCallback(notification, cancellationToken).ConfigureAwait(false);
        }
    }
}

/// <summary>
/// Notification publisher that awaits all handlers in parallel using <see cref="Task.WhenAll(System.Threading.Tasks.Task[])"/>.
/// </summary>
public class TaskWhenAllPublisher : INotificationPublisher
{
    /// <inheritdoc/>
    public async ValueTask Publish(IEnumerable<NotificationHandlerExecutor> handlerExecutors, INotification notification, CancellationToken cancellationToken)
    {
        var capacity = handlerExecutors is ICollection<NotificationHandlerExecutor> col ? col.Count : 4;
        var tasks = new List<Task>(capacity);
        foreach (var executor in handlerExecutors)
            tasks.Add(executor.HandlerCallback(notification, cancellationToken).AsTask());

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }
}

/// <summary>
/// Notification publisher that fires all handlers in the background without waiting.
/// Exceptions are suppressed.
/// </summary>
public class FireAndForgetPublisher : INotificationPublisher
{
    /// <inheritdoc/>
    public ValueTask Publish(IEnumerable<NotificationHandlerExecutor> handlerExecutors, INotification notification, CancellationToken cancellationToken)
    {
        foreach (var executor in handlerExecutors)
        {
            try
            {
                _ = SafeFireAndForget(executor.HandlerCallback(notification, cancellationToken));
            }
            catch (Exception)
            {
                // Synchronous throw suppressed in fire-and-forget mode
            }
        }

        return default;
    }

    private static async Task SafeFireAndForget(ValueTask task)
    {
        try { await task.ConfigureAwait(false); }
        catch (Exception) { /* Suppressed */ }
    }
}
