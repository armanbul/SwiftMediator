namespace SwiftMediator.Core;

/// <summary>
/// Send a request through the mediator pipeline to be handled by a single handler.
/// </summary>
public interface ISender
{
    /// <summary>
    /// Asynchronously send a request to a single handler using JIT optimized generics.
    /// </summary>
    ValueTask<TResponse> SendAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : IRequest<TResponse>;

    /// <summary>
    /// Asynchronously send a request to a single handler using dynamic dispatch (object-based).
    /// </summary>
    ValueTask<object?> SendAsync(object request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates an async enumerable stream from a streaming request handler.
    /// </summary>
    IAsyncEnumerable<TResponse> CreateStream<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : IStreamRequest<TResponse>;
}
