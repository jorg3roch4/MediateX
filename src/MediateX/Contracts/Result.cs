using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace MediateX.Contracts;

/// <summary>
/// Represents the result of an operation that can succeed or fail.
/// </summary>
/// <typeparam name="T">The type of the value on success.</typeparam>
public readonly struct Result<T> : IEquatable<Result<T>>
{
    private readonly T? _value;
    private readonly Error? _error;

    private Result(T value)
    {
        _value = value;
        _error = null;
        IsSuccess = true;
    }

    private Result(Error error)
    {
        _value = default;
        _error = error;
        IsSuccess = false;
    }

    /// <summary>
    /// Gets whether the result represents a successful operation.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess { get; }

    /// <summary>
    /// Gets whether the result represents a failed operation.
    /// </summary>
    [MemberNotNullWhen(false, nameof(Value))]
    [MemberNotNullWhen(true, nameof(Error))]
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// Gets the value if the operation succeeded.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when accessing Value on a failed result.</exception>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Cannot access Value on a failed result. Error: {_error}");

    /// <summary>
    /// Gets the error if the operation failed.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when accessing Error on a successful result.</exception>
    public Error Error => IsFailure
        ? _error!
        : throw new InvalidOperationException("Cannot access Error on a successful result.");

    /// <summary>
    /// Creates a successful result with the specified value.
    /// </summary>
    public static Result<T> Success(T value) => new(value);

    /// <summary>
    /// Creates a failed result with the specified error.
    /// </summary>
    public static Result<T> Failure(Error error) => new(error);

    /// <summary>
    /// Creates a failed result with an error code and message.
    /// </summary>
    public static Result<T> Failure(string code, string message) => new(new Error(code, message));

    /// <summary>
    /// Implicitly converts a value to a successful result.
    /// </summary>
    public static implicit operator Result<T>(T value) => Success(value);

    /// <summary>
    /// Implicitly converts an error to a failed result.
    /// </summary>
    public static implicit operator Result<T>(Error error) => Failure(error);

    /// <summary>
    /// Gets the value if successful, or the default value if failed.
    /// </summary>
    public T? GetValueOrDefault() => _value;

    /// <summary>
    /// Gets the value if successful, or the specified fallback value if failed.
    /// </summary>
    public T GetValueOrDefault(T fallback) => IsSuccess ? _value! : fallback;

    /// <summary>
    /// Executes one of the provided functions based on the result state.
    /// </summary>
    public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<Error, TResult> onFailure) =>
        IsSuccess ? onSuccess(_value!) : onFailure(_error!);

    /// <summary>
    /// Executes one of the provided actions based on the result state.
    /// </summary>
    public void Switch(Action<T> onSuccess, Action<Error> onFailure)
    {
        if (IsSuccess)
            onSuccess(_value!);
        else
            onFailure(_error!);
    }

    /// <summary>
    /// Transforms the value if successful, preserving failures.
    /// </summary>
    public Result<TNew> Map<TNew>(Func<T, TNew> mapper) =>
        IsSuccess ? Result<TNew>.Success(mapper(_value!)) : Result<TNew>.Failure(_error!);

    /// <summary>
    /// Chains another result-producing operation if successful.
    /// </summary>
    public Result<TNew> Bind<TNew>(Func<T, Result<TNew>> binder) =>
        IsSuccess ? binder(_value!) : Result<TNew>.Failure(_error!);

    public bool Equals(Result<T> other) =>
        IsSuccess == other.IsSuccess &&
        EqualityComparer<T?>.Default.Equals(_value, other._value) &&
        Equals(_error, other._error);

    public override bool Equals(object? obj) => obj is Result<T> other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(IsSuccess, _value, _error);

    public static bool operator ==(Result<T> left, Result<T> right) => left.Equals(right);

    public static bool operator !=(Result<T> left, Result<T> right) => !left.Equals(right);

    public override string ToString() =>
        IsSuccess ? $"Success({_value})" : $"Failure({_error})";
}

