using MediateX.Processing;
using MediateX.Core;
using MediateX.Behaviors;
using MediateX.ExceptionHandling;
using MediateX.Publishing;
using MediateX;
using System.IO;
using System.Threading.Tasks;
using MediateX.Examples;
using Microsoft.Extensions.DependencyInjection;

namespace MediateX.Examples.SimpleInjector;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using global::SimpleInjector;

internal static class Program
{
    private static Task Main(string[] args)
    {
        WrappingWriter writer = new(Console.Out);
        var mediator = BuildMediator(writer);

        return Runner.Run(mediator, writer, "SimpleInjector", true);
    }

    private static IMediator BuildMediator(WrappingWriter writer)
    {
        Container container = new();

        ServiceCollection services = new();

        services
            .AddSimpleInjector(container);

        var assemblies = GetAssemblies().ToArray();
        container.RegisterSingleton<IMediator, Mediator>();
        container.Register(typeof(IRequestHandler<,>), assemblies);

        RegisterHandlers(container, typeof(INotificationHandler<>), assemblies);
        RegisterHandlers(container, typeof(IRequestExceptionAction<,>), assemblies);
        RegisterHandlers(container, typeof(IRequestExceptionHandler<,,>), assemblies);
        RegisterHandlers(container, typeof(IStreamRequestHandler<,>), assemblies);

        container.Register(() => (TextWriter) writer, Lifestyle.Singleton);
        container.RegisterSingleton<INotificationPublisher, ForeachAwaitPublisher>();

        //Pipeline
        container.Collection.Register(typeof(IPipelineBehavior<,>), new[]
        {
            typeof(RequestExceptionProcessorBehavior<,>),
            typeof(RequestExceptionActionProcessorBehavior<,>),
            typeof(RequestPreProcessorBehavior<,>),
            typeof(RequestPostProcessorBehavior<,>),
            typeof(GenericPipelineBehavior<,>)
        });
        container.Collection.Register(typeof(IRequestPreProcessor<>), new[] { typeof(GenericRequestPreProcessor<>) });
        container.Collection.Register(typeof(IRequestPostProcessor<,>), new[] { typeof(GenericRequestPostProcessor<,>), typeof(ConstrainedRequestPostProcessor<,>) });
        container.Collection.Register(typeof(IStreamPipelineBehavior<,>), new[]
        {
            typeof(GenericStreamPipelineBehavior<,>)
        });

        var serviceProvider = services.BuildServiceProvider().UseSimpleInjector(container);

        container.RegisterInstance<IServiceProvider>(container);

        var mediator = container.GetRequiredService<IMediator>();

        return mediator;
    }

    private static void RegisterHandlers(Container container, Type collectionType, Assembly[] assemblies)
    {
        // we have to do this because by default, generic type definitions (such as the Constrained Notification Handler) won't be registered
        var handlerTypes = container.GetTypesToRegister(collectionType, assemblies, new TypesToRegisterOptions
        {
            IncludeGenericTypeDefinitions = true,
            IncludeComposites = false,
        });

        container.Collection.Register(collectionType, handlerTypes);
    }

    private static IEnumerable<Assembly> GetAssemblies()
    {
        yield return typeof(IMediator).GetTypeInfo().Assembly;
        yield return typeof(Ping).GetTypeInfo().Assembly;
    }
}