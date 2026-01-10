using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace MediateX.SourceGenerator;

/// <summary>
/// Incremental source generator for MediateX.
/// Generates compile-time handler registration to eliminate reflection.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class MediateXGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Step 1: Find all class declarations that might be handlers
        var handlerDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => IsHandlerCandidate(node),
                transform: static (ctx, _) => GetHandlerInfo(ctx))
            .Where(static info => info is not null);

        // Step 2: Collect all handlers and combine with compilation
        var compilationAndHandlers = context.CompilationProvider
            .Combine(handlerDeclarations.Collect());

        // Step 3: Generate the source code
        context.RegisterSourceOutput(compilationAndHandlers,
            static (ctx, source) => Execute(ctx, source.Left, source.Right!));
    }

    /// <summary>
    /// Fast predicate to filter syntax nodes - runs on every node, must be fast.
    /// </summary>
    private static bool IsHandlerCandidate(SyntaxNode node)
    {
        // Only look at class declarations
        if (node is not ClassDeclarationSyntax classDecl)
            return false;

        // Must have base list (implements interface)
        if (classDecl.BaseList is null)
            return false;

        // Quick string check for "Handler" in base types
        var baseListText = classDecl.BaseList.ToString();
        return baseListText.Contains("IRequestHandler") ||
               baseListText.Contains("INotificationHandler") ||
               baseListText.Contains("IStreamRequestHandler");
    }

    /// <summary>
    /// Transform candidate into handler info using semantic model.
    /// </summary>
    private static HandlerInfo? GetHandlerInfo(GeneratorSyntaxContext context)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;

        if (context.SemanticModel.GetDeclaredSymbol(classDecl) is not INamedTypeSymbol classSymbol)
            return null;

        if (classSymbol.IsAbstract)
            return null;

        // Find IRequestHandler<TRequest, TResponse> interface
        foreach (var iface in classSymbol.AllInterfaces)
        {
            var ifaceName = iface.OriginalDefinition.ToDisplayString();

            // IRequestHandler<TRequest, TResponse>
            if (ifaceName == "MediateX.IRequestHandler<TRequest, TResponse>" &&
                iface.TypeArguments.Length == 2)
            {
                return new HandlerInfo(
                    HandlerType.Request,
                    classSymbol.ToDisplayString(),
                    iface.TypeArguments[0].ToDisplayString(),
                    iface.TypeArguments[1].ToDisplayString());
            }

            // IRequestHandler<TRequest> (void/Unit)
            if (ifaceName == "MediateX.IRequestHandler<TRequest>" &&
                iface.TypeArguments.Length == 1)
            {
                return new HandlerInfo(
                    HandlerType.RequestVoid,
                    classSymbol.ToDisplayString(),
                    iface.TypeArguments[0].ToDisplayString(),
                    "MediateX.Unit");
            }

            // INotificationHandler<TNotification>
            if (ifaceName == "MediateX.INotificationHandler<TNotification>" &&
                iface.TypeArguments.Length == 1)
            {
                return new HandlerInfo(
                    HandlerType.Notification,
                    classSymbol.ToDisplayString(),
                    iface.TypeArguments[0].ToDisplayString(),
                    null);
            }

            // IStreamRequestHandler<TRequest, TResponse>
            if (ifaceName == "MediateX.IStreamRequestHandler<TRequest, TResponse>" &&
                iface.TypeArguments.Length == 2)
            {
                return new HandlerInfo(
                    HandlerType.Stream,
                    classSymbol.ToDisplayString(),
                    iface.TypeArguments[0].ToDisplayString(),
                    iface.TypeArguments[1].ToDisplayString());
            }
        }

        return null;
    }

    /// <summary>
    /// Generate the source code for DI registration.
    /// </summary>
    private static void Execute(
        SourceProductionContext context,
        Compilation compilation,
        ImmutableArray<HandlerInfo?> handlers)
    {
        var validHandlers = handlers
            .Where(h => h is not null)
            .Cast<HandlerInfo>()
            .Distinct()
            .ToList();

        if (validHandlers.Count == 0)
        {
            // Generate empty registration if no handlers found
            context.AddSource("MediateX.Generated.g.cs", SourceText.From(
                GenerateEmptyRegistration(), Encoding.UTF8));
            return;
        }

        var source = GenerateRegistration(validHandlers);
        context.AddSource("MediateX.Generated.g.cs", SourceText.From(source, Encoding.UTF8));
    }

    private static string GenerateEmptyRegistration()
    {
        return """
            // <auto-generated />
            // Generated by MediateX.SourceGenerator

            #nullable enable

            namespace Microsoft.Extensions.DependencyInjection
            {
                /// <summary>
                /// Generated extension methods for MediateX registration.
                /// </summary>
                public static class MediateXGeneratedExtensions
                {
                    /// <summary>
                    /// Adds MediateX handlers using compile-time generated registration.
                    /// No handlers were found in this assembly.
                    /// </summary>
                    public static IServiceCollection AddMediateXGenerated(this IServiceCollection services)
                    {
                        // No handlers found during compilation
                        return services;
                    }
                }
            }
            """;
    }

    private static string GenerateRegistration(List<HandlerInfo> handlers)
    {
        var sb = new StringBuilder();

        sb.AppendLine("""
            // <auto-generated />
            // Generated by MediateX.SourceGenerator

            #nullable enable

            using Microsoft.Extensions.DependencyInjection;
            using Microsoft.Extensions.DependencyInjection.Extensions;

            namespace Microsoft.Extensions.DependencyInjection
            {
                /// <summary>
                /// Generated extension methods for MediateX registration.
                /// </summary>
                public static class MediateXGeneratedExtensions
                {
                    /// <summary>
                    /// Adds MediateX handlers using compile-time generated registration.
                    /// This method registers handlers without using reflection.
                    /// </summary>
                    public static IServiceCollection AddMediateXGenerated(this IServiceCollection services)
                    {
            """);

        // Group handlers by type for comments
        var requestHandlers = handlers.Where(h => h.Type == HandlerType.Request || h.Type == HandlerType.RequestVoid).ToList();
        var notificationHandlers = handlers.Where(h => h.Type == HandlerType.Notification).ToList();
        var streamHandlers = handlers.Where(h => h.Type == HandlerType.Stream).ToList();

        if (requestHandlers.Count > 0)
        {
            sb.AppendLine("            // Request Handlers");
            foreach (var handler in requestHandlers)
            {
                if (handler.Type == HandlerType.RequestVoid)
                {
                    sb.AppendLine($"            services.TryAddTransient<MediateX.IRequestHandler<{handler.RequestType}>, {handler.HandlerType}>();");
                }
                else
                {
                    sb.AppendLine($"            services.TryAddTransient<MediateX.IRequestHandler<{handler.RequestType}, {handler.ResponseType}>, {handler.HandlerType}>();");
                }
            }
            sb.AppendLine();
        }

        if (notificationHandlers.Count > 0)
        {
            sb.AppendLine("            // Notification Handlers");
            foreach (var handler in notificationHandlers)
            {
                // Notifications can have multiple handlers, so use Add not TryAdd
                sb.AppendLine($"            services.AddTransient<MediateX.INotificationHandler<{handler.RequestType}>, {handler.HandlerType}>();");
            }
            sb.AppendLine();
        }

        if (streamHandlers.Count > 0)
        {
            sb.AppendLine("            // Stream Handlers");
            foreach (var handler in streamHandlers)
            {
                sb.AppendLine($"            services.TryAddTransient<MediateX.IStreamRequestHandler<{handler.RequestType}, {handler.ResponseType}>, {handler.HandlerType}>();");
            }
            sb.AppendLine();
        }

        sb.AppendLine("""
                        return services;
                    }
                }
            }
            """);

        // Add summary comment with handler count
        var header = $"""
            // Handler Summary:
            // - Request Handlers: {requestHandlers.Count}
            // - Notification Handlers: {notificationHandlers.Count}
            // - Stream Handlers: {streamHandlers.Count}
            // - Total: {handlers.Count}


            """;

        return header + sb.ToString();
    }
}

/// <summary>
/// Type of handler discovered.
/// </summary>
internal enum HandlerType
{
    Request,
    RequestVoid,
    Notification,
    Stream
}

/// <summary>
/// Information about a discovered handler.
/// </summary>
internal sealed record HandlerInfo(
    HandlerType Type,
    string HandlerType,
    string RequestType,
    string? ResponseType);
