using System.Collections.Immutable;
using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Linq;

namespace SwiftMediator.Generator;

[Generator]
public class MediatorGenerator : IIncrementalGenerator
{
    // ── Diagnostic Descriptors ──────────────────────────────────────────

    private static readonly DiagnosticDescriptor DuplicateHandlerDiagnostic = new(
        id: "SWIFT001",
        title: "Duplicate request handler",
        messageFormat: "Multiple handlers found for request type '{0}'. Only one handler per request type is allowed.",
        category: "SwiftMediator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor DuplicateStreamHandlerDiagnostic = new(
        id: "SWIFT002",
        title: "Duplicate stream handler",
        messageFormat: "Multiple stream handlers found for request type '{0}'. Only one stream handler per request type is allowed.",
        category: "SwiftMediator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor NoHandlersDiagnostic = new(
        id: "SWIFT003",
        title: "No handlers found",
        messageFormat: "No request, notification, or stream handlers were found in the assembly",
        category: "SwiftMediator",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor GenericHandlerSkippedDiagnostic = new(
        id: "SWIFT004",
        title: "Open generic handler registered",
        messageFormat: "Handler '{0}' is an open generic type. It will be registered as an open generic service.",
        category: "SwiftMediator",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    // ── Pipeline Setup ──────────────────────────────────────────────────

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var classDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => IsSyntaxTargetForGeneration(s),
                transform: static (ctx, _) => GetSemanticTargetForGeneration(ctx))
            .Where(static m => m.HasValue)
            .Select(static (m, _) => m!.Value);

        var compilationAndClasses = context.CompilationProvider.Combine(classDeclarations.Collect());

        context.RegisterSourceOutput(compilationAndClasses, static (spc, source) => Execute(source.Left, source.Right, spc));
    }

    private static bool IsSyntaxTargetForGeneration(SyntaxNode node)
    {
        if (node is not TypeDeclarationSyntax typeDec) return false;
        if (typeDec.BaseList == null || typeDec.BaseList.Types.Count == 0) return false;
        return node is ClassDeclarationSyntax or RecordDeclarationSyntax;
    }

    private static (TypeDeclarationSyntax? TypeNode, INamedTypeSymbol? Symbol)? GetSemanticTargetForGeneration(GeneratorSyntaxContext context)
    {
        var typeDeclaration = (TypeDeclarationSyntax)context.Node;
        var symbol = context.SemanticModel.GetDeclaredSymbol(typeDeclaration) as INamedTypeSymbol;
        if (symbol == null) return null;
        return (typeDeclaration, symbol);
    }

    // ── Execution ───────────────────────────────────────────────────────

    private static void Execute(Compilation compilation, ImmutableArray<(TypeDeclarationSyntax? TypeNode, INamedTypeSymbol? Symbol)> classes, SourceProductionContext context)
    {
        if (classes.IsDefaultOrEmpty)
        {
            GenerateEmptyRegistration(context);
            return;
        }

        var handlers = new List<HandlerInfo>();
        var openGenericHandlers = new List<OpenGenericHandlerInfo>();
        var seenSymbols = new HashSet<string>();

        foreach (var tuple in classes)
        {
            var symbol = tuple.Symbol;
            if (symbol == null || symbol.IsAbstract) continue;

            var symbolKey = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (!seenSymbols.Add(symbolKey)) continue;

            // #6: Open generic handler detection
            if (symbol.IsGenericType)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    GenericHandlerSkippedDiagnostic,
                    symbol.Locations.FirstOrDefault() ?? Location.None,
                    symbol.Name));

