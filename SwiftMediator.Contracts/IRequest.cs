#nullable enable

namespace SwiftMediator.Core
{
    /// <summary>
    /// Defines a marker interface to represent a request with a void response.
    /// </summary>
    public interface IRequest : IRequest<Unit>
    {
    }

    /// <summary>
    /// Defines a marker interface to represent a request with a response.
    /// </summary>
    /// <typeparam name="TResponse">Response type</typeparam>
    public interface IRequest<out TResponse>
    {
    }
}
