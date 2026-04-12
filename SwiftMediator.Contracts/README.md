# SwiftMediator.Contracts

Lightweight contracts-only package for [SwiftMediator](https://www.nuget.org/packages/SwiftMediator) — the source-generated, high-performance mediator pattern library for .NET.

## Purpose

Use this package in **shared libraries**, **API contracts**, or **domain projects** that need to define requests, notifications, and streaming requests **without** taking a dependency on the full mediator implementation.

## What's Included

| Type | Description |
|:---|:---|
| `IRequest<TResponse>` | Marker interface for a request that returns `TResponse` |
| `IRequest` | Shorthand for `IRequest<Unit>` (void requests) |
| `INotification` | Marker interface for pub/sub notifications |
| `IStreamRequest<TResponse>` | Marker interface for streaming requests (`IAsyncEnumerable<T>`) |
| `Unit` | Value type representing void — `readonly struct`, `IEquatable<Unit>` |

## Installation

```bash
dotnet add package SwiftMediator.Contracts
```

## Usage

```csharp
using SwiftMediator.Core;

// In your shared/API project — no dependency on DI or the mediator itself
public record UserDto(int Id, string Name);

public class GetUserQuery : IRequest<UserDto>
{
    public int UserId { get; init; }
}

public class UserCreatedEvent : INotification
{
    public int UserId { get; init; }
}

public class SearchQuery : IStreamRequest<UserDto>
{
    public string Term { get; init; } = "";
}
```

The handler implementations and the mediator itself live in the host project that references the full `SwiftMediator` package.

## Target Framework

`netstandard2.0` — compatible with .NET Framework 4.6.1+, .NET Core 2.0+, .NET 5+, and .NET 8/10.

## Related

- **[SwiftMediator](https://www.nuget.org/packages/SwiftMediator)** — Full mediator with source generator, pipeline behaviors, DI configuration, and assembly scanning.

## License

MIT
