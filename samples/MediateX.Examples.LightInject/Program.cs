using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using LightInject;
using MediateX;
using MediateX.Behaviors;
using MediateX.Examples;
using MediateX.Processing;
using MediateX.Publishing;

namespace MediateX.Examples.LightInject;

class Program
{
    static Task Main(string[] args)
    {
        WrappingWriter writer = new(Console.Out);
        var mediator = BuildMediator(writer);

        return Runner.Run(mediator, writer, "LightInject");
    }

    private static IMediator BuildMediator(WrappingWriter writer)
    {
        ServiceContainer container = new(ContainerOptions.Default);

        // Register handlers from assembly
        container.RegisterAssembly(typeof(Ping).GetTypeInfo().Assembly, (serviceType, implementingType) =>
            serviceType.IsConstructedGenericType &&
            (
                serviceType.GetGenericTypeDefinition() == typeof(IRequestHandler<,>) ||
                serviceType.GetGenericTypeDefinition() == typeof(INotificationHandler<>)
            ));

        // Register core services
        container.RegisterInstance<TextWriter>(writer);
        container.Register<INotificationPublisher, ForeachAwaitPublisher>(new PerContainerLifetime());

        // Pipeline behaviors
        container.RegisterOrdered(typeof(IPipelineBehavior<,>),
        [
            typeof(RequestPreProcessorBehavior<,>),
            typeof(RequestPostProcessorBehavior<,>),
            typeof(GenericPipelineBehavior<,>)
        ], type => new PerContainerLifetime());

        container.RegisterOrdered(typeof(IRequestPostProcessor<,>),
        [
            typeof(GenericRequestPostProcessor<,>),
            typeof(ConstrainedRequestPostProcessor<,>)
        ], type => new PerContainerLifetime());

        container.Register(typeof(IRequestPreProcessor<>), typeof(GenericRequestPreProcessor<>), new PerContainerLifetime());

        // Create IServiceProvider adapter and register Mediator
        var serviceProvider = new LightInjectServiceProvider(container);
        container.RegisterInstance<IServiceProvider>(serviceProvider);
        container.Register<IMediator, Mediator>(new PerContainerLifetime());

        return container.GetInstance<IMediator>();
    }
}

/// <summary>
/// Simple IServiceProvider adapter for LightInject container
/// </summary>
internal class LightInjectServiceProvider(IServiceContainer container) : IServiceProvider
{
    public object? GetService(Type serviceType)
    {
        // Handle IEnumerable<T> requests
        if (serviceType.IsGenericType && serviceType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
        {
            var itemType = serviceType.GetGenericArguments()[0];
            return container.GetAllInstances(itemType);
        }

        return container.CanGetInstance(serviceType, string.Empty)
            ? container.GetInstance(serviceType)
            : null;
    }
}
