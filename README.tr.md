# SwiftMediator

**Yüksek performanslı, kaynak üretimli (source-generated) .NET mediator deseni** -- MediatR'ın yerine geçebilen, derleme zamanında dispatch, sıfır tahsis (zero-allocation) hızlı yollar ve tam AOT/Trim uyumluluğu sunan kütüphane.

[![CI](https://github.com/armanbul/SwiftMediator/actions/workflows/ci.yml/badge.svg)](https://github.com/armanbul/SwiftMediator/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/SwiftMediator)](https://www.nuget.org/packages/SwiftMediator)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://opensource.org/licenses/MIT)

## Neden SwiftMediator?

| | MediatR | SwiftMediator |
|:---|:---:|:---:|
| Dispatch | Çalışma zamanı yansıma + `Dictionary<Type>` | **Derleme zamanı `switch`** (kaynak üretimli) |
| Dönüş tipi | `Task<T>` | **`ValueTask<T>`** (senkron yolda sıfır tahsis) |
| Behavior yoksa | Pipeline tahsis eder | **Hızlı yol** -- doğrudan çağrı, sıfır ek yük |
| AOT / Trim | Desteklenmiyor | **Tam uyumlu** (net8.0+) |
| Hata tespiti | Çalışma zamanı istisnaları | **Derleme zamanı** tanılar |
| Çerçeveler | net8.0, netstandard2.0 | **net10.0, net8.0, netstandard2.0, net462** |
| Lisans | **Ücretli lisans anahtarı gerekli** | **MIT -- sonsuza kadar ücretsiz** |

**Tam MediatR özellik paritesi** -- polimorfik bildirimler dahil 20/20 özellik.

## Kurulum

```bash
dotnet add package SwiftMediator
```

Yalnızca işaretçi arayüzlere ihtiyaç duyan paylaşılan/API projeleri için:

```bash
dotnet add package SwiftMediator.Contracts
```

## Hızlı Başlangıç

### 1. İstek ve Handler Tanımlayın

```csharp
using SwiftMediator.Core;

public record PongResponse(string Reply);

public class PingRequest : IRequest<PongResponse>
{
    public string Message { get; init; } = "";
}

public class PingHandler : IRequestHandler<PingRequest, PongResponse>
{
    public ValueTask<PongResponse> Handle(PingRequest request, CancellationToken ct)
    {
        return new ValueTask<PongResponse>(new PongResponse($"Pong: {request.Message}"));
    }
}
```

### 2. Kayıt Edin ve Kullanın

```csharp
var services = new ServiceCollection();

services.AddSwiftMediator(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();
});

var provider = services.BuildServiceProvider();
var mediator = provider.GetRequiredService<IMediator>();

var response = await mediator.SendAsync<PingRequest, PongResponse>(
    new PingRequest { Message = "Merhaba!" });
// response.Reply == "Pong: Merhaba!"
```

## Örnekler

| Örnek | Açıklama |
|:---|:---|
| [BasicUsage](samples/BasicUsage) | İstek/yanıt, void komutlar, bildirimler, akış, dinamik dispatch |
| [CqrsWithValidation](samples/CqrsWithValidation) | Komutlar, sorgular, doğrulama pipeline'ı ve alan olayları ile CQRS deseni |
| [AdvancedPipeline](samples/AdvancedPipeline) | Tam pipeline davranışları, istisna yönetimi, polimorfik bildirimler, akış pipeline'ı, özel yayıncı |

## Özellikler

### Void (Unit) İstekler

```csharp
public class DeleteUserCommand : IRequest
{
    public int UserId { get; init; }
}

public class DeleteUserHandler : IRequestHandler<DeleteUserCommand, Unit>
{
    public ValueTask<Unit> Handle(DeleteUserCommand request, CancellationToken ct)
    {
        // silme işlemini gerçekleştir
        return new ValueTask<Unit>(Unit.Value);
    }
}

// Kolaylık -- Unit belirtmeye gerek yok:
await mediator.SendAsync(new DeleteUserCommand { UserId = 42 });
```

### Bildirimler (Pub/Sub)

```csharp
public class OrderCreatedEvent : INotification
{
    public int OrderId { get; init; }
}

public class EmailHandler : INotificationHandler<OrderCreatedEvent>
{
    public ValueTask Handle(OrderCreatedEvent notification, CancellationToken ct)
    {
        // e-posta gönder
        return default;
    }
}

// Yayınlama stratejileri:
await mediator.PublishAsync(evt, PublishStrategy.Sequential);   // sırayla (varsayılan)
await mediator.PublishAsync(evt, PublishStrategy.Parallel);     // Task.WhenAll
await mediator.PublishAsync(evt, PublishStrategy.FireAndForget); // ateşle ve unut
```

### Polimorfik Bildirimler

Taban tip/arayüz için kaydedilen handler'lar **tüm** türetilmiş tipler için çağrılır:

```csharp
public interface IOrderEvent : INotification { }
public class OrderConfirmedEvent : IOrderEvent { }
public class OrderCancelledEvent : IOrderEvent { }

// TÜM IOrderEvent tipleri için çalışır:
public class AuditHandler : INotificationHandler<IOrderEvent> { ... }

// Yalnızca OrderConfirmedEvent için çalışır:
public class ConfirmedHandler : INotificationHandler<OrderConfirmedEvent> { ... }
```

### Akış (IAsyncEnumerable)

```csharp
public class SearchQuery : IStreamRequest<SearchResult>
{
    public string Term { get; init; } = "";
}

public class SearchHandler : IStreamRequestHandler<SearchQuery, SearchResult>
{
    public async IAsyncEnumerable<SearchResult> Handle(
        SearchQuery request, [EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var item in db.SearchAsync(request.Term))
            yield return item;
    }
}

await foreach (var result in mediator.CreateStream<SearchQuery, SearchResult>(query))
    Console.WriteLine(result);
```

### Pipeline Davranışları (Middleware)

```csharp
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async ValueTask<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        Console.WriteLine($"{typeof(TRequest).Name} işleniyor");
        var response = await next();
        Console.WriteLine($"{typeof(TRequest).Name} işlendi");
        return response;
    }
}
```

> **Hızlı yol:** Hiçbir behavior kayıtlı değilse handler doğrudan çağrılır -- sıfır ek yük.

### Ön/Son İşlemciler (Pre/Post Processors)

```csharp
public class ValidationPreProcessor<TRequest> : IRequestPreProcessor<TRequest>
    where TRequest : notnull
{
    public ValueTask Process(TRequest request, CancellationToken ct) { /* doğrula */ return default; }
}

public class AuditPostProcessor<TReq, TRes> : IRequestPostProcessor<TReq, TRes>
    where TReq : notnull
{
    public ValueTask Process(TReq request, TRes response, CancellationToken ct) { /* denetle */ return default; }
}
```

**Çalışma sırası:** `ÖnİşlemciI -> Davranış(lar) -> Handler -> Sonİşlemci`

### İstisna Pipeline'ı

```csharp
// Action'lar -- yalnızca gözlem (loglama, metrik), bastıramazlar
public class MetricsAction<TReq> : IRequestExceptionAction<TReq, Exception>
    where TReq : notnull
{
    public ValueTask Execute(TReq request, Exception ex, CancellationToken ct)
    {
        // metrik kaydet
        return default;
    }
}

// Handler'lar -- istisnayı bastırıp yedek yanıt döndürebilir
public class FallbackHandler : IRequestExceptionHandler<MyRequest, MyResponse, InvalidOperationException>
{
    public ValueTask Handle(MyRequest req, InvalidOperationException ex,
        RequestExceptionHandlerState<MyResponse> state, CancellationToken ct)
    {
        state.SetHandled(new MyResponse { /* yedek */ });
        return default;
    }
}
```

**Sıra:** Action'lar önce çalışır (her zaman), sonra Handler'lar (`SetHandled()` ile bastırılabilir).

### Akış Pipeline Davranışları

```csharp
public class StreamLogging<TReq, TRes> : IStreamPipelineBehavior<TReq, TRes>
    where TReq : IStreamRequest<TRes>
{
    public async IAsyncEnumerable<TRes> Handle(
        TReq request, StreamHandlerDelegate<TRes> next,
        [EnumeratorCancellation] CancellationToken ct)
    {
        Console.WriteLine("Akış başlıyor");
        await foreach (var item in next())
            yield return item;
        Console.WriteLine("Akış tamamlandı");
    }
}
```

### Özel Bildirim Yayıncısı

```csharp
public class BatchPublisher : INotificationPublisher
{
    public async ValueTask Publish(
        IEnumerable<NotificationHandlerExecutor> executors,
        INotification notification, CancellationToken ct)
    {
        foreach (var executor in executors)
            await executor.HandlerCallback(notification, ct);
    }
}
```

Yerleşik: `ForeachAwaitPublisher`, `TaskWhenAllPublisher`, `FireAndForgetPublisher`.

### ISender / IPublisher Ayrımı

```csharp
// En dar arayüze bağımlı olun:
public class MyService(ISender sender) { }    // yalnızca istek gönderebilir
public class MyPub(IPublisher publisher) { }  // yalnızca bildirim yayınlayabilir
```

Her üç arayüz (`IMediator`, `ISender`, `IPublisher`) aynı örnek üzerine çözümlenir.

## DI Yapılandırması

### Akıcı API (Fluent API)

```csharp
services.AddSwiftMediator(cfg =>
{
    // Yaşam süresi
    cfg.Lifetime = HandlerLifetime.Scoped;           // handler'lar (varsayılan: Transient)
    cfg.MediatorLifetime = HandlerLifetime.Singleton; // mediator (varsayılan: Scoped)

    // Assembly tarama -- davranışları, işlemcileri, istisna handler'ları otomatik keşfeder
    cfg.RegisterServicesFromAssemblyContaining<Program>();

    // Veya açıkça kayıt edin:
    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>))
       .AddOpenBehavior(typeof(ValidationBehavior<,>))
       .AddRequestPreProcessor<AuditPreProcessor>()
       .AddRequestPostProcessor<CachePostProcessor>()
       .AddExceptionHandler<FallbackExceptionHandler>()
       .AddExceptionAction<MetricsAction>()
       .AddStreamBehavior<StreamLoggingBehavior>()
       .SetNotificationPublisher<TaskWhenAllPublisher>();
});
```

### Assembly Tarama ile Keşfedilenler

- `IPipelineBehavior<,>` / `IStreamPipelineBehavior<,>`
- `IRequestPreProcessor<>` / `IRequestPostProcessor<,>`
- `IRequestExceptionHandler<,,>` / `IRequestExceptionAction<,>`

> **Handler'lar** (`IRequestHandler`, `INotificationHandler`, `IStreamRequestHandler`) kaynak üretici tarafından **derleme zamanında** keşfedilir -- assembly tarama ile değil.

## Derleme Zamanı Tanıları

| Kod | Ciddiyet | Açıklama |
|:---|:---|:---|
| `SWIFT001` | Hata | Yinelenmiş istek handler'ı |
| `SWIFT002` | Hata | Yinelenmiş akış handler'ı |
| `SWIFT003` | Uyarı | Handler bulunamadı |
| `SWIFT004` | Bilgi | Açık jenerik handler kaydedildi |

## Desteklenen Çerçeveler

| Paket | Hedefler |
|:---|:---|
| `SwiftMediator` | `net10.0`, `net8.0`, `netstandard2.0`, `net462` |
| `SwiftMediator.Contracts` | `netstandard2.0` |

`net8.0+` üzerinde AOT ve trim uyumlu.

## Özellik Listesi

- [x] İstek/Yanıt (`IRequest<T>` -> `IRequestHandler<T, R>`)
- [x] Void istekler (`IRequest` -> `Unit`)
- [x] Bildirimler (`INotification` -> `INotificationHandler<T>`)
- [x] Polimorfik bildirimler (taban tip handler'ları türetilmiş tipler için çağrılır)
- [x] Akış (`IStreamRequest<T>` -> `IAsyncEnumerable<T>`)
- [x] Pipeline davranışları (`IPipelineBehavior<T, R>`)
- [x] Akış pipeline davranışları (`IStreamPipelineBehavior<T, R>`)
- [x] Ön/Son işlemciler
- [x] İstisna handler'ları (bastırma + yedek yanıt)
- [x] İstisna action'ları (yalnızca gözlem)
- [x] Özel bildirim yayıncısı (`INotificationPublisher`)
- [x] ISender / IPublisher arayüz ayrımı
- [x] Akıcı DI yapılandırması
- [x] Assembly tarama
- [x] Açık jenerik handler desteği
- [x] Polimorfik istek dispatch
- [x] Dinamik dispatch (`SendAsync(object)`)
- [x] Handler yaşam süresi yapılandırması (Transient / Scoped / Singleton)
- [x] Derleme zamanı tanıları
- [x] Çoklu çerçeve desteği (net10.0, net8.0, netstandard2.0, net462)

## MediatR'dan Göç

SwiftMediator, MediatR ile **aynı API desenlerini** kullanır -- göç kolay:

| MediatR | SwiftMediator |
|:---|:---|
| `services.AddMediatR(cfg => ...)` | `services.AddSwiftMediator(cfg => ...)` |
| `cfg.RegisterServicesFromAssemblyContaining<T>()` | Aynı |
| `cfg.AddOpenBehavior(typeof(T<,>))` | Aynı |
| `cfg.AddRequestPreProcessor<T>()` | Aynı |
| `cfg.AddRequestPostProcessor<T>()` | Aynı |
| `cfg.AddStreamBehavior<T>()` | Aynı |
| `IRequestHandler<TReq, TRes>` dönüş tipi `Task<T>` | Dönüş tipi `ValueTask<T>` |
| `cfg.LicenseKey = "..."` | **Gerekli değil -- MIT lisanslı** |

> **Not:** MediatR artık [ücretli lisans anahtarı](https://mediatr.io) gerektirmektedir. SwiftMediator MIT lisansı altında **ücretsiz ve açık kaynak** olmaya devam edecektir.

## Destek

SwiftMediator'u faydalı buluyorsanız bir kahve ısmarlmayı düşünebilirsiniz:

[![Buy Me a Coffee](https://cdn.buymeacoffee.com/buttons/v2/default-yellow.png)](https://www.buymeacoffee.com/armanbulk)

## Lisans

MIT
