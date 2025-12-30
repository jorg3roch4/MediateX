using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using MediateX.Behaviors;
using MediateX.ExceptionHandling;
using MediateX.Internal;
using MediateX.Processing;

namespace MediateX.Registration;

public static class ServiceRegistrar
{
    private static int MaxGenericTypeParameters;
    private static int MaxTypesClosing;
    private static int MaxGenericTypeRegistrations;
    private static int RegistrationTimeout;

    /// <summary>
    /// Safely retrieves all loadable defined types from an assembly.
    /// Handles ReflectionTypeLoadException that can occur with assemblies containing
    /// ByRef-like types (e.g., F# inref parameters, ref structs).
    /// </summary>
    internal static IEnumerable<TypeInfo> GetLoadableDefinedTypes(Assembly assembly)
    {
        try
        {
            return assembly.DefinedTypes;
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t != null).Select(t => t!.GetTypeInfo());
        }
    }

    /// <summary>
    /// Safely retrieves all loadable types from an assembly.
    /// Handles ReflectionTypeLoadException that can occur with assemblies containing
    /// ByRef-like types (e.g., F# inref parameters, ref structs).
    /// </summary>
    internal static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t != null)!;
        }
    }

    public static void SetGenericRequestHandlerRegistrationLimitations(ServiceConfiguration configuration)
    {
        MaxGenericTypeParameters = configuration.MaxGenericTypeParameters;
        MaxTypesClosing = configuration.MaxTypesClosing;
        MaxGenericTypeRegistrations = configuration.MaxGenericTypeRegistrations;
        RegistrationTimeout = configuration.RegistrationTimeout;
    }

    public static void AddMediateXClassesWithTimeout(IServiceCollection services, ServiceConfiguration configuration)
    {
        using(CancellationTokenSource cts = new(RegistrationTimeout))
        {
            try
            {
                AddMediateXClasses(services, configuration, cts.Token);
            }
            catch (OperationCanceledException)
            {
                throw new TimeoutException("The generic handler registration process timed out.");
            }
        }
    }

    public static void AddMediateXClasses(IServiceCollection services, ServiceConfiguration configuration, CancellationToken cancellationToken = default)
    {   

        var assembliesToScan = configuration.AssembliesToRegister.Distinct().ToArray();

        ConnectImplementationsToTypesClosing(typeof(IRequestHandler<,>), services, assembliesToScan, false, configuration, cancellationToken);
        ConnectImplementationsToTypesClosing(typeof(IRequestHandler<>), services, assembliesToScan, false, configuration, cancellationToken);
        ConnectImplementationsToTypesClosing(typeof(INotificationHandler<>), services, assembliesToScan, true, configuration);
        ConnectImplementationsToTypesClosing(typeof(IStreamRequestHandler<,>), services, assembliesToScan, false, configuration);
        ConnectImplementationsToTypesClosing(typeof(IRequestExceptionHandler<,,>), services, assembliesToScan, true, configuration);
        ConnectImplementationsToTypesClosing(typeof(IRequestExceptionAction<,>), services, assembliesToScan, true, configuration);

        if (configuration.AutoRegisterRequestProcessors)
        {
            ConnectImplementationsToTypesClosing(typeof(IRequestPreProcessor<>), services, assembliesToScan, true, configuration);
            ConnectImplementationsToTypesClosing(typeof(IRequestPostProcessor<,>), services, assembliesToScan, true, configuration);
        }

        List<Type> multiOpenInterfaces =
        [
            typeof(INotificationHandler<>),
            typeof(IRequestExceptionHandler<,,>),
            typeof(IRequestExceptionAction<,>)
        ];

        if (configuration.AutoRegisterRequestProcessors)
        {
            multiOpenInterfaces.Add(typeof(IRequestPreProcessor<>));
            multiOpenInterfaces.Add(typeof(IRequestPostProcessor<,>));
        }

        foreach (var multiOpenInterface in multiOpenInterfaces)
        {
            var arity = multiOpenInterface.GetGenericArguments().Length;

            var concretions = assembliesToScan
                .SelectMany(GetLoadableDefinedTypes)
                .Where(type => type.FindInterfacesThatClose(multiOpenInterface).Any())
                .Where(type => type.IsConcrete() && type.IsOpenGeneric())
                .Where(type => type.GetGenericArguments().Length == arity)
                .Where(configuration.TypeEvaluator)
                .ToList();

            foreach (var type in concretions)
            {
                services.AddTransient(multiOpenInterface, type);
            }
        }
    }

    private static void ConnectImplementationsToTypesClosing(Type openRequestInterface,
        IServiceCollection services,
        IEnumerable<Assembly> assembliesToScan,
        bool addIfAlreadyExists,
        ServiceConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        List<Type> concretions = [];
        List<Type> interfaces = [];
        List<Type> genericConcretions = [];
        List<Type> genericInterfaces = [];

        var types = assembliesToScan
            .SelectMany(GetLoadableDefinedTypes)
            .Where(t => !t.ContainsGenericParameters || configuration.RegisterGenericHandlers)
            .Where(t => t.IsConcrete() && t.FindInterfacesThatClose(openRequestInterface).Any())
            .Where(configuration.TypeEvaluator)
            .ToList();        

        foreach (var type in types)
        {
            var interfaceTypes = type.FindInterfacesThatClose(openRequestInterface).ToArray();

            if (!type.IsOpenGeneric())
            {
                concretions.Add(type);

                foreach (var interfaceType in interfaceTypes)
                {
                    interfaces.Fill(interfaceType);
                }
            }
            else
            {
                genericConcretions.Add(type);
                foreach (var interfaceType in interfaceTypes)
                {
                    genericInterfaces.Fill(interfaceType);
                }
            }
        }

        foreach (var @interface in interfaces)
        {
            // For types that allow multiple registrations (addIfAlreadyExists = true), like INotificationHandler,
            // use CanHandleInterface to prevent contravariance from incorrectly registering
            // handlers for derived notification types (fixes issue #1118), while still allowing
            // generic handlers (e.g., INotificationHandler<INotification>) to work correctly.
            var exactMatches = addIfAlreadyExists
                ? concretions.Where(x => CanHandleInterface(x, @interface)).ToList()
                : concretions.Where(x => x.CanBeCastTo(@interface)).ToList();

            if (addIfAlreadyExists)
            {
                foreach (var type in exactMatches)
                {
                    services.AddTransient(@interface, type);
                }
            }
            else
            {
                if (exactMatches.Count > 1)
                {
                    exactMatches.RemoveAll(m => !IsMatchingWithInterface(m, @interface));
                }

                foreach (var type in exactMatches)
                {
                    services.TryAddTransient(@interface, type);
                }
            }

            if (!@interface.IsOpenGeneric())
            {
                AddConcretionsThatCouldBeClosed(@interface, concretions, services);
            }
        }

        foreach (var @interface in genericInterfaces)
        {
            var exactMatches = genericConcretions.Where(x => x.CanBeCastTo(@interface)).ToList();
            AddAllConcretionsThatClose(@interface, exactMatches, services, assembliesToScan, cancellationToken);
        }
    }

    private static bool IsMatchingWithInterface(Type? handlerType, Type handlerInterface) =>
        (handlerType, handlerInterface) switch
        {
            (null, _) or (_, null) => false,
            _ when handlerType.IsInterface => handlerType.GenericTypeArguments.SequenceEqual(handlerInterface.GenericTypeArguments),
            _ => IsMatchingWithInterface(handlerType.GetInterface(handlerInterface.Name), handlerInterface)
        };

    private static void AddConcretionsThatCouldBeClosed(Type @interface, List<Type> concretions, IServiceCollection services)
    {
        foreach (var type in concretions
                     .Where(x => x.IsOpenGeneric() && x.CouldCloseTo(@interface)))
        {
            try
            {
                services.TryAddTransient(@interface, type.MakeGenericType(@interface.GenericTypeArguments));
            }
            catch (Exception)
            {
            }
        }
    }

    private static (Type Service, Type Implementation) GetConcreteRegistrationTypes(Type openRequestHandlerInterface, Type concreteGenericTRequest, Type openRequestHandlerImplementation)
    {
        var closingTypes = concreteGenericTRequest.GetGenericArguments();

        var concreteTResponse = concreteGenericTRequest.GetInterfaces()
            .FirstOrDefault(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IRequest<>))
            ?.GetGenericArguments()
            .FirstOrDefault();

        var typeDefinition = openRequestHandlerInterface.GetGenericTypeDefinition();

        var serviceType = concreteTResponse != null ?
            typeDefinition.MakeGenericType(concreteGenericTRequest, concreteTResponse) :
            typeDefinition.MakeGenericType(concreteGenericTRequest);

        return (serviceType, openRequestHandlerImplementation.MakeGenericType(closingTypes));
    }

    private static List<Type>? GetConcreteRequestTypes(Type openRequestHandlerInterface, Type openRequestHandlerImplementation, IEnumerable<Assembly> assembliesToScan, CancellationToken cancellationToken)
    {
        //request generic type constraints       
        var constraintsForEachParameter = openRequestHandlerImplementation
            .GetGenericArguments()
            .Select(x => x.GetGenericParameterConstraints())
            .ToList();

        var typesThatCanCloseForEachParameter = constraintsForEachParameter
            .Select(constraints => assembliesToScan
                .SelectMany(GetLoadableTypes)
                .Where(type => type.IsClass && !type.IsAbstract && constraints.All(constraint => constraint.IsAssignableFrom(type))).ToList()
            ).ToList();

        var requestType = openRequestHandlerInterface.GenericTypeArguments.First();

        if (requestType.IsGenericParameter)
            return null;

        var requestGenericTypeDefinition = requestType.GetGenericTypeDefinition();
              
        var combinations = GenerateCombinations(requestType, typesThatCanCloseForEachParameter, 0, cancellationToken);

        return combinations.Select(types => requestGenericTypeDefinition.MakeGenericType(types.ToArray())).ToList();
    }

    // Method to generate combinations recursively
    public static List<List<Type>> GenerateCombinations(Type requestType, List<List<Type>> lists, int depth = 0, CancellationToken cancellationToken = default)
    {
        if (depth == 0)
        {
            // Initial checks
            if (MaxGenericTypeParameters > 0 && lists.Count > MaxGenericTypeParameters)
                throw new ArgumentException($"Error registering the generic type: {requestType.FullName}. The number of generic type parameters exceeds the maximum allowed ({MaxGenericTypeParameters}).");

            foreach (var list in lists)
            {
                if (MaxTypesClosing > 0 && list.Count > MaxTypesClosing)
                    throw new ArgumentException($"Error registering the generic type: {requestType.FullName}. One of the generic type parameter's count of types that can close exceeds the maximum length allowed ({MaxTypesClosing}).");
            }

            // Calculate the total number of combinations
            long totalCombinations = 1;
            foreach (var list in lists)
            {
                totalCombinations *= list.Count;
                if (MaxGenericTypeParameters > 0 && totalCombinations > MaxGenericTypeRegistrations)
                    throw new ArgumentException($"Error registering the generic type: {requestType.FullName}. The total number of generic type registrations exceeds the maximum allowed ({MaxGenericTypeRegistrations}).");
            }
        }

        if (depth >= lists.Count)
            return [[]];
       
        cancellationToken.ThrowIfCancellationRequested();

        var currentList = lists[depth];
        var childCombinations = GenerateCombinations(requestType, lists, depth + 1, cancellationToken);
        List<List<Type>> combinations = [];

        foreach (var item in currentList)
        {
            foreach (var childCombination in childCombinations)
            {
                List<Type> currentCombination = [item, .. childCombination];
                combinations.Add(currentCombination);
            }
        }

        return combinations;
    }

    private static void AddAllConcretionsThatClose(Type openRequestInterface, List<Type> concretions, IServiceCollection services, IEnumerable<Assembly> assembliesToScan, CancellationToken cancellationToken)
    {
        foreach (var concretion in concretions)
        {   
            var concreteRequests = GetConcreteRequestTypes(openRequestInterface, concretion, assembliesToScan, cancellationToken);

            if (concreteRequests is null)
                continue;

            var registrationTypes = concreteRequests
                .Select(concreteRequest => GetConcreteRegistrationTypes(openRequestInterface, concreteRequest, concretion));

            foreach (var (Service, Implementation) in registrationTypes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                services.AddTransient(Service, Implementation);
            }
        }
    }

    internal static bool CouldCloseTo(this Type openConcretion, Type closedInterface)
    {
        var openInterface = closedInterface.GetGenericTypeDefinition();
        var arguments = closedInterface.GenericTypeArguments;

        var concreteArguments = openConcretion.GenericTypeArguments;
        return arguments.Length == concreteArguments.Length && openConcretion.CanBeCastTo(openInterface);
    }

    private static bool CanBeCastTo(this Type pluggedType, Type pluginType) =>
        pluggedType switch
        {
            null => false,
            _ when pluggedType == pluginType => true,
            _ => pluginType.IsAssignableFrom(pluggedType)
        };

    /// <summary>
    /// Checks if a handler type can handle the specified interface, with special handling for contravariant interfaces.
    /// For contravariant interfaces like INotificationHandler&lt;in T&gt;:
    /// - If the handler declares an interface type argument (e.g., INotificationHandler&lt;INotification&gt;),
    ///   allow polymorphic matching for any notification type
    /// - If the handler declares a class type argument (e.g., INotificationHandler&lt;E1&gt;),
    ///   only allow direct implementation to prevent incorrect matching with derived types
    /// This prevents issue #1118 where handlers for base notification classes incorrectly match derived types,
    /// while still allowing generic handlers (e.g., INotificationHandler&lt;INotification&gt;) to work correctly.
    /// </summary>
    private static bool CanHandleInterface(Type handlerType, Type interfaceType)
    {
        // If the handler directly implements the interface, always allow
        if (handlerType.GetInterfaces().Contains(interfaceType))
        {
            return true;
        }

        // Check if handler can be cast to the interface (contravariance)
        if (!handlerType.CanBeCastTo(interfaceType))
        {
            return false;
        }

        // Handler can be cast. Now check if we should allow this contravariant registration.
        // Find the handler's declared interface that makes it compatible
        if (interfaceType.IsGenericType)
        {
            var genericTypeDef = interfaceType.GetGenericTypeDefinition();

            // Find the interface that the handler actually implements
            var handlerInterface = handlerType.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == genericTypeDef);

            if (handlerInterface != null)
            {
                var handlerTypeArg = handlerInterface.GetGenericArguments()[0];

                // If the handler declares an INTERFACE type argument (like INotification),
                // allow contravariant matching - this is a "catch-all" handler
                if (handlerTypeArg.IsInterface)
                {
                    return true;
                }

                // If the handler declares a CLASS type argument (like E1),
                // don't allow contravariant matching to derived classes (like E2)
                // This prevents the duplicate invocation issue #1118
                return false;
            }
        }

        return false;
    }

    private static bool IsOpenGeneric(this Type type)
    {
        return type.IsGenericTypeDefinition || type.ContainsGenericParameters;
    }

    internal static IEnumerable<Type> FindInterfacesThatClose(this Type pluggedType, Type templateType)
    {
        return FindInterfacesThatClosesCore(pluggedType, templateType).Distinct();
    }

    private static IEnumerable<Type> FindInterfacesThatClosesCore(Type pluggedType, Type templateType)
    {
        if (pluggedType == null) yield break;

        if (!pluggedType.IsConcrete()) yield break;

        if (templateType.IsInterface)
        {
            foreach (
                var interfaceType in
                pluggedType.GetInterfaces()
                    .Where(type => type.IsGenericType && (type.GetGenericTypeDefinition() == templateType)))
            {
                yield return interfaceType;
            }
        }
        else if (pluggedType.BaseType!.IsGenericType &&
                 (pluggedType.BaseType!.GetGenericTypeDefinition() == templateType))
        {
            yield return pluggedType.BaseType!;
        }

        if (pluggedType.BaseType == typeof(object)) yield break;

        foreach (var interfaceType in FindInterfacesThatClosesCore(pluggedType.BaseType!, templateType))
        {
            yield return interfaceType;
        }
    }

    private static bool IsConcrete(this Type type)
    {
        return !type.IsAbstract && !type.IsInterface;
    }

    private static void Fill<T>(this IList<T> list, T value)
    {
        if (list.Contains(value)) return;
        list.Add(value);
    }

    public static void AddRequiredServices(IServiceCollection services, ServiceConfiguration serviceConfiguration)
    {
        // Use TryAdd, so any existing ServiceFactory/IMediator registration doesn't get overridden
        services.TryAdd(new ServiceDescriptor(typeof(IMediator), serviceConfiguration.MediatorImplementationType, serviceConfiguration.Lifetime));
        services.TryAdd(new ServiceDescriptor(typeof(ISender), sp => sp.GetRequiredService<IMediator>(), serviceConfiguration.Lifetime));
        services.TryAdd(new ServiceDescriptor(typeof(IPublisher), sp => sp.GetRequiredService<IMediator>(), serviceConfiguration.Lifetime));

        var notificationPublisherServiceDescriptor = serviceConfiguration.NotificationPublisherType != null
            ? new ServiceDescriptor(typeof(INotificationPublisher), serviceConfiguration.NotificationPublisherType, serviceConfiguration.Lifetime)
            : new ServiceDescriptor(typeof(INotificationPublisher), serviceConfiguration.NotificationPublisher);

        services.TryAdd(notificationPublisherServiceDescriptor);

        // Register pre processors, then post processors, then behaviors
        if (serviceConfiguration.RequestExceptionActionProcessorStrategy == RequestExceptionActionProcessorStrategy.ApplyForUnhandledExceptions)
        {
            RegisterBehaviorIfImplementationsExist(services, typeof(RequestExceptionActionProcessorBehavior<,>), typeof(IRequestExceptionAction<,>));
            RegisterBehaviorIfImplementationsExist(services, typeof(RequestExceptionProcessorBehavior<,>), typeof(IRequestExceptionHandler<,,>));
        }
        else
        {
            RegisterBehaviorIfImplementationsExist(services, typeof(RequestExceptionProcessorBehavior<,>), typeof(IRequestExceptionHandler<,,>));
            RegisterBehaviorIfImplementationsExist(services, typeof(RequestExceptionActionProcessorBehavior<,>), typeof(IRequestExceptionAction<,>));
        }

        if (serviceConfiguration.RequestPreProcessorsToRegister.Any())
        {
            services.TryAddEnumerable(new ServiceDescriptor(typeof(IPipelineBehavior<,>), typeof(RequestPreProcessorBehavior<,>), ServiceLifetime.Transient));
            services.TryAddEnumerable(serviceConfiguration.RequestPreProcessorsToRegister);
        }

        if (serviceConfiguration.RequestPostProcessorsToRegister.Any())
        {
            services.TryAddEnumerable(new ServiceDescriptor(typeof(IPipelineBehavior<,>), typeof(RequestPostProcessorBehavior<,>), ServiceLifetime.Transient));
            services.TryAddEnumerable(serviceConfiguration.RequestPostProcessorsToRegister);
        }

        // Register validators
        foreach (var serviceDescriptor in serviceConfiguration.RequestValidatorsToRegister)
        {
            services.TryAddEnumerable(serviceDescriptor);
        }

        // Register logging options if logging behavior is enabled
        if (serviceConfiguration.LoggingOptions is not null)
        {
            services.TryAddSingleton(serviceConfiguration.LoggingOptions);
        }

        // Register retry options if retry behavior is enabled
        if (serviceConfiguration.RetryOptions is not null)
        {
            services.TryAddSingleton(serviceConfiguration.RetryOptions);
        }

        // Register timeout options if timeout behavior is enabled
        if (serviceConfiguration.TimeoutOptions is not null)
        {
            services.TryAddSingleton(serviceConfiguration.TimeoutOptions);
        }

        foreach (var serviceDescriptor in serviceConfiguration.BehaviorsToRegister)
        {
            services.TryAddEnumerable(serviceDescriptor);
        }

        // Process behaviors with nested generics (issue #1051)
        ProcessNestedGenericBehaviors(services, serviceConfiguration);

        foreach (var serviceDescriptor in serviceConfiguration.StreamBehaviorsToRegister)
        {
            services.TryAddEnumerable(serviceDescriptor);
        }
    }

    /// <summary>
    /// Processes behaviors with nested generic response types by closing them against
    /// concrete request types found in the registered assemblies.
    /// This enables behaviors like IPipelineBehavior&lt;TRequest, Result&lt;T&gt;&gt; to work correctly.
    /// </summary>
    private static void ProcessNestedGenericBehaviors(IServiceCollection services, ServiceConfiguration serviceConfiguration)
    {
        if (serviceConfiguration.NestedGenericBehaviorsToRegister.Count == 0)
            return;

        // Get all concrete request types from registered assemblies
        var requestTypes = serviceConfiguration.AssembliesToRegister
            .SelectMany(GetLoadableTypes)
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t => t.GetInterfaces().Any(i =>
                i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>)))
            .Where(serviceConfiguration.TypeEvaluator)
            .ToList();

        foreach (var (behaviorType, lifetime) in serviceConfiguration.NestedGenericBehaviorsToRegister)
        {
            foreach (var requestType in requestTypes)
            {
                if (TypeUnifier.TryCreateClosedRegistration(behaviorType, requestType,
                    out var serviceType, out var implementationType))
                {
                    services.TryAddEnumerable(new ServiceDescriptor(serviceType!, implementationType!, lifetime));
                }
            }
        }
    }

    private static void RegisterBehaviorIfImplementationsExist(IServiceCollection services, Type behaviorType, Type subBehaviorType)
    {
        var hasAnyRegistrationsOfSubBehaviorType = services
            .Where(service => !service.IsKeyedService)
            .Select(service => service.ImplementationType)
            .OfType<Type>()
            .SelectMany(type => type.GetInterfaces())
            .Where(type => type.IsGenericType)
            .Select(type => type.GetGenericTypeDefinition())
            .Any(type => type == subBehaviorType);

        if (hasAnyRegistrationsOfSubBehaviorType)
        {
            services.TryAddEnumerable(new ServiceDescriptor(typeof(IPipelineBehavior<,>), behaviorType, ServiceLifetime.Transient));
        }
    }
}
