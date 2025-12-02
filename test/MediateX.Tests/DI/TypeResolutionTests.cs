using System.Collections.Generic;

using MediateX;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System.Linq;
using System.Reflection;
using System;
using Xunit;

namespace MediateX.Tests.DI;

public class TypeResolutionTests
{
    private readonly IServiceProvider _provider;

    public TypeResolutionTests()
    {
        ServiceCollection services = new();
        services.AddSingleton(new Logger());
        services.AddMediateX(cfg => cfg.RegisterServicesFromAssemblyContaining(typeof(Ping)));
        _provider = services.BuildServiceProvider();
    }

    [Fact]
    public void ShouldResolveMediator()
    {
        _provider.GetService<IMediator>().ShouldNotBeNull();
    }

    [Fact]
    public void ShouldResolveSender()
    {
        _provider.GetService<ISender>().ShouldNotBeNull();
    }

    [Fact]
    public void ShouldResolvePublisher()
    {
        _provider.GetService<IPublisher>().ShouldNotBeNull();
    }

    [Fact]
    public void ShouldResolveRequestHandler()
    {
        _provider.GetService<IRequestHandler<Ping, Pong>>().ShouldNotBeNull();
    }

    [Fact]
    public void ShouldResolveVoidRequestHandler()
    {
        _provider.GetService<IRequestHandler<Ding>>().ShouldNotBeNull();
    }

    [Fact]
    public void ShouldResolveNotificationHandlers()
    {
        _provider.GetServices<INotificationHandler<Pinged>>().Count().ShouldBe(4);
    }

    [Fact]
    public void ShouldNotThrowWithMissingEnumerables()
    {
        Should.NotThrow(() => _provider.GetRequiredService<IEnumerable<IRequestExceptionAction<int, Exception>>>());
    }

    [Fact]
    public void ShouldResolveFirstDuplicateHandler()
    {
        _provider.GetService<IRequestHandler<DuplicateTest, string>>().ShouldNotBeNull();
        _provider.GetService<IRequestHandler<DuplicateTest, string>>()
            .ShouldBeAssignableTo<DuplicateHandler1>();
    }

    [Fact]
    public void ShouldResolveIgnoreSecondDuplicateHandler()
    {
        _provider.GetServices<IRequestHandler<DuplicateTest, string>>().Count().ShouldBe(1);
    }

    [Fact]
    public void ShouldHandleKeyedServices()
    {
        ServiceCollection services = new();
        services.AddSingleton(new Logger());
        services.AddKeyedSingleton<string>("Foo", "Foo");
        services.AddMediateX(cfg => cfg.RegisterServicesFromAssemblyContaining(typeof(Ping)));
        var serviceProvider = services.BuildServiceProvider();

        var mediator = serviceProvider.GetRequiredService<IMediator>();
        
        mediator.ShouldNotBeNull();
    }
}