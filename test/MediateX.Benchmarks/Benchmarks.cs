using MediateX;
using System.IO;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;

namespace MediateX.Benchmarks
{
    [MemoryDiagnoser]
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

    /// <summary>
    /// Benchmarks comparing non-frozen vs frozen mediator performance.
    /// Run separately with --filter to compare.
    /// </summary>
    [MemoryDiagnoser]
    public class FrozenBenchmarks
    {
        private IMediator _mediator = null!;
        private readonly Ping _request = new() { Message = "Hello World" };

        [Params(false, true)]
        public bool UseFrozen { get; set; }

        [GlobalSetup]
        public void GlobalSetup()
        {
            Mediator.Reset();

            var services = new ServiceCollection();
            services.AddSingleton(TextWriter.Null);
            services.AddMediateX(cfg =>
            {
                cfg.RegisterServicesFromAssemblyContaining(typeof(Ping));
            });

            var provider = services.BuildServiceProvider();
            _mediator = provider.GetRequiredService<IMediator>();

            // Warmup the cache
            _mediator.Send(_request).Wait();

            // Optionally freeze
            if (UseFrozen)
            {
                Mediator.Freeze();
            }
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            Mediator.Reset();
        }

        [Benchmark]
        public Task Send()
        {
            return _mediator.Send(_request);
        }
    }
}
