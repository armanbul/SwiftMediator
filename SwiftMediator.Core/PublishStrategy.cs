namespace SwiftMediator.Core;

/// <summary>
/// Defines the strategy for executing multiple notification handlers.
/// </summary>
public enum PublishStrategy
{
    /// <summary>
    /// Executes all handlers sequentially. Default behavior.
    /// </summary>
    Sequential,
    
    /// <summary>
    /// Executes all handlers in parallel using Task.WhenAll.
    /// </summary>
    Parallel,
    
    /// <summary>
    /// Starts all handlers in parallel but does not wait for their completion.
    /// </summary>
    FireAndForget
}
