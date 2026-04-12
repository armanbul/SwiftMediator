namespace SwiftMediator.Core;

/// <summary>
/// Represents an async continuation for the next task to execute in the stream pipeline.
/// </summary>
/// <typeparam name="TResponse">Streamed item type</typeparam>
/// <returns>An async enumerable of response items</returns>
public delegate IAsyncEnumerable<TResponse> StreamHandlerDelegate<out TResponse>();

/// <summary>
/// Pipeline behavior to surround the inner stream handler.
/// Works like <see cref="IPipelineBehavior{TRequest, TResponse}"/> but for streaming requests.
/// </summary>
/// <typeparam name="TRequest">Stream request type</typeparam>
/// <typeparam name="TResponse">Streamed item type</typeparam>
public interface IStreamPipelineBehavior<in TRequest, TResponse>
    where TRequest : IStreamRequest<TResponse>
{
    /// <summary>
    /// Stream pipeline handler. Perform any additional behavior and return the <paramref name="next"/> delegate's result.
    /// You may wrap, filter, or transform the stream.
    /// </summary>
    /// <param name="request">Incoming stream request</param>
    /// <param name="next">Delegate for the next action in the pipeline (eventually the handler)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>An async enumerable of response items</returns>
    IAsyncEnumerable<TResponse> Handle(TRequest request, StreamHandlerDelegate<TResponse> next, CancellationToken cancellationToken);
}
