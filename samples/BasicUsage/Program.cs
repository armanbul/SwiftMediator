using Microsoft.Extensions.DependencyInjection;
using SwiftMediator;
using SwiftMediator.Core;

namespace BasicUsage;

// ---------------------------------------------------------------------------
//  Request / Response
// ---------------------------------------------------------------------------

public record WeatherForecast(string City, int TemperatureC, string Summary);

public class GetWeatherRequest : IRequest<WeatherForecast>
{
    public string City { get; init; } = "";
}

public class GetWeatherHandler : IRequestHandler<GetWeatherRequest, WeatherForecast>
{
    public ValueTask<WeatherForecast> Handle(GetWeatherRequest request, CancellationToken ct)
    {
        var forecast = new WeatherForecast(request.City, 22, "Sunny");
        return new ValueTask<WeatherForecast>(forecast);
    }
}

// ---------------------------------------------------------------------------
//  Void Request (Unit)
// ---------------------------------------------------------------------------

public class LogMessageCommand : IRequest
{
    public string Text { get; init; } = "";
}

public class LogMessageHandler : IRequestHandler<LogMessageCommand, Unit>
{
    public ValueTask<Unit> Handle(LogMessageCommand request, CancellationToken ct)
    {
        Console.WriteLine($"    Logged: {request.Text}");
        return new ValueTask<Unit>(Unit.Value);
    }
}

// ---------------------------------------------------------------------------
//  Notifications
// ---------------------------------------------------------------------------

public class UserRegisteredEvent : INotification
{
    public string Email { get; init; } = "";
}

public class SendWelcomeEmailHandler : INotificationHandler<UserRegisteredEvent>
{
    public ValueTask Handle(UserRegisteredEvent notification, CancellationToken ct)
    {
        Console.WriteLine($"    Welcome email sent to {notification.Email}");
        return default;
    }
}

public class CreateDefaultSettingsHandler : INotificationHandler<UserRegisteredEvent>
{
    public ValueTask Handle(UserRegisteredEvent notification, CancellationToken ct)
    {
        Console.WriteLine($"    Default settings created for {notification.Email}");
        return default;
    }
}

public class NotifyAdminHandler : INotificationHandler<UserRegisteredEvent>
{
    public ValueTask Handle(UserRegisteredEvent notification, CancellationToken ct)
    {
        Console.WriteLine($"    Admin notified about new user: {notification.Email}");
        return default;
    }
}

// ---------------------------------------------------------------------------
//  Streaming
// ---------------------------------------------------------------------------

public class PrimeNumbersQuery : IStreamRequest<int>
{
    public int Count { get; init; }
}

public class PrimeNumbersHandler : IStreamRequestHandler<PrimeNumbersQuery, int>
{
    public async IAsyncEnumerable<int> Handle(
        PrimeNumbersQuery request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        int count = 0, candidate = 2;
        while (count < request.Count)
        {
            if (IsPrime(candidate))
            {
                yield return candidate;
                count++;
                await Task.Yield();
            }
            candidate++;
        }
    }

    private static bool IsPrime(int n)
    {
        if (n < 2) return false;
        for (int i = 2; i * i <= n; i++)
            if (n % i == 0) return false;
        return true;
    }
}

// ---------------------------------------------------------------------------
//  Program
// ---------------------------------------------------------------------------

class Program
{
    static async Task Main()
    {
        var services = new ServiceCollection();
        services.AddSwiftMediator(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<Program>();
        });

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        // 1. Request / Response
        Console.WriteLine("[1] Request / Response");
        var forecast = await mediator.SendAsync<GetWeatherRequest, WeatherForecast>(
            new GetWeatherRequest { City = "Istanbul" });
        Console.WriteLine($"    {forecast.City}: {forecast.TemperatureC}C, {forecast.Summary}");
        Console.WriteLine();

        // 2. Void Request
        Console.WriteLine("[2] Void Request (Unit)");
        await mediator.SendAsync(new LogMessageCommand { Text = "Application started" });
        Console.WriteLine();

        // 3. Notifications
        Console.WriteLine("[3] Notifications (3 handlers)");
        await mediator.PublishAsync(new UserRegisteredEvent { Email = "jane@example.com" });
        Console.WriteLine();

        // 4. Notification Strategies
        Console.WriteLine("[4] Parallel Notification");
        await mediator.PublishAsync(
            new UserRegisteredEvent { Email = "john@example.com" },
            PublishStrategy.Parallel);
        Console.WriteLine();

        // 5. Streaming
        Console.WriteLine("[5] Streaming (first 10 primes)");
        Console.Write("    ");
        await foreach (var prime in mediator.CreateStream<PrimeNumbersQuery, int>(
            new PrimeNumbersQuery { Count = 10 }))
        {
            Console.Write($"{prime} ");
        }
        Console.WriteLine();
        Console.WriteLine();

        // 6. Dynamic Dispatch
        Console.WriteLine("[6] Dynamic Dispatch (object -> handler)");
        object request = new GetWeatherRequest { City = "Ankara" };
        var result = await mediator.SendAsync(request);
        var weather = (WeatherForecast)result!;
        Console.WriteLine($"    {weather.City}: {weather.TemperatureC}C, {weather.Summary}");
        Console.WriteLine();

        // 7. ISender / IPublisher Segregation
        Console.WriteLine("[7] ISender / IPublisher Segregation");
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();
        Console.WriteLine($"    ISender type:    {sender.GetType().Name}");
        Console.WriteLine($"    IPublisher type: {publisher.GetType().Name}");
        Console.WriteLine($"    Same instance:   {ReferenceEquals(mediator, sender)}");
    }
}
