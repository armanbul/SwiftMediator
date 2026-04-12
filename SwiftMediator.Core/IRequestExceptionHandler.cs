namespace SwiftMediator.Core;

/// <summary>
/// Defines a handler for exceptions thrown during request processing.
/// Multiple exception handlers can be registered for the same request type;
/// they execute in registration order. Set <see cref="RequestExceptionHandlerState.Handled"/>
/// to stop propagation.
/// </summary>
/// <typeparam name="TRequest">The type of request that caused the exception</typeparam>
/// <typeparam name="TResponse">The response type of the request</typeparam>
/// <typeparam name="TException">The type of exception to handle</typeparam>
public interface IRequestExceptionHandler<in TRequest, TResponse, in TException>
    where TRequest : notnull
    where TException : Exception
{
    /// <summary>
    /// Called when <typeparamref name="TException"/> is thrown while handling <typeparamref name="TRequest"/>.
    /// </summary>
    /// <param name="request">The original request</param>
    /// <param name="exception">The caught exception</param>
    /// <param name="state">State object — set <see cref="RequestExceptionHandlerState.Handled"/> 
    /// and <see cref="RequestExceptionHandlerState.Response"/> to suppress the exception.</param>
    /// <param name="cancellationToken">Cancellation token</param>
    ValueTask Handle(TRequest request, TException exception, RequestExceptionHandlerState<TResponse> state, CancellationToken cancellationToken);
}

/// <summary>
/// Mutable state passed to <see cref="IRequestExceptionHandler{TRequest,TResponse,TException}"/>.
/// Setting <see cref="Handled"/> to true prevents the exception from being re-thrown.
/// </summary>
/// <typeparam name="TResponse">The response type</typeparam>
public class RequestExceptionHandlerState<TResponse>
{
    /// <summary>
    /// When set to true, the exception is considered handled and will not be re-thrown.
    /// The <see cref="Response"/> value will be returned to the caller instead.
    /// </summary>
    public bool Handled { get; set; }

    /// <summary>
    /// The fallback response to return when <see cref="Handled"/> is true.
    /// </summary>
    public TResponse? Response { get; set; }

    /// <summary>
    /// Marks the exception as handled and provides a fallback response.
    /// </summary>
    public void SetHandled(TResponse response)
    {
        Handled = true;
        Response = response;
    }
}