/// <summary>
/// Represents the result of an operation that can succeed or fail, without a value.
/// </summary>
public readonly struct Result : IEquatable<Result>
{
    private readonly Error? _error;

    private Result(bool isSuccess, Error? error)
    {
        IsSuccess = isSuccess;
        _error = error;
    }

    /// <summary>
    /// Gets whether the result represents a successful operation.
    /// </summary>
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess { get; }

    /// <summary>
    /// Gets whether the result represents a failed operation.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Error))]
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// Gets the error if the operation failed.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when accessing Error on a successful result.</exception>
    public Error Error => IsFailure
        ? _error!
        : throw new InvalidOperationException("Cannot access Error on a successful result.");

    private static readonly Result _success = new(true, null);

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static Result Success() => _success;

    /// <summary>
    /// Creates a failed result with the specified error.
    /// </summary>
    public static Result Failure(Error error) => new(false, error);

    /// <summary>
    /// Creates a failed result with an error code and message.
    /// </summary>
    public static Result Failure(string code, string message) => new(false, new Error(code, message));

    /// <summary>
    /// Implicitly converts an error to a failed result.
    /// </summary>
    public static implicit operator Result(Error error) => Failure(error);

    /// <summary>
    /// Executes one of the provided functions based on the result state.
    /// </summary>
    public TResult Match<TResult>(Func<TResult> onSuccess, Func<Error, TResult> onFailure) =>
        IsSuccess ? onSuccess() : onFailure(_error!);

    /// <summary>
    /// Executes one of the provided actions based on the result state.
    /// </summary>
    public void Switch(Action onSuccess, Action<Error> onFailure)
    {
        if (IsSuccess)
            onSuccess();
        else
            onFailure(_error!);
    }

    public bool Equals(Result other) =>
        IsSuccess == other.IsSuccess && Equals(_error, other._error);

    public override bool Equals(object? obj) => obj is Result other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(IsSuccess, _error);

    public static bool operator ==(Result left, Result right) => left.Equals(right);

    public static bool operator !=(Result left, Result right) => !left.Equals(right);

    public override string ToString() =>
        IsSuccess ? "Success" : $"Failure({_error})";
}

/// <summary>
/// Represents an error with a code and message.
/// </summary>
/// <param name="Code">A unique error code for categorization.</param>
/// <param name="Message">A human-readable error message.</param>
public sealed record Error(string Code, string Message)
{
    /// <summary>
    /// Represents no error (null object pattern).
    /// </summary>
    public static readonly Error None = new(string.Empty, string.Empty);

    /// <summary>
    /// Creates a validation error.
    /// </summary>
    public static Error Validation(string message) => new("Validation", message);

    /// <summary>
    /// Creates a not found error.
    /// </summary>
    public static Error NotFound(string message) => new("NotFound", message);

    /// <summary>
    /// Creates a conflict error.
    /// </summary>
    public static Error Conflict(string message) => new("Conflict", message);

    /// <summary>
    /// Creates an unauthorized error.
    /// </summary>
    public static Error Unauthorized(string message) => new("Unauthorized", message);

    /// <summary>
    /// Creates a forbidden error.
    /// </summary>
    public static Error Forbidden(string message) => new("Forbidden", message);

    /// <summary>
    /// Creates an internal error from an exception.
    /// </summary>
    public static Error FromException(Exception exception) =>
        new("Internal", exception.Message);

    public override string ToString() => $"[{Code}] {Message}";
}

/// <summary>
/// Provides extension methods for Result types.
/// </summary>
public static class ResultExtensions
{
    /// <summary>
    /// Converts a value to a successful result.
    /// </summary>
    public static Result<T> ToResult<T>(this T value) => Result<T>.Success(value);

    /// <summary>
    /// Converts a nullable value to a result, failing if null.
    /// </summary>
    public static Result<T> ToResult<T>(this T? value, Error errorIfNull) where T : class =>
        value is not null ? Result<T>.Success(value) : Result<T>.Failure(errorIfNull);

    /// <summary>
    /// Converts a nullable value to a result, failing if null.
    /// </summary>
    public static Result<T> ToResult<T>(this T? value, Error errorIfNull) where T : struct =>
        value.HasValue ? Result<T>.Success(value.Value) : Result<T>.Failure(errorIfNull);

    /// <summary>
    /// Combines multiple results into a single result containing all values.
    /// </summary>
    public static Result<IReadOnlyList<T>> Combine<T>(this IEnumerable<Result<T>> results)
    {
        var values = new List<T>();
        foreach (var result in results)
        {
            if (result.IsFailure)
                return Result<IReadOnlyList<T>>.Failure(result.Error);
            values.Add(result.Value);
        }
        return Result<IReadOnlyList<T>>.Success(values);
    }

    /// <summary>
    /// Combines multiple void results into a single result.
    /// </summary>
    public static Result Combine(this IEnumerable<Result> results)
    {
        foreach (var result in results)
        {
            if (result.IsFailure)
                return Result.Failure(result.Error);
        }
        return Result.Success();
    }
}
