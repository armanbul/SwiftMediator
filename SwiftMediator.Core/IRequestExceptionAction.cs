namespace SwiftMediator.Core;

/// <summary>
/// Defines an action to execute when an exception is thrown during request processing.
/// Unlike <see cref="IRequestExceptionHandler{TRequest,TResponse,TException}"/>, actions
/// cannot handle (suppress) the exception — they only observe it (e.g., for logging or metrics).
/// All registered actions execute regardless of whether an exception handler subsequently handles the exception.
/// </summary>
/// <typeparam name="TRequest">The type of request that caused the exception</typeparam>
/// <typeparam name="TException">The type of exception to observe</typeparam>
public interface IRequestExceptionAction<in TRequest, in TException>
    where TRequest : notnull
    where TException : Exception
{
    /// <summary>
    /// Called when <typeparamref name="TException"/> is thrown while handling <typeparamref name="TRequest"/>.
    /// </summary>
    /// <param name="request">The original request</param>
    /// <param name="exception">The caught exception</param>
    /// <param name="cancellationToken">Cancellation token</param>
    ValueTask Execute(TRequest request, TException exception, CancellationToken cancellationToken);
}