                foreach (var iface in symbol.AllInterfaces)
                {
                    var originalDef = iface.OriginalDefinition.ToDisplayString();
                    if (originalDef == "SwiftMediator.Core.IRequestHandler<TRequest, TResponse>")
                    {
                        openGenericHandlers.Add(new OpenGenericHandlerInfo(
                            symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                            iface.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                            OpenGenericKind.Request));
                    }
                    else if (originalDef == "SwiftMediator.Core.INotificationHandler<TNotification>")
                    {
                        openGenericHandlers.Add(new OpenGenericHandlerInfo(
                            symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                            iface.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                            OpenGenericKind.Notification));
                    }
                    else if (originalDef == "SwiftMediator.Core.IStreamRequestHandler<TRequest, TResponse>")
                    {
                        openGenericHandlers.Add(new OpenGenericHandlerInfo(
                            symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                            iface.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                            OpenGenericKind.Stream));
                    }
                }
                continue;
            }

            foreach (var iface in symbol.AllInterfaces)
            {
                var originalDef = iface.OriginalDefinition.ToDisplayString();

                if (originalDef == "SwiftMediator.Core.IRequestHandler<TRequest, TResponse>")
                {
                    var requestType = iface.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    var responseType = iface.TypeArguments[1].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    var handlerType = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                    var requestSymbol = iface.TypeArguments[0] as INamedTypeSymbol;
                    var baseRequestTypes = new List<string>();
                    if (requestSymbol != null)
                        CollectBaseRequestTypes(requestSymbol, baseRequestTypes);

                    handlers.Add(new HandlerInfo(requestType, responseType, handlerType, HandlerKind.Request, baseRequestTypes));
                }
                else if (originalDef == "SwiftMediator.Core.INotificationHandler<TNotification>")
                {
                    var notificationType = iface.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    var handlerType = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    handlers.Add(new HandlerInfo(notificationType, "void", handlerType, HandlerKind.Notification));
                }
                else if (originalDef == "SwiftMediator.Core.IStreamRequestHandler<TRequest, TResponse>")
                {
                    var requestType = iface.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    var responseType = iface.TypeArguments[1].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    var handlerType = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    handlers.Add(new HandlerInfo(requestType, responseType, handlerType, HandlerKind.Stream));
                }
            }
        }

        // ── Diagnostics ──
        foreach (var group in handlers.Where(h => h.Kind == HandlerKind.Request).GroupBy(h => h.RequestType).Where(g => g.Count() > 1))
            context.ReportDiagnostic(Diagnostic.Create(DuplicateHandlerDiagnostic, Location.None, group.Key));

        foreach (var group in handlers.Where(h => h.Kind == HandlerKind.Stream).GroupBy(h => h.RequestType).Where(g => g.Count() > 1))
            context.ReportDiagnostic(Diagnostic.Create(DuplicateStreamHandlerDiagnostic, Location.None, group.Key));

        if (handlers.Count == 0 && openGenericHandlers.Count == 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(NoHandlersDiagnostic, Location.None));
            GenerateEmptyRegistration(context);
            return;
        }

        context.AddSource("SwiftGeneratedMediator.g.cs", SourceText.From(BuildMediatorClass(handlers), Encoding.UTF8));
        context.AddSource("SwiftMediatorDependencyInjection.g.cs", SourceText.From(BuildDependencyInjectionClass(handlers, openGenericHandlers), Encoding.UTF8));
    }

    /// <summary>
    /// Polymorphic: Collect base types/interfaces that are also IRequest&lt;T&gt;.
    /// </summary>
    private static void CollectBaseRequestTypes(INamedTypeSymbol requestSymbol, List<string> baseRequestTypes)
    {
        var current = requestSymbol.BaseType;
        while (current != null && current.SpecialType != SpecialType.System_Object)
        {
            foreach (var iface in current.Interfaces)
            {
                var ifaceDef = iface.OriginalDefinition.ToDisplayString();
                if (ifaceDef == "SwiftMediator.Core.IRequest<TResponse>")
                    baseRequestTypes.Add(current.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
            }
            current = current.BaseType;
        }

        foreach (var iface in requestSymbol.Interfaces)
        {
            var ifaceDef = iface.OriginalDefinition.ToDisplayString();
            if (ifaceDef != "SwiftMediator.Core.IRequest<TResponse>" &&
                ifaceDef != "SwiftMediator.Core.IRequest")
            {
                foreach (var parentIface in iface.AllInterfaces)
                {
                    var parentDef = parentIface.OriginalDefinition.ToDisplayString();
                    if (parentDef == "SwiftMediator.Core.IRequest<TResponse>")
                        baseRequestTypes.Add(iface.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                }
            }
        }
    }

    // ── Empty Registration ──────────────────────────────────────────────

    private static void GenerateEmptyRegistration(SourceProductionContext context)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Threading;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Runtime.CompilerServices;");
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        sb.AppendLine("using SwiftMediator.Core;");
        sb.AppendLine();
        sb.AppendLine("namespace SwiftMediator;");
        sb.AppendLine();
        sb.AppendLine("public sealed class GeneratedMediator : IMediator");
        sb.AppendLine("{");
        sb.AppendLine("    private readonly IServiceProvider _serviceProvider;");
        sb.AppendLine("    public GeneratedMediator(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;");
        sb.AppendLine();
        sb.AppendLine("    public ValueTask<TResponse> SendAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest<TResponse>");
        sb.AppendLine("        => throw new InvalidOperationException($\"No handler registered for type {typeof(TRequest).Name}\");");
        sb.AppendLine();
        sb.AppendLine("    public ValueTask<object?> SendAsync(object request, CancellationToken cancellationToken = default)");
        sb.AppendLine("        => throw new InvalidOperationException($\"No handler registered for type {request.GetType().Name}\");");
        sb.AppendLine();
        sb.AppendLine("    public ValueTask PublishAsync<TNotification>(TNotification notification, PublishStrategy strategy = PublishStrategy.Sequential, CancellationToken cancellationToken = default) where TNotification : INotification");
        sb.AppendLine("        => default;");
        sb.AppendLine();
        sb.AppendLine("    public IAsyncEnumerable<TResponse> CreateStream<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IStreamRequest<TResponse>");
        sb.AppendLine("        => throw new InvalidOperationException($\"No stream handler registered for type {typeof(TRequest).Name}\");");
        sb.AppendLine("}");

        context.AddSource("SwiftGeneratedMediator.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));

        var diSb = new StringBuilder();
        diSb.AppendLine("// <auto-generated/>");
        diSb.AppendLine("#nullable enable");
        diSb.AppendLine("using System;");
        diSb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        diSb.AppendLine("using SwiftMediator.Core;");
        diSb.AppendLine();
        diSb.AppendLine("namespace SwiftMediator;");
        diSb.AppendLine();
        diSb.AppendLine("public static class SwiftMediatorServiceCollectionExtensions");
        diSb.AppendLine("{");
        diSb.AppendLine("    public static IServiceCollection AddSwiftMediator(this IServiceCollection services, Action<MediatorServiceConfiguration>? configure = null)");
        diSb.AppendLine("    {");
        diSb.AppendLine("        var config = new MediatorServiceConfiguration();");
        diSb.AppendLine("        configure?.Invoke(config);");
        diSb.AppendLine("        config.RegisterMediator<GeneratedMediator>(services);");
        diSb.AppendLine("        config.Apply(services);");
        diSb.AppendLine("        return services;");
        diSb.AppendLine("    }");
        diSb.AppendLine("}");

        context.AddSource("SwiftMediatorDependencyInjection.g.cs", SourceText.From(diSb.ToString(), Encoding.UTF8));
    }

    // ════════════════════════════════════════════════════════════════════
    // Generated Mediator Class
    // ════════════════════════════════════════════════════════════════════

    private static string BuildMediatorClass(List<HandlerInfo> handlers)
    {
        var requestHandlers = handlers.Where(h => h.Kind == HandlerKind.Request).ToList();
        var notificationHandlers = handlers.Where(h => h.Kind == HandlerKind.Notification).ToList();
        var streamHandlers = handlers.Where(h => h.Kind == HandlerKind.Stream).ToList();
        var notificationsGrouped = notificationHandlers.GroupBy(h => h.RequestType).ToList();

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Linq;");
        sb.AppendLine("using System.Threading;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Runtime.CompilerServices;");
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        sb.AppendLine("using SwiftMediator.Core;");
        sb.AppendLine();
        sb.AppendLine("namespace SwiftMediator;");
        sb.AppendLine();
        sb.AppendLine("public sealed class GeneratedMediator : IMediator");
        sb.AppendLine("{");
        sb.AppendLine("    private readonly IServiceProvider _serviceProvider;");
        sb.AppendLine();
        sb.AppendLine("    public GeneratedMediator(IServiceProvider serviceProvider)");
        sb.AppendLine("    {");
        sb.AppendLine("        _serviceProvider = serviceProvider;");
        sb.AppendLine("    }");
        sb.AppendLine();

        // ════════════════════════════════════════════════════════════════
        // 1. SendAsync<TRequest, TResponse>
        // ════════════════════════════════════════════════════════════════
        sb.AppendLine("    [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine("    public ValueTask<TResponse> SendAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default)");
        sb.AppendLine("        where TRequest : IRequest<TResponse>");
        sb.AppendLine("    {");

        foreach (var handler in requestHandlers)
        {
            sb.AppendLine($"        if (typeof(TRequest) == typeof({handler.RequestType}))");
            sb.AppendLine("        {");
            sb.AppendLine($"            return SendTyped_{SanitizeName(handler.RequestType)}<TRequest, TResponse>(request, cancellationToken);");
            sb.AppendLine("        }");
            sb.AppendLine();
        }

        // Polymorphic dispatch
        sb.AppendLine("        // Polymorphic dispatch: check base types and interfaces");
        foreach (var handler in requestHandlers)
        {
            if (handler.BaseRequestTypes.Count > 0)
            {
                foreach (var baseType in handler.BaseRequestTypes)
                {
                    sb.AppendLine($"        if (typeof(TRequest) == typeof({baseType}) && request is {handler.RequestType})");
                    sb.AppendLine("        {");
                    sb.AppendLine($"            return SendTyped_{SanitizeName(handler.RequestType)}<TRequest, TResponse>(request, cancellationToken);");
                    sb.AppendLine("        }");
                }
            }
        }
        sb.AppendLine();
        sb.AppendLine("        throw new InvalidOperationException($\"No handler registered for type {typeof(TRequest).Name}\");");
        sb.AppendLine("    }");
        sb.AppendLine();

        // Per-request SendTyped methods with Pre/Post processors, Exception handlers + Exception actions
        foreach (var handler in requestHandlers)
        {
            var safeName = SanitizeName(handler.RequestType);
            sb.AppendLine($"    private async ValueTask<TResponse> SendTyped_{safeName}<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken)");
            sb.AppendLine("        where TRequest : IRequest<TResponse>");
            sb.AppendLine("    {");
            sb.AppendLine($"        var handler = _serviceProvider.GetRequiredService<{handler.HandlerType}>();");
            sb.AppendLine($"        var typed = ({handler.RequestType})(object)request!;");
            sb.AppendLine();

            // Pre-processors
            sb.AppendLine($"        foreach (var pre in _serviceProvider.GetServices<IRequestPreProcessor<{handler.RequestType}>>())");
            sb.AppendLine("            await pre.Process(typed, cancellationToken).ConfigureAwait(false);");
            sb.AppendLine();

            // Pipeline with fast-path
            sb.AppendLine("        var behaviors = _serviceProvider.GetServices<IPipelineBehavior<TRequest, TResponse>>();");
            sb.AppendLine("        using var enumerator = behaviors.GetEnumerator();");
            sb.AppendLine();

            sb.AppendLine("        try");
            sb.AppendLine("        {");
            sb.AppendLine("            TResponse response;");
            sb.AppendLine();
            sb.AppendLine("            if (!enumerator.MoveNext())");
            sb.AppendLine("            {");
            sb.AppendLine($"                response = (TResponse)(object)(await handler.Handle(typed, cancellationToken).ConfigureAwait(false))!;");
            sb.AppendLine("            }");
            sb.AppendLine("            else");
            sb.AppendLine("            {");
            sb.AppendLine("                var behaviorList = new List<IPipelineBehavior<TRequest, TResponse>>();");
            sb.AppendLine("                do { behaviorList.Add(enumerator.Current); } while (enumerator.MoveNext());");
            sb.AppendLine();
            sb.AppendLine("                RequestHandlerDelegate<TResponse> next = async () =>");
            sb.AppendLine($"                    (TResponse)(object)(await handler.Handle(typed, cancellationToken).ConfigureAwait(false))!;");
            sb.AppendLine();
            sb.AppendLine("                for (int i = behaviorList.Count - 1; i >= 0; i--)");
            sb.AppendLine("                {");
            sb.AppendLine("                    var behavior = behaviorList[i];");
            sb.AppendLine("                    var currentNext = next;");
            sb.AppendLine("                    next = () => behavior.Handle(request, currentNext, cancellationToken);");
            sb.AppendLine("                }");
            sb.AppendLine();
            sb.AppendLine("                response = await next().ConfigureAwait(false);");
            sb.AppendLine("            }");
            sb.AppendLine();

            // Post-processors
            sb.AppendLine($"            foreach (var post in _serviceProvider.GetServices<IRequestPostProcessor<{handler.RequestType}, {handler.ResponseType}>>())");
            sb.AppendLine($"                await post.Process(typed, ({handler.ResponseType})(object)response!, cancellationToken).ConfigureAwait(false);");
            sb.AppendLine();
            sb.AppendLine("            return response;");
            sb.AppendLine("        }");

            // Exception handling: actions first, then handlers
            sb.AppendLine("        catch (Exception ex)");
            sb.AppendLine("        {");
            // #1: Exception Actions — always run, cannot suppress
            sb.AppendLine($"            foreach (var action in _serviceProvider.GetServices<IRequestExceptionAction<{handler.RequestType}, Exception>>())");
            sb.AppendLine("                await action.Execute(typed, ex, cancellationToken).ConfigureAwait(false);");
            sb.AppendLine();
            // Exception Handlers — can suppress
            sb.AppendLine($"            var exceptionHandlers = _serviceProvider.GetServices<IRequestExceptionHandler<{handler.RequestType}, {handler.ResponseType}, Exception>>();");
            sb.AppendLine($"            var state = new RequestExceptionHandlerState<{handler.ResponseType}>();");
            sb.AppendLine("            foreach (var exHandler in exceptionHandlers)");
            sb.AppendLine("            {");
            sb.AppendLine("                await exHandler.Handle(typed, ex, state, cancellationToken).ConfigureAwait(false);");
            sb.AppendLine("                if (state.Handled)");
            sb.AppendLine("                    return (TResponse)(object)state.Response!;");
            sb.AppendLine("            }");
            sb.AppendLine("            throw;");
            sb.AppendLine("        }");

            sb.AppendLine("    }");
            sb.AppendLine();
        }

        // ════════════════════════════════════════════════════════════════
        // 2. SendAsync(object) — Dynamic dispatch
        // ════════════════════════════════════════════════════════════════
        sb.AppendLine("    public async ValueTask<object?> SendAsync(object request, CancellationToken cancellationToken = default)");
        sb.AppendLine("    {");
        sb.AppendLine("        switch (request)");
        sb.AppendLine("        {");
        foreach (var handler in requestHandlers)
        {
            sb.AppendLine($"            case {handler.RequestType} req:");
            sb.AppendLine($"                return await SendAsync<{handler.RequestType}, {handler.ResponseType}>(req, cancellationToken).ConfigureAwait(false);");
        }
        sb.AppendLine("            default:");
        sb.AppendLine("                throw new InvalidOperationException($\"No handler registered for type {request.GetType().Name}\");");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();

        // ════════════════════════════════════════════════════════════════
        // 3. PublishAsync — polymorphic notification dispatch
        // ════════════════════════════════════════════════════════════════
        sb.AppendLine("    public async ValueTask PublishAsync<TNotification>(TNotification notification, PublishStrategy strategy = PublishStrategy.Sequential, CancellationToken cancellationToken = default)");
        sb.AppendLine("        where TNotification : INotification");
        sb.AppendLine("    {");
        sb.AppendLine("        var executors = new List<NotificationHandlerExecutor>();");
        sb.AppendLine();

        foreach (var group in notificationsGrouped)
        {
            var handlersInGroup = group.ToList();
            var safeName = SanitizeName(group.Key);

            sb.AppendLine($"        if (notification is {group.Key})");
            sb.AppendLine("        {");
            for (int i = 0; i < handlersInGroup.Count; i++)
            {
                var hvar = $"h_{safeName}_{i}";
                sb.AppendLine($"            var {hvar} = _serviceProvider.GetRequiredService<{handlersInGroup[i].HandlerType}>();");
                sb.AppendLine($"            executors.Add(new NotificationHandlerExecutor({hvar}, (n, ct) => {hvar}.Handle(({group.Key})n, ct)));");
            }
            sb.AppendLine("        }");
            sb.AppendLine();
        }

        sb.AppendLine("        if (executors.Count == 0) return;");
        sb.AppendLine();
        sb.AppendLine("        var customPublisher = _serviceProvider.GetService<INotificationPublisher>();");
        sb.AppendLine("        if (customPublisher != null)");
        sb.AppendLine("        {");
        sb.AppendLine("            await customPublisher.Publish(executors, notification!, cancellationToken).ConfigureAwait(false);");
        sb.AppendLine("            return;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        switch (strategy)");
        sb.AppendLine("        {");
        sb.AppendLine("            case PublishStrategy.Sequential:");
        sb.AppendLine("                foreach (var executor in executors)");
        sb.AppendLine("                    await executor.HandlerCallback(notification!, cancellationToken).ConfigureAwait(false);");
        sb.AppendLine("                break;");
        sb.AppendLine("            case PublishStrategy.Parallel:");
        sb.AppendLine("                var tasks = new Task[executors.Count];");
        sb.AppendLine("                for (int i = 0; i < executors.Count; i++)");
        sb.AppendLine("                    tasks[i] = executors[i].HandlerCallback(notification!, cancellationToken).AsTask();");
        sb.AppendLine("                await Task.WhenAll(tasks).ConfigureAwait(false);");
        sb.AppendLine("                break;");
        sb.AppendLine("            case PublishStrategy.FireAndForget:");
        sb.AppendLine("                foreach (var executor in executors)");
        sb.AppendLine("                    try { _ = SafeFireAndForget(executor.HandlerCallback(notification!, cancellationToken)); }");
        sb.AppendLine("                    catch (Exception) { /* Synchronous throw suppressed */ }");
        sb.AppendLine("                break;");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();

        // ════════════════════════════════════════════════════════════════
        // 4. CreateStream — with IStreamPipelineBehavior support
        // ════════════════════════════════════════════════════════════════
        sb.AppendLine("    public IAsyncEnumerable<TResponse> CreateStream<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default)");
        sb.AppendLine("        where TRequest : IStreamRequest<TResponse>");
        sb.AppendLine("    {");
        foreach (var handler in streamHandlers)
        {
            sb.AppendLine($"        if (typeof(TRequest) == typeof({handler.RequestType}))");
            sb.AppendLine("        {");
            sb.AppendLine($"            var handler = _serviceProvider.GetRequiredService<{handler.HandlerType}>();");
            sb.AppendLine($"            var typed = ({handler.RequestType})(object)request!;");
            sb.AppendLine();
            // #2: Stream pipeline behaviors
            sb.AppendLine($"            var streamBehaviors = _serviceProvider.GetServices<IStreamPipelineBehavior<{handler.RequestType}, {handler.ResponseType}>>();");
            sb.AppendLine("            var behaviorList = streamBehaviors.ToList();");
            sb.AppendLine();
            sb.AppendLine("            if (behaviorList.Count == 0)");
            sb.AppendLine("            {");
            sb.AppendLine($"                return (IAsyncEnumerable<TResponse>)handler.Handle(typed, cancellationToken);");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine($"            StreamHandlerDelegate<{handler.ResponseType}> next = () => handler.Handle(typed, cancellationToken);");
            sb.AppendLine("            for (int i = behaviorList.Count - 1; i >= 0; i--)");
            sb.AppendLine("            {");
            sb.AppendLine("                var behavior = behaviorList[i];");
            sb.AppendLine("                var currentNext = next;");
            sb.AppendLine("                next = () => behavior.Handle(typed, currentNext, cancellationToken);");
            sb.AppendLine("            }");
            sb.AppendLine($"            return (IAsyncEnumerable<TResponse>)next();");
            sb.AppendLine("        }");
        }
        sb.AppendLine("        throw new InvalidOperationException($\"No stream handler registered for type {typeof(TRequest).Name}\");");
        sb.AppendLine("    }");
        sb.AppendLine();

        // Helpers
        sb.AppendLine("    private static async Task SafeFireAndForget(ValueTask task)");
        sb.AppendLine("    {");
        sb.AppendLine("        try { await task.ConfigureAwait(false); }");
        sb.AppendLine("        catch (Exception) { /* Suppressed in fire-and-forget mode */ }");
        sb.AppendLine("    }");

        sb.AppendLine("}");
        return sb.ToString();
    }

    // ════════════════════════════════════════════════════════════════════
    // Generated DI Registration
    // ════════════════════════════════════════════════════════════════════

    private static string BuildDependencyInjectionClass(List<HandlerInfo> handlers, List<OpenGenericHandlerInfo> openGenericHandlers)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using System;");
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        sb.AppendLine("using SwiftMediator.Core;");
        sb.AppendLine();
        sb.AppendLine("namespace SwiftMediator;");
        sb.AppendLine();
        sb.AppendLine("public static class SwiftMediatorServiceCollectionExtensions");
        sb.AppendLine("{");
        sb.AppendLine("    public static IServiceCollection AddSwiftMediator(this IServiceCollection services, Action<MediatorServiceConfiguration>? configure = null)");
        sb.AppendLine("    {");
        sb.AppendLine("        var config = new MediatorServiceConfiguration();");
        sb.AppendLine("        configure?.Invoke(config);");
        sb.AppendLine();
        sb.AppendLine("        // Register the mediator (implements IMediator, ISender, IPublisher)");
        sb.AppendLine("        config.RegisterMediator<GeneratedMediator>(services);");
        sb.AppendLine();

        // Register concrete handlers
        sb.AppendLine("        // Register all compile-time discovered handlers");
        foreach (var handler in handlers)
            sb.AppendLine($"        config.RegisterHandler(services, typeof({handler.HandlerType}));");
        sb.AppendLine();

        // Register open generic handlers
        if (openGenericHandlers.Count > 0)
        {
            sb.AppendLine("        // Register open generic handlers");
            foreach (var og in openGenericHandlers)
                sb.AppendLine($"        config.RegisterOpenGenericHandler(services, typeof({og.InterfaceType}), typeof({og.HandlerType}));");
            sb.AppendLine();
        }

        sb.AppendLine("        // Apply fluent registrations and assembly scanning");
        sb.AppendLine("        config.Apply(services);");
        sb.AppendLine();
        sb.AppendLine("        return services;");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private static string SanitizeName(string fqn)
    {
        return fqn.Replace("global::", "").Replace(".", "_").Replace("<", "_").Replace(">", "_").Replace(",", "_").Replace(" ", "");
    }

    // ── Supporting Types ────────────────────────────────────────────────

    private enum HandlerKind
    {
        Request,
        Notification,
        Stream
    }

    private class HandlerInfo
    {
        public string RequestType { get; }
        public string ResponseType { get; }
        public string HandlerType { get; }
        public HandlerKind Kind { get; }
        public List<string> BaseRequestTypes { get; }

        public HandlerInfo(string req, string res, string handler, HandlerKind kind, List<string>? baseRequestTypes = null)
        {
            RequestType = req;
            ResponseType = res;
            HandlerType = handler;
            Kind = kind;
            BaseRequestTypes = baseRequestTypes ?? new List<string>();
        }
    }

    private enum OpenGenericKind
    {
        Request,
        Notification,
        Stream
    }

    private class OpenGenericHandlerInfo
    {
        public string HandlerType { get; }
        public string InterfaceType { get; }
        public OpenGenericKind Kind { get; }

        public OpenGenericHandlerInfo(string handlerType, string interfaceType, OpenGenericKind kind)
        {
            HandlerType = handlerType;
            InterfaceType = interfaceType;
            Kind = kind;
        }
    }
}
