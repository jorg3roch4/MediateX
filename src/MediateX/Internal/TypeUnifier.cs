using System;
using System.Collections.Generic;
using System.Linq;

namespace MediateX.Internal;

/// <summary>
/// Provides type unification capabilities for matching generic type patterns with concrete types.
/// Used to support behaviors with nested generic type parameters (issue #1051).
/// </summary>
internal static class TypeUnifier
{
    /// <summary>
    /// Attempts to unify a type pattern with a concrete type, extracting type parameter bindings.
    /// This is a standard type unification algorithm used in type inference systems.
    /// </summary>
    /// <param name="pattern">Type pattern that may contain generic parameters (e.g., Result&lt;TValue&gt;)</param>
    /// <param name="concrete">Concrete type to match against (e.g., Result&lt;string&gt;)</param>
    /// <param name="bindings">Dictionary to store type parameter bindings (e.g., {TValue: string})</param>
    /// <returns>True if unification succeeds, false otherwise</returns>
    public static bool TryUnify(Type pattern, Type concrete, Dictionary<Type, Type> bindings)
    {
        // Case 1: Pattern is a generic parameter (e.g., TValue, TRequest)
        if (pattern.IsGenericParameter)
        {
            if (bindings.TryGetValue(pattern, out var existing))
            {
                // Parameter already bound - must match exactly
                return existing == concrete;
            }

            // Bind the parameter to the concrete type
            bindings[pattern] = concrete;
            return true;
        }

        // Case 2: Pattern is a constructed generic type (e.g., Result<TValue>, List<T>)
        if (pattern.IsGenericType && !pattern.IsGenericTypeDefinition)
        {
            // Concrete must also be a generic type
            if (!concrete.IsGenericType)
                return false;

            // Generic type definitions must match (e.g., Result<> == Result<>)
            if (pattern.GetGenericTypeDefinition() != concrete.GetGenericTypeDefinition())
                return false;

            var patternArgs = pattern.GetGenericArguments();
            var concreteArgs = concrete.GetGenericArguments();

            // Argument counts must match
            if (patternArgs.Length != concreteArgs.Length)
                return false;

            // Recursively unify each argument
            for (int i = 0; i < patternArgs.Length; i++)
            {
                if (!TryUnify(patternArgs[i], concreteArgs[i], bindings))
                    return false;
            }

            return true;
        }

        // Case 3: Non-generic types must match exactly
        return pattern == concrete;
    }

    /// <summary>
    /// Checks if a behavior type has nested generics in its response type that require
    /// special handling during registration.
    /// </summary>
    /// <param name="behaviorType">The open generic behavior type</param>
    /// <returns>True if the behavior has nested generics in its response type</returns>
    public static bool HasNestedGenericsInResponseType(Type behaviorType)
    {
        var pipelineInterface = behaviorType.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType &&
                i.GetGenericTypeDefinition() == typeof(IPipelineBehavior<,>));

        if (pipelineInterface == null)
            return false;

        var responseType = pipelineInterface.GetGenericArguments()[1];

        // Check if the response type is a constructed generic containing generic parameters
        // e.g., Result<TValue> where TValue is a generic parameter
        return IsConstructedGenericWithParameters(responseType);
    }

    /// <summary>
    /// Checks if a type is a constructed generic type that contains generic parameters.
    /// </summary>
    private static bool IsConstructedGenericWithParameters(Type type)
    {
        if (type.IsGenericParameter)
            return false; // Just a parameter, not a constructed generic

        if (!type.IsGenericType)
            return false; // Not generic at all

        // It's a generic type - check if any of its arguments are or contain generic parameters
        return type.GetGenericArguments().Any(arg =>
            arg.IsGenericParameter || IsConstructedGenericWithParameters(arg));
    }

    /// <summary>
    /// Gets the IPipelineBehavior interface from a behavior type.
    /// </summary>
    public static Type? GetPipelineBehaviorInterface(Type behaviorType)
    {
        return behaviorType.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType &&
                i.GetGenericTypeDefinition() == typeof(IPipelineBehavior<,>));
    }

    /// <summary>
    /// Gets the IRequest&lt;TResponse&gt; interface from a request type.
    /// </summary>
    public static Type? GetRequestInterface(Type requestType)
    {
        return requestType.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType &&
                i.GetGenericTypeDefinition() == typeof(IRequest<>));
    }

    /// <summary>
    /// Attempts to create a closed behavior registration for a specific request type.
    /// </summary>
    /// <param name="openBehaviorType">The open generic behavior type</param>
    /// <param name="requestType">The concrete request type</param>
    /// <param name="serviceType">Output: The closed IPipelineBehavior interface type</param>
    /// <param name="implementationType">Output: The closed behavior implementation type</param>
    /// <returns>True if registration can be created, false otherwise</returns>
    public static bool TryCreateClosedRegistration(
        Type openBehaviorType,
        Type requestType,
        out Type? serviceType,
        out Type? implementationType)
    {
        serviceType = null;
        implementationType = null;

        // Get the request's response type
        var requestInterface = GetRequestInterface(requestType);
        if (requestInterface == null)
            return false;

        var responseType = requestInterface.GetGenericArguments()[0];

        // Get the behavior's pipeline interface
        var behaviorInterface = GetPipelineBehaviorInterface(openBehaviorType);
        if (behaviorInterface == null)
            return false;

        var patternRequest = behaviorInterface.GetGenericArguments()[0];
        var patternResponse = behaviorInterface.GetGenericArguments()[1];

        // Attempt to unify types
        var bindings = new Dictionary<Type, Type>();

        // Unify the request type
        if (!TryUnify(patternRequest, requestType, bindings))
            return false;

        // Unify the response type
        if (!TryUnify(patternResponse, responseType, bindings))
            return false;

        // Get the behavior's generic parameters and build the type argument array
        var genericParams = openBehaviorType.GetGenericArguments();
        var typeArgs = new Type[genericParams.Length];

        for (int i = 0; i < genericParams.Length; i++)
        {
            if (!bindings.TryGetValue(genericParams[i], out var boundType))
            {
                // Parameter without binding - cannot close this type for this request
                return false;
            }
            typeArgs[i] = boundType;
        }

        // Try to create the closed generic types
        try
        {
            implementationType = openBehaviorType.MakeGenericType(typeArgs);
            serviceType = typeof(IPipelineBehavior<,>).MakeGenericType(requestType, responseType);
            return true;
        }
        catch (ArgumentException)
        {
            // Constraints not satisfied
            return false;
        }
    }
}
