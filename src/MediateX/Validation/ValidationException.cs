using System;
using System.Collections.Generic;
using System.Linq;

namespace MediateX.Validation;

/// <summary>
/// Exception thrown when request validation fails.
/// </summary>
public class ValidationException : Exception
{
    /// <summary>
    /// Creates a new validation exception with the specified errors.
    /// </summary>
    public ValidationException(IEnumerable<ValidationError> errors)
        : base(BuildErrorMessage(errors))
    {
        Errors = errors.ToArray();
    }

    /// <summary>
    /// Creates a new validation exception with the specified errors and message.
    /// </summary>
    public ValidationException(string message, IEnumerable<ValidationError> errors)
        : base(message)
    {
        Errors = errors.ToArray();
    }

    /// <summary>
    /// Creates a new validation exception with a single error.
    /// </summary>
    public ValidationException(string propertyName, string errorMessage)
        : this([new ValidationError(propertyName, errorMessage)])
    {
    }

    /// <summary>
    /// Gets the validation errors that caused this exception.
    /// </summary>
    public IReadOnlyList<ValidationError> Errors { get; }

    /// <summary>
    /// Gets the errors grouped by property name.
    /// </summary>
    public IDictionary<string, string[]> ErrorsByProperty =>
        Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.ErrorMessage).ToArray());

    private static string BuildErrorMessage(IEnumerable<ValidationError> errors)
    {
        var errorList = errors.ToList();
        return errorList.Count switch
        {
            0 => "Validation failed.",
            1 => $"Validation failed: {errorList[0]}",
            _ => $"Validation failed with {errorList.Count} errors: {string.Join("; ", errorList.Take(5))}" +
                 (errorList.Count > 5 ? $" (and {errorList.Count - 5} more)" : "")
        };
    }
}
