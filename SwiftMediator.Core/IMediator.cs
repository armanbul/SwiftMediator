namespace SwiftMediator.Core;

/// <summary>
/// Defines a mediator to encapsulate request/response and publishing interaction patterns.
/// Combines <see cref="ISender"/> and <see cref="IPublisher"/> into a single interface.
/// </summary>
public interface IMediator : ISender, IPublisher
{
}
