using System.Threading;
using System.Threading.Tasks;
using MediateX;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

// IMPORTANT: This file intentionally does NOT use "using MediateX.Contracts;"
// to verify that users with their own Result<T> type can use MediateX without conflicts.

namespace MediateX.Tests.Contracts;

/// <summary>
/// These tests verify that users who have their own Result&lt;T&gt; type
/// can use MediateX without naming conflicts.
///
/// This is a regression test for the namespace conflict issue fixed in v3.1.1.
/// See: https://github.com/jorg3roch4/MediateX/issues/XXX
/// </summary>
public class UserDefinedResultTests
{
    #region User-Defined Result Types (simulating what a user might have)

    /// <summary>
    /// A user-defined Result type that should NOT conflict with MediateX.Results.Result.
    /// </summary>
    public readonly struct Result<T>
    {
        public bool IsSuccess { get; }
        public T? Value { get; }
        public string? ErrorMessage { get; }

        private Result(bool isSuccess, T? value, string? errorMessage)
        {
            IsSuccess = isSuccess;
            Value = value;
            ErrorMessage = errorMessage;
        }

        public static Result<T> Success(T value) => new(true, value, null);
        public static Result<T> Failure(string error) => new(false, default, error);
    }

    /// <summary>
    /// A user-defined non-generic Result type.
    /// </summary>
    public readonly struct Result
    {
        public bool IsSuccess { get; }
        public string? ErrorMessage { get; }

        private Result(bool isSuccess, string? errorMessage)
        {
            IsSuccess = isSuccess;
            ErrorMessage = errorMessage;
        }

        public static Result Success() => new(true, null);
        public static Result Failure(string error) => new(false, error);
    }

    #endregion

    #region Request/Handler using User-Defined Result

    public record GetProductQuery(int Id) : IRequest<Result<ProductDto>>;

    public record ProductDto(int Id, string Name);

    public class GetProductHandler : IRequestHandler<GetProductQuery, Result<ProductDto>>
    {
        public Task<Result<ProductDto>> Handle(GetProductQuery request, CancellationToken ct)
        {
            if (request.Id <= 0)
                return Task.FromResult(Result<ProductDto>.Failure("Invalid product ID"));

            var product = new ProductDto(request.Id, $"Product {request.Id}");
            return Task.FromResult(Result<ProductDto>.Success(product));
        }
    }

    public record DeleteProductCommand(int Id) : IRequest<Result>;

    public class DeleteProductHandler : IRequestHandler<DeleteProductCommand, Result>
    {
        public Task<Result> Handle(DeleteProductCommand request, CancellationToken ct)
        {
            if (request.Id <= 0)
                return Task.FromResult(Result.Failure("Invalid product ID"));

            return Task.FromResult(Result.Success());
        }
    }

    #endregion

    #region Tests

    [Fact]
    public async Task User_defined_Result_should_work_without_namespace_conflict()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMediateX(cfg => cfg.RegisterServicesFromAssemblyContaining<GetProductHandler>());
        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        // Act
        var result = await mediator.Send(new GetProductQuery(42));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Id.ShouldBe(42);
        result.Value.Name.ShouldBe("Product 42");
    }

    [Fact]
    public async Task User_defined_Result_failure_should_work_without_namespace_conflict()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMediateX(cfg => cfg.RegisterServicesFromAssemblyContaining<GetProductHandler>());
        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        // Act
        var result = await mediator.Send(new GetProductQuery(-1));

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessage.ShouldBe("Invalid product ID");
    }

    [Fact]
    public async Task User_defined_void_Result_should_work_without_namespace_conflict()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMediateX(cfg => cfg.RegisterServicesFromAssemblyContaining<DeleteProductHandler>());
        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        // Act
        var result = await mediator.Send(new DeleteProductCommand(1));

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task User_defined_void_Result_failure_should_work_without_namespace_conflict()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMediateX(cfg => cfg.RegisterServicesFromAssemblyContaining<DeleteProductHandler>());
        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        // Act
        var result = await mediator.Send(new DeleteProductCommand(-1));

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessage.ShouldBe("Invalid product ID");
    }

    #endregion
}
