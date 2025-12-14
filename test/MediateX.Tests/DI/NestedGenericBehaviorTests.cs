using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Shouldly;
using Xunit;

namespace MediateX.Tests.DI;

/// <summary>
/// Tests for issue #1051: AddOpenBehavior fails with nested generic parameters.
/// When a behavior has nested generics in the response type (e.g., IPipelineBehavior&lt;TRequest, Result&lt;T&gt;&gt;),
/// the DI container cannot infer the type parameters and the behavior is never invoked.
/// </summary>
public class NestedGenericBehaviorTests
{
    // Simple wrapper type for testing nested generics
    public class Result<T>
    {
        public T? Value { get; set; }
        public bool IsSuccess { get; set; }

        public static Result<T> Success(T value) => new() { Value = value, IsSuccess = true };
    }

    // Request that returns Result<string>
    public class GetStringQuery : IRequest<Result<string>>
    {
        public string Input { get; set; } = "";
    }

    // Handler for GetStringQuery
    public class GetStringQueryHandler : IRequestHandler<GetStringQuery, Result<string>>
    {
        public Task<Result<string>> Handle(GetStringQuery request, CancellationToken cancellationToken)
        {
            return Task.FromResult(Result<string>.Success($"Processed: {request.Input}"));
        }
    }

    // Request that returns Result<int>
    public class GetIntQuery : IRequest<Result<int>>
    {
        public int Input { get; set; }
    }

    // Handler for GetIntQuery
    public class GetIntQueryHandler : IRequestHandler<GetIntQuery, Result<int>>
    {
        public Task<Result<int>> Handle(GetIntQuery request, CancellationToken cancellationToken)
        {
            return Task.FromResult(Result<int>.Success(request.Input * 2));
        }
    }

    // Behavior with nested generic in response type - THIS IS THE PROBLEM CASE
    public class ResultBehavior<TRequest, TValue> : IPipelineBehavior<TRequest, Result<TValue>>
        where TRequest : IRequest<Result<TValue>>
    {
        public static int CallCount = 0;

        public async Task<Result<TValue>> Handle(TRequest request,
            RequestHandlerDelegate<Result<TValue>> next, CancellationToken cancellationToken)
        {
            CallCount++;
            return await next();
        }
    }

    // Request that returns List<string>
    public class GetStringsQuery : IRequest<List<string>>
    {
        public int Count { get; set; }
    }

    // Handler for GetStringsQuery
    public class GetStringsQueryHandler : IRequestHandler<GetStringsQuery, List<string>>
    {
        public Task<List<string>> Handle(GetStringsQuery request, CancellationToken cancellationToken)
        {
            var result = new List<string>();
            for (int i = 0; i < request.Count; i++)
                result.Add($"Item {i}");
            return Task.FromResult(result);
        }
    }

    // Behavior for List<T> responses
    public class ListBehavior<TRequest, TItem> : IPipelineBehavior<TRequest, List<TItem>>
        where TRequest : IRequest<List<TItem>>
    {
        public static int CallCount = 0;

        public async Task<List<TItem>> Handle(TRequest request,
            RequestHandlerDelegate<List<TItem>> next, CancellationToken cancellationToken)
        {
            CallCount++;
            return await next();
        }
    }

    // Deep nesting test: Result<Dictionary<TKey, List<TValue>>>
    public class DeepNestedQuery : IRequest<Result<Dictionary<string, List<int>>>>
    {
    }

    public class DeepNestedQueryHandler : IRequestHandler<DeepNestedQuery, Result<Dictionary<string, List<int>>>>
    {
        public Task<Result<Dictionary<string, List<int>>>> Handle(DeepNestedQuery request, CancellationToken cancellationToken)
        {
            var dict = new Dictionary<string, List<int>> { ["key"] = [1, 2, 3] };
            return Task.FromResult(Result<Dictionary<string, List<int>>>.Success(dict));
        }
    }

    public class DeepNestedBehavior<TRequest, TKey, TValue>
        : IPipelineBehavior<TRequest, Result<Dictionary<TKey, List<TValue>>>>
        where TRequest : IRequest<Result<Dictionary<TKey, List<TValue>>>>
        where TKey : notnull
    {
        public static int CallCount = 0;

        public async Task<Result<Dictionary<TKey, List<TValue>>>> Handle(TRequest request,
            RequestHandlerDelegate<Result<Dictionary<TKey, List<TValue>>>> next, CancellationToken cancellationToken)
        {
            CallCount++;
            return await next();
        }
    }

