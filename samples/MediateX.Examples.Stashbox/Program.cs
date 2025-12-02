using Stashbox;
using MediateX;
using Stashbox.Configuration;
using System;
using System.IO;
using System.Threading.Tasks;
using MediateX.Examples;
using Microsoft.Extensions.DependencyInjection;

namespace MediateX.Examples.Stashbox;

class Program
{
    static Task Main()
    {
        WrappingWriter writer = new(Console.Out);
        var mediator = BuildMediator(writer);
        return Runner.Run(mediator, writer, "Stashbox", testStreams: true);
    }

    private static IMediator BuildMediator(WrappingWriter writer)
    {
        var container = new StashboxContainer()
            .RegisterInstance<TextWriter>(writer)
            .RegisterAssemblies(new[] { typeof(Mediator).Assembly, typeof(Ping).Assembly },
                serviceTypeSelector: Rules.ServiceRegistrationFilters.Interfaces, registerSelf: false);

        return container.GetRequiredService<IMediator>();
    }
}