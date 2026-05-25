using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediateX.Wrappers;
using MediateX.Core;
using MediateX.Publishing;

namespace MediateX;

/// <summary>
/// Default mediator implementation relying on single- and multi instance delegates for resolving handlers.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="Mediator"/> class.
/// </remarks>
/// <param name="serviceProvider">Service provider. Can be a scoped or root provider</param>
/// <param name="publisher">Notification publisher. Defaults to <see cref="ForeachAwaitPublisher"/> if not specified or null.</param>
public class Mediator(IServiceProvider serviceProvider, INotificationPublisher? publisher = null) : IMediator
{
    // Warmup caches (mutable, used during application startup)
    private static readonly ConcurrentDictionary<Type, RequestHandlerBase> _requestHandlersWarmup = new();
    private static readonly ConcurrentDictionary<Type, NotificationHandlerWrapper> _notificationHandlersWarmup = new();
    private static readonly ConcurrentDictionary<Type, StreamRequestHandlerBase> _streamRequestHandlersWarmup = new();

    // Frozen caches (immutable, used after Freeze() is called for maximum performance)
    private static FrozenDictionary<Type, RequestHandlerBase>? _requestHandlersFrozen;
    private static FrozenDictionary<Type, NotificationHandlerWrapper>? _notificationHandlersFrozen;
    private static FrozenDictionary<Type, StreamRequestHandlerBase>? _streamRequestHandlersFrozen;

    // Lock for thread-safe freeze operation
    private static readonly Lock _freezeLock = new();
    private static bool _isFrozen;

    private readonly INotificationPublisher _publisher = publisher ?? new ForeachAwaitPublisher();

    /// <summary>
    /// Gets a value indicating whether the handler caches have been frozen.
    /// </summary>
    public static bool IsFrozen => _isFrozen;

    /// <summary>
    /// Freezes all handler caches for maximum lookup performance.
    /// Call this after application warmup when all handler types have been resolved at least once.
    /// After freezing, new handler types can still be resolved but won't benefit from frozen cache optimization.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method converts internal <see cref="ConcurrentDictionary{TKey,TValue}"/> caches to
    /// <see cref="FrozenDictionary{TKey,TValue}"/> which provides ~3x faster lookups using perfect hashing.
    /// </para>
    /// <para>
    /// Recommended usage:
    /// <code>
    /// var app = builder.Build();
    ///
    /// // Warmup: resolve all handler types
    /// using (var scope = app.Services.CreateScope())
    /// {
    ///     var mediator = scope.ServiceProvider.GetRequiredService&lt;IMediator&gt;();
    ///     await mediator.Send(new Ping());
    ///     await mediator.Send(new GetUser(1));
    ///     // ... resolve all request types
    /// }
    ///
    /// // Freeze for maximum performance
    /// Mediator.Freeze();
    ///
    /// app.Run();
    /// </code>
    /// </para>
    /// </remarks>
    public static void Freeze()
    {
        if (_isFrozen) return;

        lock (_freezeLock)
        {
            if (_isFrozen) return;

            _requestHandlersFrozen = _requestHandlersWarmup.ToFrozenDictionary();
            _notificationHandlersFrozen = _notificationHandlersWarmup.ToFrozenDictionary();
            _streamRequestHandlersFrozen = _streamRequestHandlersWarmup.ToFrozenDictionary();

            _isFrozen = true;
        }
    }

    /// <summary>
    /// Resets the frozen state and clears all caches. Primarily intended for testing.
    /// </summary>
    internal static void Reset()
    {
        lock (_freezeLock)
        {
            _isFrozen = false;
            _requestHandlersFrozen = null;
            _notificationHandlersFrozen = null;
            _streamRequestHandlersFrozen = null;
            _requestHandlersWarmup.Clear();
            _notificationHandlersWarmup.Clear();
            _streamRequestHandlersWarmup.Clear();
        }
    }

    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestType = request.GetType();
        var handler = (SyncRequestHandlerWrapper<TResponse>)GetOrCreateRequestHandler(requestType, static rt =>
        {
            var wrapperType = typeof(SyncRequestHandlerWrapperImpl<,>).MakeGenericType(rt, typeof(TResponse));
            var wrapper = Activator.CreateInstance(wrapperType) ?? throw new InvalidOperationException($"Could not create wrapper type for {rt}");
            return (RequestHandlerBase)wrapper;
        });

