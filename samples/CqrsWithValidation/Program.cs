using Microsoft.Extensions.DependencyInjection;
using SwiftMediator;
using SwiftMediator.Core;

namespace CqrsWithValidation;

// ---------------------------------------------------------------------------
//  Marker interfaces for CQRS
// ---------------------------------------------------------------------------

public interface ICommand : IRequest<CommandResponse> { }
public interface ICommand<TResponse> : IRequest<TResponse> { }
public interface IQuery<TResponse> : IRequest<TResponse> { }

public record CommandResponse(bool Success, string Message);

// ---------------------------------------------------------------------------
//  Domain model
// ---------------------------------------------------------------------------

public record Product(int Id, string Name, decimal Price);

public static class ProductStore
{
    private static readonly List<Product> _products = new()
    {
        new Product(1, "Keyboard", 79.99m),
        new Product(2, "Mouse", 49.99m),
        new Product(3, "Monitor", 399.99m)
    };

    public static IReadOnlyList<Product> All => _products;

    public static void Add(Product product) => _products.Add(product);
}

// ---------------------------------------------------------------------------
//  Queries
// ---------------------------------------------------------------------------

public class GetAllProductsQuery : IQuery<IReadOnlyList<Product>> { }

public record ProductResult(Product? Product);

public class GetProductByIdQuery : IQuery<ProductResult>
{
    public int Id { get; init; }
}

public class GetAllProductsHandler : IRequestHandler<GetAllProductsQuery, IReadOnlyList<Product>>
{
    public ValueTask<IReadOnlyList<Product>> Handle(GetAllProductsQuery request, CancellationToken ct)
    {
        return new ValueTask<IReadOnlyList<Product>>(ProductStore.All);
    }
}

public class GetProductByIdHandler : IRequestHandler<GetProductByIdQuery, ProductResult>
{
    public ValueTask<ProductResult> Handle(GetProductByIdQuery request, CancellationToken ct)
    {
        var product = ProductStore.All.FirstOrDefault(p => p.Id == request.Id);
        return new ValueTask<ProductResult>(new ProductResult(product));
    }
}

// ---------------------------------------------------------------------------
//  Commands
// ---------------------------------------------------------------------------

public class CreateProductCommand : ICommand
{
    public string Name { get; init; } = "";
    public decimal Price { get; init; }
}

public class CreateProductHandler : IRequestHandler<CreateProductCommand, CommandResponse>
{
    public ValueTask<CommandResponse> Handle(CreateProductCommand request, CancellationToken ct)
    {
        var id = ProductStore.All.Count + 1;
        ProductStore.Add(new Product(id, request.Name, request.Price));
        Console.WriteLine($"    Product created: #{id} {request.Name} (${request.Price})");
        return new ValueTask<CommandResponse>(new CommandResponse(true, $"Product #{id} created"));
    }
}

// ---------------------------------------------------------------------------
//  Notification: domain event after command
// ---------------------------------------------------------------------------

public class ProductCreatedEvent : INotification
{
    public string ProductName { get; init; } = "";
}

public class IndexProductHandler : INotificationHandler<ProductCreatedEvent>
{
    public ValueTask Handle(ProductCreatedEvent notification, CancellationToken ct)
    {
        Console.WriteLine($"    Search index updated for: {notification.ProductName}");
        return default;
    }
}

public class CacheInvalidationHandler : INotificationHandler<ProductCreatedEvent>
{
    public ValueTask Handle(ProductCreatedEvent notification, CancellationToken ct)
    {
        Console.WriteLine($"    Product cache invalidated after: {notification.ProductName}");
        return default;
    }
}

// ---------------------------------------------------------------------------
//  Validation behavior (open generic pipeline)
// ---------------------------------------------------------------------------

public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public ValueTask<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        // Validate CreateProductCommand
        if (request is CreateProductCommand cmd)
        {
            if (string.IsNullOrWhiteSpace(cmd.Name))
                throw new ArgumentException("Product name is required.");
            if (cmd.Price <= 0)
                throw new ArgumentException("Product price must be positive.");

            Console.WriteLine($"    [Validation] Passed: Name={cmd.Name}, Price=${cmd.Price}");
        }

        return next();
    }
}

// ---------------------------------------------------------------------------
//  Logging behavior
// ---------------------------------------------------------------------------

public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async ValueTask<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var name = typeof(TRequest).Name;
        var kind = request is ICommand or ICommand<TResponse> ? "Command" : "Query";
        Console.WriteLine($"    [Pipeline] {kind}: {name}");

        var response = await next();

        Console.WriteLine($"    [Pipeline] Completed: {name}");
        return response;
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

        // Query: Get all products
        Console.WriteLine("[Query] Get all products");
        var products = await mediator.SendAsync<GetAllProductsQuery, IReadOnlyList<Product>>(
            new GetAllProductsQuery());
        foreach (var p in products)
            Console.WriteLine($"    #{p.Id} {p.Name} - ${p.Price}");
        Console.WriteLine();

        // Query: Get by ID
        Console.WriteLine("[Query] Get product by ID");
        var found = await mediator.SendAsync<GetProductByIdQuery, ProductResult>(
            new GetProductByIdQuery { Id = 2 });
        Console.WriteLine($"    Found: {found.Product?.Name} - ${found.Product?.Price}");
        Console.WriteLine();

        // Command: Create product (passes validation)
        Console.WriteLine("[Command] Create product");
        var result = await mediator.SendAsync<CreateProductCommand, CommandResponse>(
            new CreateProductCommand { Name = "Webcam", Price = 129.99m });
        Console.WriteLine($"    Result: {result.Message}");
        Console.WriteLine();

        // Publish domain event
        Console.WriteLine("[Event] Product created notification");
        await mediator.PublishAsync(new ProductCreatedEvent { ProductName = "Webcam" });
        Console.WriteLine();

        // Command: Validation failure
        Console.WriteLine("[Command] Create product with invalid data");
        try
        {
            await mediator.SendAsync<CreateProductCommand, CommandResponse>(
                new CreateProductCommand { Name = "", Price = -10m });
        }
        catch (ArgumentException ex)
        {
            Console.WriteLine($"    Validation failed: {ex.Message}");
        }
        Console.WriteLine();

        // Verify final state
        Console.WriteLine("[Query] Final product list");
        var all = await mediator.SendAsync<GetAllProductsQuery, IReadOnlyList<Product>>(
            new GetAllProductsQuery());
        foreach (var p in all)
            Console.WriteLine($"    #{p.Id} {p.Name} - ${p.Price}");
    }
}
