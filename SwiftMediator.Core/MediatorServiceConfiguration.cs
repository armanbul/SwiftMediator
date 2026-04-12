using System.Reflection;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace SwiftMediator.Core;

/// <summary>
/// Fluent configuration for SwiftMediator DI registration.
/// Supports assembly scanning, explicit pipeline registrations, and lifetime configuration.
/// </summary>
public sealed class MediatorServiceConfiguration
{
    // ── Lifetime Settings (backward compatible with SwiftMediatorOptions) ──

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

    /// <summary>
    /// Optional: set a custom <see cref="INotificationPublisher"/> type.
    /// When set, all notification publishes will delegate to this publisher.
    /// </summary>
    public Type? NotificationPublisherType { get; set; }

    // ── Internal Storage ──────────────────────────────────────────────

    private readonly List<PipelineRegistration> _pipelineRegistrations = new();
    private readonly List<Assembly> _assembliesToScan = new();

    // ══════════════════════════════════════════════════════════════════
    // Assembly Scanning
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Scan the given assembly for pipeline behaviors, pre/post processors,
    /// exception handlers, and exception actions. Registers them automatically.
    /// </summary>
    public MediatorServiceConfiguration RegisterServicesFromAssembly(Assembly assembly)
    {
        _assembliesToScan.Add(assembly);
        return this;
    }

    /// <summary>
    /// Scan the assembly containing <typeparamref name="T"/> for pipeline services.
    /// </summary>
    public MediatorServiceConfiguration RegisterServicesFromAssemblyContaining<T>()
        => RegisterServicesFromAssembly(typeof(T).Assembly);

    /// <summary>
    /// Scan the assembly containing <paramref name="type"/> for pipeline services.
    /// </summary>
    public MediatorServiceConfiguration RegisterServicesFromAssemblyContaining(Type type)
        => RegisterServicesFromAssembly(type.Assembly);

    // ══════════════════════════════════════════════════════════════════
    // Pipeline Behaviors
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Register an open generic pipeline behavior.
    /// Example: <c>cfg.AddOpenBehavior(typeof(LoggingBehavior&lt;,&gt;))</c>
    /// </summary>
    public MediatorServiceConfiguration AddOpenBehavior(Type openBehaviorType, ServiceLifetime? lifetime = null)
    {
        ValidateOpenGeneric(openBehaviorType, typeof(IPipelineBehavior<,>), nameof(openBehaviorType));
        _pipelineRegistrations.Add(new PipelineRegistration(typeof(IPipelineBehavior<,>), openBehaviorType, lifetime));
        return this;
    }

