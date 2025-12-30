using System;
using System.Threading;
using System.Threading.Tasks;
using MediateX;
using MediateX.Behaviors;
using MediateX.Validation;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MediateX.Tests.Pipeline.Validation;

public class ValidationBehaviorTests
{
    #region Test Types

    public record CreateUserCommand(string Name, string Email) : IRequest<int>;

    public record CreateUserResultCommand(string Name, string Email) : IResultRequest<int>;

    public class CreateUserHandler : IRequestHandler<CreateUserCommand, int>
    {
        public Task<int> Handle(CreateUserCommand request, CancellationToken cancellationToken)
            => Task.FromResult(42);
    }

    public class CreateUserResultHandler : IRequestHandler<CreateUserResultCommand, Result<int>>
    {
        public Task<Result<int>> Handle(CreateUserResultCommand request, CancellationToken cancellationToken)
            => Task.FromResult(Result<int>.Success(42));
    }

    public class CreateUserValidator : IRequestValidator<CreateUserCommand>
    {
        public ValueTask<ValidationResult> ValidateAsync(CreateUserCommand request, CancellationToken cancellationToken)
        {
            var builder = new ValidationResultBuilder();

            builder.RequireNotEmpty(request.Name, nameof(request.Name));
            builder.RequireNotEmpty(request.Email, nameof(request.Email));

            if (!string.IsNullOrEmpty(request.Email) && !request.Email.Contains('@'))
            {
                builder.AddError(nameof(request.Email), "Email must contain @", "InvalidEmail");
            }

            return ValueTask.FromResult(builder.Build());
        }
    }

    public class CreateUserResultValidator : IRequestValidator<CreateUserResultCommand>
    {
        public ValueTask<ValidationResult> ValidateAsync(CreateUserResultCommand request, CancellationToken cancellationToken)
        {
            var builder = new ValidationResultBuilder();

            builder.RequireNotEmpty(request.Name, nameof(request.Name));
            builder.RequireNotEmpty(request.Email, nameof(request.Email));

            return ValueTask.FromResult(builder.Build());
        }
    }

    public class AlwaysValidValidator<TRequest> : IRequestValidator<TRequest> where TRequest : notnull
    {
        public ValueTask<ValidationResult> ValidateAsync(TRequest request, CancellationToken cancellationToken)
            => ValueTask.FromResult(ValidationResult.Success());
    }

    public class AlwaysFailValidator<TRequest> : IRequestValidator<TRequest> where TRequest : notnull
    {
        public ValueTask<ValidationResult> ValidateAsync(TRequest request, CancellationToken cancellationToken)
            => ValueTask.FromResult(ValidationResult.Failure("Test", "Always fails"));
    }

    #endregion

    #region ValidationBehavior Tests

    [Fact]
    public async Task Should_Pass_When_No_Validators_Registered()
    {
        var services = new ServiceCollection();
        services.AddMediateX(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<CreateUserCommand>();
            cfg.AddValidationBehavior();
        });

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var result = await mediator.Send(new CreateUserCommand("John", "john@example.com"));

