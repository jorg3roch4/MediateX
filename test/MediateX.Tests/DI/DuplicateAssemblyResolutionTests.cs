using Microsoft.Extensions.DependencyInjection;

using MediateX;
using Shouldly;
using System.Linq;
using System;
using Xunit;

namespace MediateX.Tests.DI;

public class DuplicateAssemblyResolutionTests
{
    private readonly IServiceProvider _provider;

    public DuplicateAssemblyResolutionTests()
    {
        ServiceCollection services = new();
        services.AddSingleton(new Logger());
        services.AddMediateX(cfg => cfg.RegisterServicesFromAssemblies(typeof(Ping).Assembly, typeof(Ping).Assembly));
        _provider = services.BuildServiceProvider();
    }

    [Fact]
    public void ShouldResolveNotificationHandlersOnlyOnce()
    {
        _provider.GetServices<INotificationHandler<Pinged>>().Count().ShouldBe(4);
    }
}