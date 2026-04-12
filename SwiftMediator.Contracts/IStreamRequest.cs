#nullable enable

namespace SwiftMediator.Core
{
    /// <summary>
    /// Marker interface for a streaming request.
    /// </summary>
    /// <typeparam name="TResponse">The type of each streamed item</typeparam>
    public interface IStreamRequest<out TResponse>
    {
    }
}
