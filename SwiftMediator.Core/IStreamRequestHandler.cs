namespace SwiftMediator.Core;

/// <summary>
/// Defines a handler for a streaming request that yields items via <see cref="IAsyncEnumerable{TResponse}"/>.
/// </summary>
/// <typeparam name="TRequest">The type of streaming request being handled</typeparam>
/// <typeparam name="TResponse">The type of each streamed response item</typeparam>
public interface IStreamRequestHandler<in TRequest, out TResponse>
    where TRequest : IStreamRequest<TResponse>
{
    /// <summary>
    /// Handles a streaming request
    /// </summary>
    /// <param name="request">The streaming request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>An async enumerable of response items</returns>
    IAsyncEnumerable<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}
