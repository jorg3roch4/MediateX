using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace MediateX.Tests.Contracts;

public class ResultTests
{
    #region Result<T> - Success

    [Fact]
    public void Success_Should_Create_Successful_Result()
    {
        var result = Result<int>.Success(42);

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Success_Should_Work_With_Reference_Types()
    {
        var value = new TestObject { Name = "Test" };
        var result = Result<TestObject>.Success(value);

        Assert.True(result.IsSuccess);
        Assert.Same(value, result.Value);
    }

    [Fact]
    public void Success_Should_Allow_Null_Value_For_Reference_Types()
    {
        var result = Result<string?>.Success(null);

        Assert.True(result.IsSuccess);
        Assert.Null(result.GetValueOrDefault());
    }

    [Fact]
    public void Implicit_Conversion_Should_Create_Success()
    {
        Result<int> result = 42;

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    #endregion

    #region Result<T> - Failure

    [Fact]
    public void Failure_Should_Create_Failed_Result()
    {
        var error = new Error("ERR001", "Something went wrong");
        var result = Result<int>.Failure(error);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Equal(error, result.Error);
    }

    [Fact]
    public void Failure_With_Code_And_Message_Should_Create_Failed_Result()
    {
        var result = Result<int>.Failure("ERR001", "Something went wrong");

        Assert.True(result.IsFailure);
        Assert.Equal("ERR001", result.Error.Code);
        Assert.Equal("Something went wrong", result.Error.Message);
    }

    [Fact]
    public void Implicit_Conversion_From_Error_Should_Create_Failure()
    {
        var error = new Error("ERR001", "Test");
        Result<int> result = error;

        Assert.True(result.IsFailure);
        Assert.Equal(error, result.Error);
    }

    [Fact]
    public void Accessing_Value_On_Failure_Should_Throw()
    {
        var result = Result<int>.Failure("ERR", "Error");

        var exception = Assert.Throws<InvalidOperationException>(() => result.Value);
        Assert.Contains("failed result", exception.Message);
    }

    [Fact]
    public void Accessing_Error_On_Success_Should_Throw()
    {
        var result = Result<int>.Success(42);

        Assert.Throws<InvalidOperationException>(() => result.Error);
    }

    #endregion

    #region Result<T> - GetValueOrDefault

    [Fact]
    public void GetValueOrDefault_Should_Return_Value_On_Success()
    {
        var result = Result<int>.Success(42);

        Assert.Equal(42, result.GetValueOrDefault());
        Assert.Equal(42, result.GetValueOrDefault(0));
    }

    [Fact]
    public void GetValueOrDefault_Should_Return_Default_On_Failure()
    {
        var result = Result<int>.Failure("ERR", "Error");

        Assert.Equal(0, result.GetValueOrDefault());
        Assert.Equal(99, result.GetValueOrDefault(99));
    }

    #endregion

    #region Result<T> - Match

    [Fact]
    public void Match_Should_Execute_OnSuccess_When_Successful()
    {
        var result = Result<int>.Success(42);

        var output = result.Match(
            onSuccess: v => $"Value: {v}",
            onFailure: e => $"Error: {e.Code}");

        Assert.Equal("Value: 42", output);
    }

    [Fact]
    public void Match_Should_Execute_OnFailure_When_Failed()
    {
        var result = Result<int>.Failure("ERR001", "Error");

        var output = result.Match(
            onSuccess: v => $"Value: {v}",
            onFailure: e => $"Error: {e.Code}");

        Assert.Equal("Error: ERR001", output);
    }

    #endregion

    #region Result<T> - Switch

    [Fact]
    public void Switch_Should_Execute_OnSuccess_When_Successful()
    {
        var result = Result<int>.Success(42);
        var executed = false;

        result.Switch(
            onSuccess: v => executed = true,
            onFailure: e => executed = false);

        Assert.True(executed);
    }

    [Fact]
    public void Switch_Should_Execute_OnFailure_When_Failed()
    {
        var result = Result<int>.Failure("ERR", "Error");
        var executed = false;

        result.Switch(
            onSuccess: v => executed = false,
            onFailure: e => executed = true);

        Assert.True(executed);
    }

    #endregion

    #region Result<T> - Map

    [Fact]
    public void Map_Should_Transform_Value_On_Success()
    {
        var result = Result<int>.Success(42);

        var mapped = result.Map(v => v.ToString());

        Assert.True(mapped.IsSuccess);
        Assert.Equal("42", mapped.Value);
    }

    [Fact]
    public void Map_Should_Preserve_Error_On_Failure()
    {
        var error = new Error("ERR", "Error");
        var result = Result<int>.Failure(error);

        var mapped = result.Map(v => v.ToString());

        Assert.True(mapped.IsFailure);
        Assert.Equal(error, mapped.Error);
    }

    #endregion

    #region Result<T> - Bind

    [Fact]
    public void Bind_Should_Chain_On_Success()
    {
        var result = Result<int>.Success(42);

        var bound = result.Bind(v => Result<string>.Success(v.ToString()));

        Assert.True(bound.IsSuccess);
        Assert.Equal("42", bound.Value);
    }

    [Fact]
    public void Bind_Should_Preserve_Original_Error_On_Failure()
    {
        var error = new Error("ERR1", "First error");
        var result = Result<int>.Failure(error);

        var bound = result.Bind(v => Result<string>.Success(v.ToString()));

        Assert.True(bound.IsFailure);
        Assert.Equal(error, bound.Error);
    }

    [Fact]
    public void Bind_Should_Propagate_New_Error()
    {
        var result = Result<int>.Success(42);
        var newError = new Error("ERR2", "Second error");

        var bound = result.Bind(v => Result<string>.Failure(newError));

        Assert.True(bound.IsFailure);
        Assert.Equal(newError, bound.Error);
    }

    #endregion

    #region Result<T> - Equality

    [Fact]
    public void Equal_Success_Results_Should_Be_Equal()
    {
        var result1 = Result<int>.Success(42);
        var result2 = Result<int>.Success(42);

        Assert.Equal(result1, result2);
        Assert.True(result1 == result2);
        Assert.False(result1 != result2);
    }

    [Fact]
    public void Different_Success_Values_Should_Not_Be_Equal()
    {
        var result1 = Result<int>.Success(42);
        var result2 = Result<int>.Success(99);

        Assert.NotEqual(result1, result2);
    }

    [Fact]
    public void Equal_Failure_Results_Should_Be_Equal()
    {
        var error = new Error("ERR", "Error");
        var result1 = Result<int>.Failure(error);
        var result2 = Result<int>.Failure(error);

        Assert.Equal(result1, result2);
    }

    [Fact]
    public void Success_And_Failure_Should_Not_Be_Equal()
    {
        var result1 = Result<int>.Success(42);
        var result2 = Result<int>.Failure("ERR", "Error");

        Assert.NotEqual(result1, result2);
    }

    #endregion

    #region Result<T> - ToString

    [Fact]
    public void ToString_Should_Show_Success_With_Value()
    {
        var result = Result<int>.Success(42);

        Assert.Equal("Success(42)", result.ToString());
    }

    [Fact]
    public void ToString_Should_Show_Failure_With_Error()
    {
        var result = Result<int>.Failure("ERR001", "Something failed");

        Assert.Equal("Failure([ERR001] Something failed)", result.ToString());
    }

    #endregion

    #region Result (void) - Success

    [Fact]
    public void Void_Success_Should_Create_Successful_Result()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
    }

    [Fact]
    public void Void_Success_Should_Be_Singleton()
    {
        var result1 = Result.Success();
        var result2 = Result.Success();

        Assert.Equal(result1, result2);
    }

    #endregion

    #region Result (void) - Failure

    [Fact]
    public void Void_Failure_Should_Create_Failed_Result()
    {
        var error = new Error("ERR", "Error");
        var result = Result.Failure(error);

        Assert.True(result.IsFailure);
        Assert.Equal(error, result.Error);
    }

    [Fact]
    public void Void_Implicit_Conversion_From_Error_Should_Create_Failure()
    {
        var error = new Error("ERR", "Test");
        Result result = error;

        Assert.True(result.IsFailure);
    }

    #endregion

    #region Result (void) - Match and Switch

    [Fact]
    public void Void_Match_Should_Execute_Correct_Branch()
    {
        var success = Result.Success();
        var failure = Result.Failure("ERR", "Error");

        Assert.Equal("OK", success.Match(() => "OK", e => "FAIL"));
        Assert.Equal("FAIL", failure.Match(() => "OK", e => "FAIL"));
    }

    [Fact]
    public void Void_Switch_Should_Execute_Correct_Action()
    {
        var result = Result.Success();
        var executed = false;

        result.Switch(
            onSuccess: () => executed = true,
            onFailure: e => executed = false);

        Assert.True(executed);
    }

    #endregion

    #region Error

    [Fact]
    public void Error_None_Should_Be_Empty()
    {
        Assert.Equal(string.Empty, Error.None.Code);
        Assert.Equal(string.Empty, Error.None.Message);
    }

    [Fact]
    public void Error_Factory_Methods_Should_Create_Correct_Codes()
    {
        Assert.Equal("Validation", Error.Validation("msg").Code);
        Assert.Equal("NotFound", Error.NotFound("msg").Code);
        Assert.Equal("Conflict", Error.Conflict("msg").Code);
        Assert.Equal("Unauthorized", Error.Unauthorized("msg").Code);
        Assert.Equal("Forbidden", Error.Forbidden("msg").Code);
    }

    [Fact]
    public void Error_FromException_Should_Create_Internal_Error()
    {
        var exception = new InvalidOperationException("Test exception");
        var error = Error.FromException(exception);

        Assert.Equal("Internal", error.Code);
        Assert.Equal("Test exception", error.Message);
    }

    [Fact]
    public void Error_ToString_Should_Format_Correctly()
    {
        var error = new Error("ERR001", "Something went wrong");

        Assert.Equal("[ERR001] Something went wrong", error.ToString());
    }

    #endregion

    #region ResultExtensions

    [Fact]
    public void ToResult_Should_Convert_Value_To_Success()
    {
        var result = 42.ToResult();

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void ToResult_Nullable_Reference_Should_Return_Failure_If_Null()
    {
        string? value = null;
        var error = Error.NotFound("Value not found");

        var result = value.ToResult(error);

        Assert.True(result.IsFailure);
        Assert.Equal(error, result.Error);
    }

    [Fact]
    public void ToResult_Nullable_Reference_Should_Return_Success_If_Not_Null()
    {
        string? value = "test";
        var error = Error.NotFound("Value not found");

        var result = value.ToResult(error);

        Assert.True(result.IsSuccess);
        Assert.Equal("test", result.Value);
    }

    [Fact]
    public void ToResult_Nullable_Struct_Should_Return_Failure_If_Null()
    {
        int? value = null;
        var error = Error.NotFound("Value not found");

        var result = value.ToResult(error);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void ToResult_Nullable_Struct_Should_Return_Success_If_Has_Value()
    {
        int? value = 42;
        var error = Error.NotFound("Value not found");

        var result = value.ToResult(error);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Combine_Should_Return_All_Values_On_Success()
    {
        var results = new[]
        {
            Result<int>.Success(1),
            Result<int>.Success(2),
            Result<int>.Success(3)
        };

        var combined = results.Combine();

        Assert.True(combined.IsSuccess);
        Assert.Equal([1, 2, 3], combined.Value);
    }

    [Fact]
    public void Combine_Should_Return_First_Error_On_Failure()
    {
        var error = new Error("ERR", "Error");
        var results = new[]
        {
            Result<int>.Success(1),
            Result<int>.Failure(error),
            Result<int>.Success(3)
        };

        var combined = results.Combine();

        Assert.True(combined.IsFailure);
        Assert.Equal(error, combined.Error);
    }

    [Fact]
    public void Combine_Void_Should_Succeed_If_All_Succeed()
    {
        var results = new[]
        {
            Result.Success(),
            Result.Success(),
            Result.Success()
        };

        var combined = results.Combine();

        Assert.True(combined.IsSuccess);
    }

    [Fact]
    public void Combine_Void_Should_Return_First_Error()
    {
        var error = new Error("ERR", "Error");
        var results = new[]
        {
            Result.Success(),
            Result.Failure(error),
            Result.Success()
        };

        var combined = results.Combine();

        Assert.True(combined.IsFailure);
        Assert.Equal(error, combined.Error);
    }

    #endregion

    #region IResultRequest Integration

    [Fact]
    public void IResultRequest_Should_Inherit_From_IRequest()
    {
        Assert.True(typeof(IRequest<Result<int>>).IsAssignableFrom(typeof(IResultRequest<int>)));
    }

    [Fact]
    public void IResultRequest_Void_Should_Inherit_From_IRequest()
    {
        Assert.True(typeof(IRequest<Result>).IsAssignableFrom(typeof(IResultRequest)));
    }

    #endregion

    private class TestObject
    {
        public string Name { get; set; } = string.Empty;
    }
}
