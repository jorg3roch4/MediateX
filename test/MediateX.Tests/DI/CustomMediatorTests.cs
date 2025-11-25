using Microsoft.Extensions.DependencyInjection;

using MediateX;
using Shouldly;
using System.Linq;
using System;
using Xunit;

namespace MediateX.Tests.DI;

public class CustomMediatorTests
{
    private readonly IServiceProvider _provider;

    public CustomMediatorTests()
    {
        IServiceCollection services = new ServiceCollection();
        services.AddSingleton(new Logger());
        services.AddMediateX(cfg =>
        {
            cfg.MediatorImplementationType = typeof(MyCustomMediator);
            cfg.RegisterServicesFromAssemblyContaining(typeof(CustomMediatorTests));
        });
        _provider = services.BuildServiceProvider();
    }

    [Fact]
    public void ShouldResolveMediator()
    {
        _provider.GetService<IMediator>().ShouldNotBeNull();
        _provider.GetRequiredService<IMediator>().GetType().ShouldBe(typeof(MyCustomMediator));
    }

    [Fact]
    public void ShouldResolveRequestHandler()
    {
        _provider.GetService<IRequestHandler<Ping, Pong>>().ShouldNotBeNull();
    }

    [Fact]
    public void ShouldResolveNotificationHandlers()
    {
        _provider.GetServices<INotificationHandler<Pinged>>().Count().ShouldBe(4);
    }

    [Fact]
    public void Can_Call_AddMediatr_multiple_times()
    {
        IServiceCollection services = new ServiceCollection();
        services.AddSingleton(new Logger());
        services.AddMediateX(cfg =>
        {
            cfg.MediatorImplementationType = typeof(MyCustomMediator);
            cfg.RegisterServicesFromAssemblyContaining(typeof(CustomMediatorTests));
        });
            
        // Call AddMediatr again, this should NOT override our custom mediatr (With MS DI, last registration wins)
        services.AddMediateX(cfg => cfg.RegisterServicesFromAssemblyContaining(typeof(CustomMediatorTests)));

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();
        mediator.GetType().ShouldBe(typeof(MyCustomMediator));
    }
}