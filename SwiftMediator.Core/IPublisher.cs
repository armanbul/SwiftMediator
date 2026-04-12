namespace SwiftMediator.Core;

/// <summary>
/// Publish a notification event through the mediator to be handled by multiple handlers.
/// </summary>
public interface IPublisher
{
    /// <summary>
    /// Asynchronously publish a notification to all registered handlers.
    /// </summary>
    ValueTask PublishAsync<TNotification>(TNotification notification, PublishStrategy strategy = PublishStrategy.Sequential, CancellationToken cancellationToken = default)
        where TNotification : INotification;
}
