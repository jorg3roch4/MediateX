using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Castle.MicroKernel.Registration;
using Castle.MicroKernel.Resolvers.SpecializedResolvers;
using Castle.Windsor;
using MediateX;
using MediateX.Behaviors;
using MediateX.ExceptionHandling;
using MediateX.Examples;
using MediateX.Processing;
using MediateX.Publishing;

namespace MediateX.Examples.Windsor;

internal class Program
{
    private static Task Main(string[] args)
    {
        var writer = new WrappingWriter(Console.Out);
        var mediator = BuildMediator(writer);

        return Runner.Run(mediator, writer, "Castle.Windsor", true);
    }

    private static IMediator BuildMediator(WrappingWriter writer)
    {
        var container = new WindsorContainer();
        container.Kernel.Resolver.AddSubResolver(new CollectionResolver(container.Kernel));
        container.Kernel.AddHandlersFilter(new ContravariantFilter());

        // *** The default lifestyle for Windsor is Singleton
        // *** If you are using ASP.net, it's better to register your services with 'Per Web Request LifeStyle'.

        var fromAssemblyContainingPing = Classes.FromAssemblyContaining<Ping>();
        container.Register(fromAssemblyContainingPing.BasedOn(typeof(IRequestHandler<,>)).WithServiceAllInterfaces().AllowMultipleMatches());
        container.Register(fromAssemblyContainingPing.BasedOn(typeof(INotificationHandler<>)).WithServiceAllInterfaces().AllowMultipleMatches());
        container.Register(Component.For(typeof(IPipelineBehavior<,>)).ImplementedBy(typeof(RequestExceptionProcessorBehavior<,>)));
        container.Register(Component.For(typeof(IPipelineBehavior<,>)).ImplementedBy(typeof(RequestExceptionActionProcessorBehavior<,>)));
        container.Register(fromAssemblyContainingPing.BasedOn(typeof(IRequestExceptionAction<,>)).WithServiceAllInterfaces().AllowMultipleMatches());
        container.Register(fromAssemblyContainingPing.BasedOn(typeof(IRequestExceptionHandler<,,>)).WithServiceAllInterfaces().AllowMultipleMatches());
        container.Register(fromAssemblyContainingPing.BasedOn(typeof(IStreamRequestHandler<,>)).WithServiceAllInterfaces().AllowMultipleMatches());
        container.Register(fromAssemblyContainingPing.BasedOn(typeof(IRequestPreProcessor<>)).WithServiceAllInterfaces().AllowMultipleMatches());
        container.Register(fromAssemblyContainingPing.BasedOn(typeof(IRequestPostProcessor<,>)).WithServiceAllInterfaces().AllowMultipleMatches());

        container.Register(Component.For<TextWriter>().Instance(writer));
        container.Register(Component.For<INotificationPublisher>().ImplementedBy<ForeachAwaitPublisher>());

        // Pipeline
        container.Register(Component.For(typeof(IStreamPipelineBehavior<,>)).ImplementedBy(typeof(GenericStreamPipelineBehavior<,>)));
        container.Register(Component.For(typeof(IPipelineBehavior<,>)).ImplementedBy(typeof(RequestPreProcessorBehavior<,>)).NamedAutomatically("PreProcessorBehavior"));
        container.Register(Component.For(typeof(IPipelineBehavior<,>)).ImplementedBy(typeof(RequestPostProcessorBehavior<,>)).NamedAutomatically("PostProcessorBehavior"));
        container.Register(Component.For(typeof(IPipelineBehavior<,>)).ImplementedBy(typeof(GenericPipelineBehavior<,>)).NamedAutomatically("Pipeline"));
        container.Register(Component.For(typeof(IRequestPreProcessor<>)).ImplementedBy(typeof(GenericRequestPreProcessor<>)).NamedAutomatically("PreProcessor"));
        container.Register(Component.For(typeof(IRequestPostProcessor<,>)).ImplementedBy(typeof(GenericRequestPostProcessor<,>)).NamedAutomatically("PostProcessor"));
        container.Register(Component.For(typeof(IRequestPostProcessor<,>), typeof(ConstrainedRequestPostProcessor<,>)).NamedAutomatically("ConstrainedRequestPostProcessor"));
        container.Register(Component.For(typeof(INotificationHandler<>), typeof(ConstrainedPingedHandler<>)).NamedAutomatically("ConstrainedPingedHandler"));

        // Create IServiceProvider adapter for Windsor
        var serviceProvider = new WindsorServiceProvider(container);
        container.Register(Component.For<IServiceProvider>().Instance(serviceProvider));
        container.Register(Component.For<IMediator>().ImplementedBy<Mediator>());

        var mediator = container.Resolve<IMediator>();

        return mediator;
    }
}

/// <summary>
/// Simple IServiceProvider adapter for Windsor container
/// </summary>
internal class WindsorServiceProvider(IWindsorContainer container) : IServiceProvider
{
    public object? GetService(Type serviceType)
    {
        // Handle IEnumerable<T> requests
        if (serviceType.IsGenericType && serviceType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
        {
            var itemType = serviceType.GetGenericArguments()[0];
            var items = container.ResolveAll(itemType);
            var typedArray = Array.CreateInstance(itemType, items.Length);
            items.CopyTo(typedArray, 0);
            return typedArray;
        }

        return container.Kernel.HasComponent(serviceType) ? container.Resolve(serviceType) : null;
    }
}
