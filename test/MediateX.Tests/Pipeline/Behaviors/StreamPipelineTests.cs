using MediateX.Tests.Core;
using MediateX.Tests.TestFixtures;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Lamar;
using MediateX;
using Shouldly;
using Xunit;

namespace MediateX.Tests.Pipeline;

public class StreamPipelineTests
{
    public class Ping : IStreamRequest<Pong>
    {
        public string? Message { get; set; }
    }

    public class Pong
    {
        public string? Message { get; set; }
    }

    public class Zing : IStreamRequest<Zong>
    {
        public string? Message { get; set; }
    }

    public class Zong
    {
        public string? Message { get; set; }
    }

    public class PingHandler : IStreamRequestHandler<Ping, Pong>
    {
        private readonly TestLogger _output;

        public PingHandler(TestLogger output)
        {
            _output = output;
        }

        public async IAsyncEnumerable<Pong> Handle(Ping request, [EnumeratorCancellation]CancellationToken cancellationToken)
        {
            _output.Messages.Add("Handler");
            yield return await Task.FromResult(new Pong { Message = request.Message + " Pong" });
        }
    }

    public class ZingHandler : IStreamRequestHandler<Zing, Zong>
    {
        private readonly TestLogger _output;

        public ZingHandler(TestLogger output)
        {
            _output = output;
        }

        public async IAsyncEnumerable<Zong> Handle(Zing request, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            _output.Messages.Add("Handler");
            yield return await Task.FromResult(new Zong { Message = request.Message + " Zong" });
        }
    }

    public class OuterBehavior : IStreamPipelineBehavior<Ping, Pong>
    {
        private readonly TestLogger _output;

        public OuterBehavior(TestLogger output)
        {
            _output = output;
        }

        public async IAsyncEnumerable<Pong> Handle(Ping request, StreamHandlerDelegate<Pong> next, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            _output.Messages.Add("Outer before");
            await foreach (var result in next())
            {
                yield return result;
            }
            _output.Messages.Add("Outer after");
        }
    }

    public class InnerBehavior : IStreamPipelineBehavior<Ping, Pong>
    {
        private readonly TestLogger _output;

        public InnerBehavior(TestLogger output)
        {
            _output = output;
        }

        public async IAsyncEnumerable<Pong> Handle(Ping request, StreamHandlerDelegate<Pong> next, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            _output.Messages.Add("Inner before");
            await foreach (var result in next())
            {
                yield return result;
            }
            _output.Messages.Add("Inner after");
        }
    }

    public class InnerBehavior<TRequest, TResponse> : IStreamPipelineBehavior<TRequest, TResponse>
        where TRequest : IStreamRequest<TResponse>
    {
        private readonly TestLogger _output;

        public InnerBehavior(TestLogger output)
        {
            _output = output;
        }

        public async IAsyncEnumerable<TResponse> Handle(TRequest request, StreamHandlerDelegate<TResponse> next, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            _output.Messages.Add("Inner generic before");
            await foreach (var result in next())
            {
                yield return result;
            }
            _output.Messages.Add("Inner generic after");
        }
    }

    public class OuterBehavior<TRequest, TResponse> : IStreamPipelineBehavior<TRequest, TResponse>
        where TRequest : IStreamRequest<TResponse>
    {
        private readonly TestLogger _output;

        public OuterBehavior(TestLogger output)
        {
            _output = output;
        }

        public async IAsyncEnumerable<TResponse> Handle(TRequest request, StreamHandlerDelegate<TResponse> next, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            _output.Messages.Add("Outer generic before");
            await foreach (var result in next())
            {
                yield return result;
            }
            _output.Messages.Add("Outer generic after");
        }
    }

    public class ConstrainedBehavior<TRequest, TResponse> : IStreamPipelineBehavior<TRequest, TResponse>
        where TRequest : Ping, IStreamRequest<TResponse>
        where TResponse : Pong
    {
        private readonly TestLogger _output;

        public ConstrainedBehavior(TestLogger output)
        {
            _output = output;
        }
        public async IAsyncEnumerable<TResponse> Handle(TRequest request, StreamHandlerDelegate<TResponse> next, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            _output.Messages.Add("Constrained before");
            await foreach (var result in next())
            {
                yield return result;
            }
            _output.Messages.Add("Constrained after");
        }
    }