    /// <summary>
    /// Register a closed pipeline behavior by its concrete type.
    /// </summary>
    public MediatorServiceConfiguration AddBehavior<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] TBehavior>(ServiceLifetime? lifetime = null) where TBehavior : class
    {
        AddClosedService(typeof(TBehavior), typeof(IPipelineBehavior<,>), lifetime);
        return this;
    }

    // ══════════════════════════════════════════════════════════════════
    // Stream Pipeline Behaviors
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Register an open generic stream pipeline behavior.
    /// Example: <c>cfg.AddOpenStreamBehavior(typeof(StreamLoggingBehavior&lt;,&gt;))</c>
    /// </summary>
    public MediatorServiceConfiguration AddOpenStreamBehavior(Type openBehaviorType, ServiceLifetime? lifetime = null)
    {
        ValidateOpenGeneric(openBehaviorType, typeof(IStreamPipelineBehavior<,>), nameof(openBehaviorType));
        _pipelineRegistrations.Add(new PipelineRegistration(typeof(IStreamPipelineBehavior<,>), openBehaviorType, lifetime));
        return this;
    }

    /// <summary>
    /// Register a closed stream pipeline behavior by its concrete type.
    /// </summary>
    public MediatorServiceConfiguration AddStreamBehavior<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] TBehavior>(ServiceLifetime? lifetime = null) where TBehavior : class
    {
        AddClosedService(typeof(TBehavior), typeof(IStreamPipelineBehavior<,>), lifetime);
        return this;
    }

    // ══════════════════════════════════════════════════════════════════
    // Pre/Post Processors
    // ══════════════════════════════════════════════════════════════════

    /// <summary>Register a closed request pre-processor.</summary>
    public MediatorServiceConfiguration AddRequestPreProcessor<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] TProcessor>(ServiceLifetime? lifetime = null) where TProcessor : class
    {
        AddClosedService(typeof(TProcessor), typeof(IRequestPreProcessor<>), lifetime);
        return this;
    }

    /// <summary>Register an open generic request pre-processor.</summary>
    public MediatorServiceConfiguration AddOpenRequestPreProcessor(Type openType, ServiceLifetime? lifetime = null)
    {
        ValidateOpenGeneric(openType, typeof(IRequestPreProcessor<>), nameof(openType));
        _pipelineRegistrations.Add(new PipelineRegistration(typeof(IRequestPreProcessor<>), openType, lifetime));
        return this;
    }

    /// <summary>Register a closed request post-processor.</summary>
    public MediatorServiceConfiguration AddRequestPostProcessor<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] TProcessor>(ServiceLifetime? lifetime = null) where TProcessor : class
    {
        AddClosedService(typeof(TProcessor), typeof(IRequestPostProcessor<,>), lifetime);
        return this;
    }

    /// <summary>Register an open generic request post-processor.</summary>
    public MediatorServiceConfiguration AddOpenRequestPostProcessor(Type openType, ServiceLifetime? lifetime = null)
    {
        ValidateOpenGeneric(openType, typeof(IRequestPostProcessor<,>), nameof(openType));
        _pipelineRegistrations.Add(new PipelineRegistration(typeof(IRequestPostProcessor<,>), openType, lifetime));
        return this;
    }

    // ══════════════════════════════════════════════════════════════════
    // Exception Handlers & Actions
    // ══════════════════════════════════════════════════════════════════

    /// <summary>Register a closed exception handler.</summary>
    public MediatorServiceConfiguration AddExceptionHandler<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] THandler>(ServiceLifetime? lifetime = null) where THandler : class
    {
        AddClosedService(typeof(THandler), typeof(IRequestExceptionHandler<,,>), lifetime);
        return this;
    }

    /// <summary>Register a closed exception action.</summary>
    public MediatorServiceConfiguration AddExceptionAction<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] TAction>(ServiceLifetime? lifetime = null) where TAction : class
    {
        AddClosedService(typeof(TAction), typeof(IRequestExceptionAction<,>), lifetime);
        return this;
    }

    // ══════════════════════════════════════════════════════════════════
    // Notification Publisher
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Set a custom notification publisher. When registered, the mediator delegates
    /// all notification publishes to this publisher instead of the built-in strategies.
    /// </summary>
    public MediatorServiceConfiguration SetNotificationPublisher<TPublisher>() where TPublisher : class, INotificationPublisher
    {
        NotificationPublisherType = typeof(TPublisher);
        return this;
    }

    // ══════════════════════════════════════════════════════════════════
    // Registration Helpers — Called by generated code
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Registers the mediator implementation along with ISender and IPublisher
    /// forwarding services. Called by the source-generated AddSwiftMediator method.
    /// </summary>
    public void RegisterMediator<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TMediator>(IServiceCollection services) where TMediator : class, IMediator
    {
        var sl = MapLifetime(MediatorLifetime);
        services.Add(new ServiceDescriptor(typeof(IMediator), typeof(TMediator), sl));
        services.Add(new ServiceDescriptor(typeof(ISender), sp => sp.GetRequiredService<IMediator>(), sl));
        services.Add(new ServiceDescriptor(typeof(IPublisher), sp => sp.GetRequiredService<IMediator>(), sl));
    }

    /// <summary>
    /// Registers a concrete handler type with the configured handler lifetime.
    /// Called by the source-generated AddSwiftMediator method.
    /// </summary>
    public void RegisterHandler(IServiceCollection services, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type handlerType)
    {
        services.Add(new ServiceDescriptor(handlerType, handlerType, MapLifetime(Lifetime)));
    }

    /// <summary>
    /// Registers an open generic handler with the configured handler lifetime.
    /// Called by the source-generated AddSwiftMediator method.
    /// </summary>
    public void RegisterOpenGenericHandler(IServiceCollection services, Type serviceType, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type implementationType)
    {
        services.Add(new ServiceDescriptor(serviceType, implementationType, MapLifetime(Lifetime)));
    }

    /// <summary>
    /// Applies all fluent registrations and assembly scanning results to the service collection.
    /// Called by the source-generated AddSwiftMediator method as the final step.
    /// </summary>
    [RequiresUnreferencedCode("Assembly scanning uses reflection. For AOT/trimmed apps, use explicit registration methods instead.")]
    public void Apply(IServiceCollection services)
    {
        // 1. Assembly scanning
        foreach (var assembly in _assembliesToScan)
            ScanAssembly(assembly, services);

        // 2. Explicit pipeline registrations
        foreach (var reg in _pipelineRegistrations)
        {
            var sl = reg.Lifetime ?? MapLifetime(Lifetime);
            services.Add(new ServiceDescriptor(reg.ServiceType, reg.ImplementationType, sl));
        }

        // 3. Notification publisher
        if (NotificationPublisherType != null)
            services.Add(new ServiceDescriptor(typeof(INotificationPublisher), NotificationPublisherType, ServiceLifetime.Singleton));
    }

    // ══════════════════════════════════════════════════════════════════
    // Assembly Scanning Engine
    // ══════════════════════════════════════════════════════════════════

    private static readonly Type[] PipelineInterfaceDefinitions =
    {
        typeof(IPipelineBehavior<,>),
        typeof(IStreamPipelineBehavior<,>),
        typeof(IRequestPreProcessor<>),
        typeof(IRequestPostProcessor<,>),
        typeof(IRequestExceptionHandler<,,>),
        typeof(IRequestExceptionAction<,>),
    };

    private void ScanAssembly(Assembly assembly, IServiceCollection services)
    {
        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types.Where(t => t != null).ToArray()!;
        }

        foreach (var type in types)
        {
            if (type.IsAbstract || type.IsInterface)
                continue;

            foreach (var iface in type.GetInterfaces())
            {
                if (!iface.IsGenericType)
                    continue;

                var genericDef = iface.GetGenericTypeDefinition();

                foreach (var pipelineIface in PipelineInterfaceDefinitions)
                {
                    if (genericDef != pipelineIface)
                        continue;

                    var sl = MapLifetime(Lifetime);

                    if (type.IsGenericTypeDefinition)
                        services.Add(new ServiceDescriptor(pipelineIface, type, sl));
                    else
                        services.Add(new ServiceDescriptor(iface, type, sl));

                    break;
                }
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // Private Helpers
    // ══════════════════════════════════════════════════════════════════

    private void AddClosedService([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type implementationType, Type openInterfaceType, ServiceLifetime? lifetime)
    {
        foreach (var iface in implementationType.GetInterfaces())
        {
            if (iface.IsGenericType && iface.GetGenericTypeDefinition() == openInterfaceType)
            {
                _pipelineRegistrations.Add(new PipelineRegistration(iface, implementationType, lifetime));
                return;
            }
        }

        // Fallback: register as self (e.g. user explicitly typed AddBehavior<ClosedBehavior>())
        _pipelineRegistrations.Add(new PipelineRegistration(implementationType, implementationType, lifetime));
    }

    private static void ValidateOpenGeneric([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type type, Type expectedInterface, string paramName)
    {
        if (!type.IsGenericTypeDefinition)
            throw new ArgumentException($"Type {type.Name} must be an open generic type definition.", paramName);

        bool implements = type.GetInterfaces()
            .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == expectedInterface);

        if (!implements)
            throw new ArgumentException($"Type {type.Name} must implement {expectedInterface.Name}.", paramName);
    }

    private static ServiceLifetime MapLifetime(HandlerLifetime lifetime) => lifetime switch
    {
        HandlerLifetime.Singleton => ServiceLifetime.Singleton,
        HandlerLifetime.Scoped => ServiceLifetime.Scoped,
        _ => ServiceLifetime.Transient
    };

    private sealed class PipelineRegistration
    {
        public Type ServiceType { get; }
        public Type ImplementationType { get; }
        public ServiceLifetime? Lifetime { get; }

        public PipelineRegistration(Type serviceType, Type implementationType, ServiceLifetime? lifetime)
        {
            ServiceType = serviceType;
            ImplementationType = implementationType;
            Lifetime = lifetime;
        }
    }
}
