namespace SwiftMediator.Core;

/// <summary>
/// Configuration options for <c>AddSwiftMediator</c>.
/// Allows customizing handler registration lifetimes.
/// </summary>
[Obsolete("Use MediatorServiceConfiguration instead. This class will be removed in a future version.")]
public sealed class SwiftMediatorOptions
{
    /// <summary>
    /// The <see cref="HandlerLifetime"/> for all request, notification, and stream handlers.
    /// Defaults to <see cref="HandlerLifetime.Transient"/>.
    /// </summary>
    public HandlerLifetime Lifetime { get; set; } = HandlerLifetime.Transient;

    /// <summary>
    /// The <see cref="HandlerLifetime"/> for the <see cref="IMediator"/> registration itself.
    /// Defaults to <see cref="HandlerLifetime.Scoped"/>.
    /// </summary>
    public HandlerLifetime MediatorLifetime { get; set; } = HandlerLifetime.Scoped;
}

/// <summary>
/// Specifies the DI registration lifetime for handlers and the mediator.
/// </summary>
public enum HandlerLifetime
{
    /// <summary>A new instance is created each time it is requested.</summary>
    Transient,

    /// <summary>A single instance is created per scope (e.g. per HTTP request).</summary>
    Scoped,

    /// <summary>A single instance is shared across the entire application.</summary>
    Singleton
}
