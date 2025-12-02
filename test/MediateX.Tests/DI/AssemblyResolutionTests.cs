using System;
using System.Linq;
using MediateX;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace MediateX.Tests.DI;

public class AssemblyResolutionTests
{
    private readonly IServiceProvider _provider;

    public AssemblyResolutionTests()
    {
        ServiceCollection services = new();
        services.AddSingleton(new Logger());
        services.AddMediateX(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(Ping).Assembly);
            cfg.RegisterGenericHandlers = true;
        });
        _provider = services.BuildServiceProvider();
    }

    [Fact]
    public void ShouldResolveMediator()
    {
        _provider.GetService<IMediator>().ShouldNotBeNull();
    }

    [Fact]
    public void ShouldResolveRequestHandler()
    {
        _provider.GetService<IRequestHandler<Ping, Pong>>().ShouldNotBeNull();
    }

    [Fact]
    public void ShouldResolveInternalHandler()
    {
        _provider.GetService<IRequestHandler<InternalPing>>().ShouldNotBeNull();
    }

    [Fact]
    public void ShouldResolveNotificationHandlers()
    {
        _provider.GetServices<INotificationHandler<Pinged>>().Count().ShouldBe(4);
    }

    [Fact]
    public void ShouldResolveStreamHandlers()
    {
        _provider.GetService<IStreamRequestHandler<StreamPing, Pong>>().ShouldNotBeNull();
    }

    [Fact]
    public void ShouldRequireAtLeastOneAssembly()
    {
        ServiceCollection services = new();

        Action registration = () => services.AddMediateX(_ => { });

        registration.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void ShouldResolveGenericVoidRequestHandler()
    {
        _provider.GetService<IRequestHandler<OpenGenericVoidRequest<ConcreteTypeArgument>>>().ShouldNotBeNull();
    }

    [Fact]
    public void ShouldResolveGenericReturnTypeRequestHandler()
    {
        _provider.GetService<IRequestHandler<OpenGenericReturnTypeRequest<ConcreteTypeArgument>, string>>().ShouldNotBeNull();
    }

    [Fact]
    public void ShouldResolveGenericPingRequestHandler()
    {
        _provider.GetService<IRequestHandler<GenericPing<Pong>, Pong>>().ShouldNotBeNull();
    }

    [Fact]
    public void ShouldResolveVoidGenericPingRequestHandler()
    {
        _provider.GetService<IRequestHandler<VoidGenericPing<Pong>>>().ShouldNotBeNull();
    }
}