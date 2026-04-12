namespace SwiftMediator.Core;

/// <summary>
/// Executed before the request handler (but after pipeline behaviors).
/// Useful for logging, validation setup, or enriching the request context.
/// Multiple pre-processors can be registered; they run in registration order.
/// </summary>
/// <typeparam name="TRequest">The type of request being processed</typeparam>
public interface IRequestPreProcessor<in TRequest> where TRequest : notnull
{
    /// <summary>
    /// Process method executed before the request handler.
    /// </summary>
    /// <param name="request">The incoming request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    ValueTask Process(TRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Executed after the request handler completes successfully (but before pipeline behaviors return).
/// Useful for audit logging, cache population, or response enrichment.
/// Multiple post-processors can be registered; they run in registration order.
/// </summary>
/// <typeparam name="TRequest">The type of request being processed</typeparam>
/// <typeparam name="TResponse">The response type</typeparam>
public interface IRequestPostProcessor<in TRequest, in TResponse> where TRequest : notnull
{
    /// <summary>
    /// Process method executed after the request handler.
    /// </summary>
    /// <param name="request">The original request</param>
    /// <param name="response">The response returned by the handler</param>
    /// <param name="cancellationToken">Cancellation token</param>
    ValueTask Process(TRequest request, TResponse response, CancellationToken cancellationToken);
}