    [Fact]
    public async Task Should_wrap_with_behavior()
    {
        var output = new TestLogger();
        var container = new Container(cfg =>
        {
            cfg.Scan(scanner =>
            {
                scanner.AssemblyContainingType(typeof(PublishTests));
                scanner.IncludeNamespaceContainingType<Ping>();
                scanner.WithDefaultConventions();
                scanner.AddAllTypesOf(typeof(IStreamRequestHandler<,>));
            });
            cfg.For<TestLogger>().Use(output);
            cfg.For<IStreamPipelineBehavior<Ping, Pong>>().Add<OuterBehavior>();
            cfg.For<IStreamPipelineBehavior<Ping, Pong>>().Add<InnerBehavior>();
            cfg.For<IMediator>().Use<Mediator>();
        });

        var mediator = container.GetInstance<IMediator>();

        await foreach(var response in mediator.CreateStream(new Ping { Message = "Ping" }, TestContext.Current.CancellationToken).WithCancellation(TestContext.Current.CancellationToken))
        {
            response.Message.ShouldBe("Ping Pong");
        }

        output.Messages.ShouldBe(new []
        {
            "Outer before",
            "Inner before",
            "Handler",
            "Inner after",
            "Outer after"
        });
    }

    [Fact]
    public async Task Should_wrap_generics_with_behavior()
    {
        var output = new TestLogger();
        var container = new Container(cfg =>
        {
            cfg.Scan(scanner =>
            {
                scanner.AssemblyContainingType(typeof(PublishTests));
                scanner.IncludeNamespaceContainingType<Ping>();
                scanner.WithDefaultConventions();
                scanner.AddAllTypesOf(typeof(IStreamRequestHandler<,>));
            });
            cfg.For<TestLogger>().Use(output);

            cfg.For(typeof(IStreamPipelineBehavior<,>)).Add(typeof(OuterBehavior<,>));
            cfg.For(typeof(IStreamPipelineBehavior<,>)).Add(typeof(InnerBehavior<,>));

            cfg.For<IMediator>().Use<Mediator>();
        });

        var mediator = container.GetInstance<IMediator>();

        await foreach (var response in mediator.CreateStream(new Ping { Message = "Ping" }, TestContext.Current.CancellationToken).WithCancellation(TestContext.Current.CancellationToken))
        {
            response.Message.ShouldBe("Ping Pong");
        }

        output.Messages.ShouldBe(new[]
        {
            "Outer generic before",
            "Inner generic before",
            "Handler",
            "Inner generic after",
            "Outer generic after",
        });
    }

    [Fact]
    public async Task Should_handle_constrained_generics()
    {
        var output = new TestLogger();
        var container = new Container(cfg =>
        {
            cfg.Scan(scanner =>
            {
                scanner.AssemblyContainingType(typeof(PublishTests));
                scanner.IncludeNamespaceContainingType<Ping>();
                scanner.WithDefaultConventions();
                scanner.AddAllTypesOf(typeof(IStreamRequestHandler<,>));
            });
            cfg.For<TestLogger>().Use(output);

            cfg.For(typeof(IStreamPipelineBehavior<,>)).Add(typeof(OuterBehavior<,>));
            cfg.For(typeof(IStreamPipelineBehavior<,>)).Add(typeof(InnerBehavior<,>));
            cfg.For(typeof(IStreamPipelineBehavior<,>)).Add(typeof(ConstrainedBehavior<,>));

            cfg.For<IMediator>().Use<Mediator>();
        });

        var mediator = container.GetInstance<IMediator>();

        await foreach (var response in mediator.CreateStream(new Ping { Message = "Ping" }, TestContext.Current.CancellationToken).WithCancellation(TestContext.Current.CancellationToken))
        {
            response.Message.ShouldBe("Ping Pong");
        }

        output.Messages.ShouldBe(new[]
        {
            "Outer generic before",
            "Inner generic before",
            "Constrained before",
            "Handler",
            "Constrained after",
            "Inner generic after",
            "Outer generic after",
        });

        output.Messages.Clear();

        await foreach (var response in mediator.CreateStream(new Zing { Message = "Zing" }, TestContext.Current.CancellationToken).WithCancellation(TestContext.Current.CancellationToken))
        {
            response.Message.ShouldBe("Zing Zong");
        }

        output.Messages.ShouldBe(new[]
        {
            "Outer generic before",
            "Inner generic before",
            "Handler",
            "Inner generic after",
            "Outer generic after",
        });
    }

}