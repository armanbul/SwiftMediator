namespace SwiftMediator.Core;

/// <summary>
/// Convenience extension methods for <see cref="IMediator"/>.
/// </summary>
public static class MediatorExtensions
{
    /// <summary>
    /// Send a void (Unit) request without needing to specify the Unit response type.
    /// </summary>
    public static ValueTask<Unit> SendAsync<TRequest>(this ISender sender, TRequest request, CancellationToken cancellationToken = default)
        where TRequest : IRequest<Unit>
    {
        return sender.SendAsync<TRequest, Unit>(request, cancellationToken);
    }
}
