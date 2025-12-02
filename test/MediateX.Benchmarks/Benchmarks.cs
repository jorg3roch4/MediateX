using MediateX;
using System.IO;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;

namespace MediateX.Benchmarks
{
    [DotTraceDiagnoser]
    public class Benchmarks
    {
        private IMediator _mediator;
        private readonly Ping _request = new() {Message = "Hello World"};
        private readonly Pinged _notification = new();

        [GlobalSetup]
        public void GlobalSetup()
        {
            ServiceCollection services = new();

            services.AddSingleton(TextWriter.Null);

            services.AddMediateX(cfg =>
            {
                cfg.RegisterServicesFromAssemblyContaining(typeof(Ping));
                cfg.AddOpenBehavior(typeof(GenericPipelineBehavior<,>));
            });

            var provider = services.BuildServiceProvider();

            _mediator = provider.GetRequiredService<IMediator>();
        }

        [Benchmark]
        public Task SendingRequests()
        {
            return _mediator.Send(_request);
        }

        [Benchmark]
        public Task PublishingNotifications()
        {
            return _mediator.Publish(_notification);
        }
    }
}
