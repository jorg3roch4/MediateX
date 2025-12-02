using MediateX.Processing;
using MediateX.Core;
using MediateX.Behaviors;
using MediateX.ExceptionHandling;
using MediateX;
using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using LightInject;
using LightInject.Microsoft.DependencyInjection;
using MediateX.Examples;
using Microsoft.Extensions.DependencyInjection;

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
        ServiceContainer serviceContainer = new(ContainerOptions.Default.WithMicrosoftSettings());
        serviceContainer.Register<IMediator, Mediator>();            
        serviceContainer.RegisterInstance<TextWriter>(writer);

        serviceContainer.RegisterAssembly(typeof(Ping).GetTypeInfo().Assembly, (serviceType, implementingType) =>
            serviceType.IsConstructedGenericType &&
            (
                serviceType.GetGenericTypeDefinition() == typeof(IRequestHandler<,>) ||
                serviceType.GetGenericTypeDefinition() == typeof(INotificationHandler<>)
            ));
                    
        serviceContainer.RegisterOrdered(typeof(IPipelineBehavior<,>),
            new[]
            {
                typeof(RequestPreProcessorBehavior<,>),
                typeof(RequestPostProcessorBehavior<,>),
                typeof(GenericPipelineBehavior<,>)
            }, type => null);

            
        serviceContainer.RegisterOrdered(typeof(IRequestPostProcessor<,>),
            new[]
            {
                typeof(GenericRequestPostProcessor<,>),
                typeof(ConstrainedRequestPostProcessor<,>)
            }, type => null);
                   
        serviceContainer.Register(typeof(IRequestPreProcessor<>), typeof(GenericRequestPreProcessor<>));

        ServiceCollection services = new();
        var provider = serviceContainer.CreateServiceProvider(services);
        return provider.GetRequiredService<IMediator>(); 
    }
}