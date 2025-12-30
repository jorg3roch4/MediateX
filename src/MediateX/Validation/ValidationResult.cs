using System;
using System.Collections.Generic;
using System.Linq;

namespace MediateX.Validation;

/// <summary>
/// Represents the result of a validation operation.
/// </summary>
public sealed class ValidationResult
{
    private static readonly ValidationResult _success = new([]);

    private ValidationResult(IReadOnlyList<ValidationError> errors)
    {
        Errors = errors;
    }

    /// <summary>
    /// Gets the collection of validation errors.
    /// </summary>
    public IReadOnlyList<ValidationError> Errors { get; }

    /// <summary>
    /// Gets whether the validation passed (no errors).
    /// </summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>
    /// Gets whether the validation failed (has errors).
    /// </summary>
    public bool IsInvalid => Errors.Count > 0;

    /// <summary>
    /// Creates a successful validation result with no errors.
    /// </summary>
    public static ValidationResult Success() => _success;

    /// <summary>
    /// Creates a failed validation result with the specified errors.
    /// </summary>
    public static ValidationResult Failure(params ValidationError[] errors) => new(errors);

    /// <summary>
    /// Creates a failed validation result with the specified errors.
    /// </summary>
    public static ValidationResult Failure(IEnumerable<ValidationError> errors) => new(errors.ToArray());

    /// <summary>
    /// Creates a failed validation result with a single error.
    /// </summary>
    public static ValidationResult Failure(string propertyName, string errorMessage)
        => new([new ValidationError(propertyName, errorMessage)]);

    /// <summary>
    /// Combines multiple validation results into one.
    /// </summary>
    public static ValidationResult Combine(params ValidationResult[] results)
    {
        var errors = results.SelectMany(r => r.Errors).ToArray();
        return errors.Length == 0 ? _success : new ValidationResult(errors);
    }

    /// <summary>
    /// Combines multiple validation results into one.
    /// </summary>
    public static ValidationResult Combine(IEnumerable<ValidationResult> results)
    {
        var errors = results.SelectMany(r => r.Errors).ToArray();
        return errors.Length == 0 ? _success : new ValidationResult(errors);
    }

    public override string ToString() =>
        IsValid ? "Valid" : $"Invalid: {string.Join(", ", Errors)}";
}

/// <summary>
/// Represents a single validation error.
/// </summary>
/// <param name="PropertyName">The name of the property that failed validation.</param>
/// <param name="ErrorMessage">A description of the validation error.</param>
/// <param name="ErrorCode">An optional error code for programmatic handling.</param>
/// <param name="AttemptedValue">The value that was attempted (optional).</param>
public sealed record ValidationError(
    string PropertyName,
    string ErrorMessage,
    string? ErrorCode = null,
    object? AttemptedValue = null)
{
    public override string ToString() =>
        string.IsNullOrEmpty(PropertyName)
            ? ErrorMessage
            : $"{PropertyName}: {ErrorMessage}";
}

/// <summary>
/// Provides a fluent API for building validation results.
/// </summary>
public sealed class ValidationResultBuilder
{
    private readonly List<ValidationError> _errors = [];

    /// <summary>
    /// Adds an error if the condition is true.
    /// </summary>
    public ValidationResultBuilder AddErrorIf(bool condition, string propertyName, string errorMessage, string? errorCode = null)
    {
        if (condition)
            _errors.Add(new ValidationError(propertyName, errorMessage, errorCode));
        return this;
    }

    /// <summary>
    /// Adds an error if the value is null or empty.
    /// </summary>
    public ValidationResultBuilder RequireNotEmpty(string? value, string propertyName, string? errorMessage = null)
    {
        if (string.IsNullOrWhiteSpace(value))
            _errors.Add(new ValidationError(propertyName, errorMessage ?? $"{propertyName} is required.", "Required"));
        return this;
    }

    /// <summary>
    /// Adds an error if the value is null.
    /// </summary>
    public ValidationResultBuilder RequireNotNull<T>(T? value, string propertyName, string? errorMessage = null) where T : class
    {
        if (value is null)
            _errors.Add(new ValidationError(propertyName, errorMessage ?? $"{propertyName} is required.", "Required"));
        return this;
    }

    /// <summary>
    /// Adds an error if the value is less than or equal to the minimum.
    /// </summary>
    public ValidationResultBuilder RequireGreaterThan<T>(T value, T minimum, string propertyName, string? errorMessage = null)
        where T : IComparable<T>
    {
        if (value.CompareTo(minimum) <= 0)
            _errors.Add(new ValidationError(propertyName, errorMessage ?? $"{propertyName} must be greater than {minimum}.", "Range", value));
        return this;
    }

    /// <summary>
    /// Adds an error if the value is outside the specified range.
    /// </summary>
    public ValidationResultBuilder RequireInRange<T>(T value, T min, T max, string propertyName, string? errorMessage = null)
        where T : IComparable<T>
    {
        if (value.CompareTo(min) < 0 || value.CompareTo(max) > 0)
            _errors.Add(new ValidationError(propertyName, errorMessage ?? $"{propertyName} must be between {min} and {max}.", "Range", value));
        return this;
    }

    /// <summary>
    /// Adds an error if the string length exceeds the maximum.
    /// </summary>
    public ValidationResultBuilder RequireMaxLength(string? value, int maxLength, string propertyName, string? errorMessage = null)
    {
        if (value is not null && value.Length > maxLength)
            _errors.Add(new ValidationError(propertyName, errorMessage ?? $"{propertyName} must not exceed {maxLength} characters.", "MaxLength", value));
        return this;
    }

    /// <summary>
    /// Adds an error directly.
    /// </summary>
    public ValidationResultBuilder AddError(ValidationError error)
    {
        _errors.Add(error);
        return this;
    }

    /// <summary>
    /// Adds an error directly.
    /// </summary>
    public ValidationResultBuilder AddError(string propertyName, string errorMessage, string? errorCode = null)
    {
        _errors.Add(new ValidationError(propertyName, errorMessage, errorCode));
        return this;
    }

    /// <summary>
    /// Builds the validation result.
    /// </summary>
    public ValidationResult Build() =>
        _errors.Count == 0 ? ValidationResult.Success() : ValidationResult.Failure(_errors);
}