    [Fact]
    public async Task AddOpenBehavior_WithNestedGeneric_Result_ShouldBeInvoked()
    {
        // Arrange
        ResultBehavior<GetStringQuery, string>.CallCount = 0;

        var services = new ServiceCollection();
        services.AddMediateX(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<NestedGenericBehaviorTests>();
            cfg.AddOpenBehavior(typeof(ResultBehavior<,>));
        });

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        // Act
        var result = await mediator.Send(new GetStringQuery { Input = "test" });

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("Processed: test");
        ResultBehavior<GetStringQuery, string>.CallCount.ShouldBe(1,
            "ResultBehavior should be invoked once for GetStringQuery");
    }

    [Fact]
    public async Task AddOpenBehavior_WithNestedGeneric_Result_ShouldWorkForMultipleRequestTypes()
    {
        // Arrange
        ResultBehavior<GetStringQuery, string>.CallCount = 0;
        ResultBehavior<GetIntQuery, int>.CallCount = 0;

        var services = new ServiceCollection();
        services.AddMediateX(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<NestedGenericBehaviorTests>();
            cfg.AddOpenBehavior(typeof(ResultBehavior<,>));
        });

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        // Act
        var stringResult = await mediator.Send(new GetStringQuery { Input = "hello" });
        var intResult = await mediator.Send(new GetIntQuery { Input = 5 });

        // Assert
        stringResult.Value.ShouldBe("Processed: hello");
        intResult.Value.ShouldBe(10);

        ResultBehavior<GetStringQuery, string>.CallCount.ShouldBe(1);
        ResultBehavior<GetIntQuery, int>.CallCount.ShouldBe(1);
    }

    [Fact]
    public async Task AddOpenBehavior_WithNestedGeneric_List_ShouldBeInvoked()
    {
        // Arrange
        ListBehavior<GetStringsQuery, string>.CallCount = 0;

        var services = new ServiceCollection();
        services.AddMediateX(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<NestedGenericBehaviorTests>();
            cfg.AddOpenBehavior(typeof(ListBehavior<,>));
        });

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        // Act
        var result = await mediator.Send(new GetStringsQuery { Count = 3 });

        // Assert
        result.Count.ShouldBe(3);
        ListBehavior<GetStringsQuery, string>.CallCount.ShouldBe(1,
            "ListBehavior should be invoked once for GetStringsQuery");
    }

    [Fact]
    public async Task AddOpenBehavior_WithDeeplyNestedGeneric_ShouldBeInvoked()
    {
        // Arrange
        DeepNestedBehavior<DeepNestedQuery, string, int>.CallCount = 0;

        var services = new ServiceCollection();
        services.AddMediateX(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<NestedGenericBehaviorTests>();
            cfg.AddOpenBehavior(typeof(DeepNestedBehavior<,,>));
        });

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        // Act
        var result = await mediator.Send(new DeepNestedQuery());

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.ShouldContainKey("key");
        DeepNestedBehavior<DeepNestedQuery, string, int>.CallCount.ShouldBe(1,
            "DeepNestedBehavior should be invoked once for DeepNestedQuery");
    }

    [Fact]
    public void AddOpenBehavior_WithNestedGeneric_ShouldRegisterClosedTypes()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMediateX(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<NestedGenericBehaviorTests>();
            cfg.AddOpenBehavior(typeof(ResultBehavior<,>));
        });

        // Act - Check registrations
        var resultBehaviorForString = services
            .Where(sd => sd.ServiceType == typeof(IPipelineBehavior<GetStringQuery, Result<string>>))
            .ToList();

        var resultBehaviorForInt = services
            .Where(sd => sd.ServiceType == typeof(IPipelineBehavior<GetIntQuery, Result<int>>))
            .ToList();

        // Assert
        resultBehaviorForString.Count.ShouldBe(1,
            "Should have one registration for IPipelineBehavior<GetStringQuery, Result<string>>");

        resultBehaviorForInt.Count.ShouldBe(1,
            "Should have one registration for IPipelineBehavior<GetIntQuery, Result<int>>");
    }
}