        return handler.Handle(request, serviceProvider, cancellationToken);
    }

    public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : IRequest
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestType = request.GetType();
        var handler = (SyncRequestHandlerWrapper)GetOrCreateRequestHandler(requestType, static rt =>
        {
            var wrapperType = typeof(SyncRequestHandlerWrapperImpl<>).MakeGenericType(rt);
            var wrapper = Activator.CreateInstance(wrapperType) ?? throw new InvalidOperationException($"Could not create wrapper type for {rt}");
            return (RequestHandlerBase)wrapper;
        });

        return handler.Handle(request, serviceProvider, cancellationToken);
    }

    public Task<object?> Send(object request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestType = request.GetType();
        var handler = GetOrCreateRequestHandler(requestType, static rt =>
        {
            Type wrapperType;

            var requestInterfaceType = rt.GetInterfaces().FirstOrDefault(static i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>));
            if (requestInterfaceType is null)
            {
                requestInterfaceType = rt.GetInterfaces().FirstOrDefault(static i => i == typeof(IRequest));
                if (requestInterfaceType is null)
                {
                    throw new ArgumentException($"{rt.Name} does not implement {nameof(IRequest)}");
                }

                wrapperType = typeof(SyncRequestHandlerWrapperImpl<>).MakeGenericType(rt);
            }
            else
            {
                var responseType = requestInterfaceType.GetGenericArguments()[0];
                wrapperType = typeof(SyncRequestHandlerWrapperImpl<,>).MakeGenericType(rt, responseType);
            }

            var wrapper = Activator.CreateInstance(wrapperType) ?? throw new InvalidOperationException($"Could not create wrapper for type {rt}");
            return (RequestHandlerBase)wrapper;
        });

        // call via dynamic dispatch to avoid calling through reflection for performance reasons
        return handler.Handle(request, serviceProvider, cancellationToken);
    }

    public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification
    {
        ArgumentNullException.ThrowIfNull(notification);

        return PublishNotification(notification, cancellationToken);
    }

    public Task Publish(object notification, CancellationToken cancellationToken = default) =>
        notification switch
        {
            null => throw new ArgumentNullException(nameof(notification)),
            INotification instance => PublishNotification(instance, cancellationToken),
            _ => throw new ArgumentException($"{nameof(notification)} does not implement ${nameof(INotification)}")
        };

    /// <summary>
    /// Override in a derived class to control how the tasks are awaited. By default the implementation calls the <see cref="INotificationPublisher"/>.
    /// </summary>
    /// <param name="handlerExecutors">Enumerable of tasks representing invoking each notification handler</param>
    /// <param name="notification">The notification being published</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A task representing invoking all handlers</returns>
    protected virtual Task PublishCore(IEnumerable<NotificationHandlerExecutor> handlerExecutors, INotification notification, CancellationToken cancellationToken)
        => _publisher.Publish(handlerExecutors, notification, cancellationToken);

    private Task PublishNotification(INotification notification, CancellationToken cancellationToken = default)
    {
        var notificationType = notification.GetType();
        var handler = GetOrCreateNotificationHandler(notificationType, static nt =>
        {
            var wrapperType = typeof(NotificationHandlerWrapperImpl<>).MakeGenericType(nt);
            var wrapper = Activator.CreateInstance(wrapperType) ?? throw new InvalidOperationException($"Could not create wrapper for type {nt}");
            return (NotificationHandlerWrapper)wrapper;
        });

        return handler.Handle(notification, serviceProvider, PublishCore, cancellationToken);
    }

    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestType = request.GetType();
        var streamHandler = (StreamRequestHandlerWrapper<TResponse>)GetOrCreateStreamHandler(requestType, static rt =>
        {
            var wrapperType = typeof(StreamRequestHandlerWrapperImpl<,>).MakeGenericType(rt, typeof(TResponse));
            var wrapper = Activator.CreateInstance(wrapperType) ?? throw new InvalidOperationException($"Could not create wrapper for type {rt}");
            return (StreamRequestHandlerBase)wrapper;
        });

        return streamHandler.Handle(request, serviceProvider, cancellationToken);
    }

    public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestType = request.GetType();
        var handler = GetOrCreateStreamHandler(requestType, static rt =>
        {
            var requestInterfaceType = rt.GetInterfaces().FirstOrDefault(static i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IStreamRequest<>));
            if (requestInterfaceType is null)
            {
                throw new ArgumentException($"{rt.Name} does not implement IStreamRequest<TResponse>");
            }

            var responseType = requestInterfaceType.GetGenericArguments()[0];
            var wrapperType = typeof(StreamRequestHandlerWrapperImpl<,>).MakeGenericType(rt, responseType);
            var wrapper = Activator.CreateInstance(wrapperType) ?? throw new InvalidOperationException($"Could not create wrapper for type {rt}");
            return (StreamRequestHandlerBase)wrapper;
        });

        return handler.Handle(request, serviceProvider, cancellationToken);
    }

    // Helper methods for frozen/warmup cache lookup pattern

    private static RequestHandlerBase GetOrCreateRequestHandler(Type requestType, Func<Type, RequestHandlerBase> factory)
    {
        // Fast path: check frozen cache first (after Freeze() is called)
        if (_requestHandlersFrozen is not null && _requestHandlersFrozen.TryGetValue(requestType, out var frozenHandler))
        {
            return frozenHandler;
        }

        // Slow path: use warmup cache (before Freeze() or for new types after Freeze())
        return _requestHandlersWarmup.GetOrAdd(requestType, factory);
    }

    private static NotificationHandlerWrapper GetOrCreateNotificationHandler(Type notificationType, Func<Type, NotificationHandlerWrapper> factory)
    {
        // Fast path: check frozen cache first
        if (_notificationHandlersFrozen is not null && _notificationHandlersFrozen.TryGetValue(notificationType, out var frozenHandler))
        {
            return frozenHandler;
        }

        // Slow path: use warmup cache
        return _notificationHandlersWarmup.GetOrAdd(notificationType, factory);
    }

    private static StreamRequestHandlerBase GetOrCreateStreamHandler(Type requestType, Func<Type, StreamRequestHandlerBase> factory)
    {
        // Fast path: check frozen cache first
        if (_streamRequestHandlersFrozen is not null && _streamRequestHandlersFrozen.TryGetValue(requestType, out var frozenHandler))
        {
            return frozenHandler;
        }

        // Slow path: use warmup cache
        return _streamRequestHandlersWarmup.GetOrAdd(requestType, factory);
    }
}