        Assert.Equal(42, result);
    }

    [Fact]
    public async Task Should_Pass_When_Validation_Succeeds()
    {
        var services = new ServiceCollection();
        services.AddMediateX(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<CreateUserCommand>();
            cfg.AddValidationBehavior();
            cfg.AddRequestValidator<CreateUserValidator>();
        });

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var result = await mediator.Send(new CreateUserCommand("John", "john@example.com"));

        Assert.Equal(42, result);
    }

    [Fact]
    public async Task Should_Throw_ValidationException_When_Validation_Fails()
    {
        var services = new ServiceCollection();
        services.AddMediateX(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<CreateUserCommand>();
            cfg.AddValidationBehavior();
            cfg.AddRequestValidator<CreateUserValidator>();
        });

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => mediator.Send(new CreateUserCommand("", "")));

        Assert.Equal(2, exception.Errors.Count);
        Assert.Contains(exception.Errors, e => e.PropertyName == "Name");
        Assert.Contains(exception.Errors, e => e.PropertyName == "Email");
    }

    [Fact]
    public async Task Should_Include_All_Errors_From_Multiple_Validators()
    {
        var services = new ServiceCollection();
        services.AddMediateX(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<CreateUserCommand>();
            cfg.AddValidationBehavior();
            cfg.AddRequestValidator<CreateUserValidator>();
            cfg.AddRequestValidator<IRequestValidator<CreateUserCommand>, AlwaysFailValidator<CreateUserCommand>>();
        });

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => mediator.Send(new CreateUserCommand("John", "john@example.com")));

        // Should have error from AlwaysFailValidator
        Assert.Contains(exception.Errors, e => e.ErrorMessage == "Always fails");
    }

    [Fact]
    public async Task Should_Include_Custom_Error_Code()
    {
        var services = new ServiceCollection();
        services.AddMediateX(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<CreateUserCommand>();
            cfg.AddValidationBehavior();
            cfg.AddRequestValidator<CreateUserValidator>();
        });

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => mediator.Send(new CreateUserCommand("John", "invalid-email")));

        Assert.Contains(exception.Errors, e => e.ErrorCode == "InvalidEmail");
    }

    #endregion

    #region ValidationResultBehavior Tests

    [Fact]
    public async Task ResultBehavior_Should_Return_Success_When_Valid()
    {
        var services = new ServiceCollection();
        services.AddMediateX(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<CreateUserResultCommand>();
            cfg.AddValidationResultBehavior();
            cfg.AddRequestValidator<CreateUserResultValidator>();
        });

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var result = await mediator.Send(new CreateUserResultCommand("John", "john@example.com"));

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public async Task ResultBehavior_Should_Return_Failure_When_Invalid()
    {
        var services = new ServiceCollection();
        services.AddMediateX(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<CreateUserResultCommand>();
            cfg.AddValidationResultBehavior();
            cfg.AddRequestValidator<CreateUserResultValidator>();
        });

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var result = await mediator.Send(new CreateUserResultCommand("", ""));

        Assert.True(result.IsFailure);
        Assert.Contains("validation", result.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region ValidationResult Tests

    [Fact]
    public void ValidationResult_Success_Should_Be_Valid()
    {
        var result = ValidationResult.Success();

        Assert.True(result.IsValid);
        Assert.False(result.IsInvalid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidationResult_Failure_Should_Be_Invalid()
    {
        var result = ValidationResult.Failure("Name", "Name is required");

        Assert.False(result.IsValid);
        Assert.True(result.IsInvalid);
        Assert.Single(result.Errors);
    }

    [Fact]
    public void ValidationResult_Combine_Should_Merge_Errors()
    {
        var result1 = ValidationResult.Failure("Name", "Name is required");
        var result2 = ValidationResult.Failure("Email", "Email is required");

        var combined = ValidationResult.Combine(result1, result2);

        Assert.Equal(2, combined.Errors.Count);
    }

    [Fact]
    public void ValidationResult_Combine_All_Success_Should_Be_Valid()
    {
        var result1 = ValidationResult.Success();
        var result2 = ValidationResult.Success();

        var combined = ValidationResult.Combine(result1, result2);

        Assert.True(combined.IsValid);
    }

    #endregion

    #region ValidationResultBuilder Tests

    [Fact]
    public void Builder_RequireNotEmpty_Should_Add_Error_For_Empty_String()
    {
        var result = new ValidationResultBuilder()
            .RequireNotEmpty("", "Name")
            .Build();

        Assert.True(result.IsInvalid);
        Assert.Single(result.Errors);
        Assert.Equal("Name", result.Errors[0].PropertyName);
    }

    [Fact]
    public void Builder_RequireNotEmpty_Should_Not_Add_Error_For_Valid_String()
    {
        var result = new ValidationResultBuilder()
            .RequireNotEmpty("John", "Name")
            .Build();

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Builder_AddErrorIf_Should_Add_Error_When_Condition_True()
    {
        var result = new ValidationResultBuilder()
            .AddErrorIf(true, "Age", "Must be positive")
            .Build();

        Assert.True(result.IsInvalid);
    }

    [Fact]
    public void Builder_AddErrorIf_Should_Not_Add_Error_When_Condition_False()
    {
        var result = new ValidationResultBuilder()
            .AddErrorIf(false, "Age", "Must be positive")
            .Build();

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Builder_RequireGreaterThan_Should_Validate_Correctly()
    {
        var result = new ValidationResultBuilder()
            .RequireGreaterThan(0, 0, "Age")
            .Build();

        Assert.True(result.IsInvalid);

        result = new ValidationResultBuilder()
            .RequireGreaterThan(1, 0, "Age")
            .Build();

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Builder_RequireInRange_Should_Validate_Correctly()
    {
        var result = new ValidationResultBuilder()
            .RequireInRange(5, 1, 10, "Value")
            .Build();

        Assert.True(result.IsValid);

        result = new ValidationResultBuilder()
            .RequireInRange(15, 1, 10, "Value")
            .Build();

        Assert.True(result.IsInvalid);
    }

    [Fact]
    public void Builder_RequireMaxLength_Should_Validate_Correctly()
    {
        var result = new ValidationResultBuilder()
            .RequireMaxLength("Hello", 10, "Text")
            .Build();

        Assert.True(result.IsValid);

        result = new ValidationResultBuilder()
            .RequireMaxLength("Hello World!", 5, "Text")
            .Build();

        Assert.True(result.IsInvalid);
    }

    #endregion

    #region ValidationException Tests

    [Fact]
    public void ValidationException_Should_Group_Errors_By_Property()
    {
        var errors = new[]
        {
            new ValidationError("Name", "Name is required"),
            new ValidationError("Name", "Name must be at least 2 characters"),
            new ValidationError("Email", "Email is required")
        };

        var exception = new ValidationException(errors);

        Assert.Equal(2, exception.ErrorsByProperty.Count);
        Assert.Equal(2, exception.ErrorsByProperty["Name"].Length);
        Assert.Single(exception.ErrorsByProperty["Email"]);
    }

    [Fact]
    public void ValidationException_Message_Should_Summarize_Errors()
    {
        var exception = new ValidationException("Name", "Name is required");

        Assert.Contains("Name is required", exception.Message);
    }

    #endregion

    #region RequestValidator Base Class Tests

    public class SyncValidator : RequestValidator<CreateUserCommand>
    {
        public override ValidationResult Validate(CreateUserCommand request)
        {
            return string.IsNullOrEmpty(request.Name)
                ? ValidationResult.Failure("Name", "Required")
                : ValidationResult.Success();
        }
    }

    [Fact]
    public async Task RequestValidator_Base_Should_Wrap_Sync_Validation()
    {
        var validator = new SyncValidator();

        var result = await validator.ValidateAsync(new CreateUserCommand("John", "john@example.com"));

        Assert.True(result.IsValid);

        result = await validator.ValidateAsync(new CreateUserCommand("", ""));

        Assert.True(result.IsInvalid);
    }

    #endregion
}
