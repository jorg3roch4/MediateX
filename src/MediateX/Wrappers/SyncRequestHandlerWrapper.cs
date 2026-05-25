using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using MediateX.Contracts;

namespace MediateX.Wrappers;

/// <summary>
/// Base wrapper for sync request handlers with typed response.
/// Resolves <see cref="ISyncRequestHandler{TRequest,TResponse}"/> first,
/// falls back to <see cref="IRequestHandler{TRequest,TResponse}"/> if not found.
/// </summary>
public abstract class SyncRequestHandlerWrapper<TResponse> : RequestHandlerBase
{
    public abstract Task<TResponse> Handle(IRequest<TResponse> request, IServiceProvider serviceProvider,
        CancellationToken cancellationToken);
}

/// <summary>
/// Base wrapper for sync request handlers with void response.
/// Resolves <see cref="ISyncRequestHandler{TRequest}"/> first,
/// falls back to <see cref="IRequestHandler{TRequest}"/> if not found.
/// </summary>
public abstract class SyncRequestHandlerWrapper : RequestHandlerBase
{
    public abstract Task<Unit> Handle(IRequest request, IServiceProvider serviceProvider,
        CancellationToken cancellationToken);
}

/// <summary>
/// Implementation of <see cref="SyncRequestHandlerWrapper{TResponse}"/> that prioritizes
/// <see cref="ISyncRequestHandler{TRequest,TResponse}"/> for optimized sync paths.
/// </summary>
public class SyncRequestHandlerWrapperImpl<TRequest, TResponse> : SyncRequestHandlerWrapper<TResponse>
    where TRequest : IRequest<TResponse>
{
    public override async Task<object?> Handle(object request, IServiceProvider serviceProvider,
        CancellationToken cancellationToken) =>
        await Handle((IRequest<TResponse>)request, serviceProvider, cancellationToken).ConfigureAwait(false);

    public override Task<TResponse> Handle(IRequest<TResponse> request, IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        // Try to resolve ISyncRequestHandler first (priority)
        var syncHandler = serviceProvider.GetService<ISyncRequestHandler<TRequest, TResponse>>();
        if (syncHandler is not null)
        {
            return HandleWithSyncHandler(syncHandler, (TRequest)request, serviceProvider, cancellationToken);
        }

        // Fall back to IRequestHandler
        return HandleWithAsyncHandler((TRequest)request, serviceProvider, cancellationToken);
    }

    private Task<TResponse> HandleWithSyncHandler(
        ISyncRequestHandler<TRequest, TResponse> handler,
        TRequest request,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        ValueTask<TResponse> Handler(CancellationToken t = default) =>
            handler.Handle(request, t == default ? cancellationToken : t);

        var pipeline = serviceProvider
            .GetServices<IPipelineBehavior<TRequest, TResponse>>()
            .Reverse()
            .Aggregate(
                (SyncRequestHandlerDelegate<TResponse>)Handler,
                (next, behavior) => (t) => new ValueTask<TResponse>(
                    behavior.Handle(request, ct => next(ct).AsTask(), t == default ? cancellationToken : t)));

        return pipeline().AsTask();
    }

    private Task<TResponse> HandleWithAsyncHandler(
        TRequest request,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        Task<TResponse> Handler(CancellationToken t = default) =>
            serviceProvider.GetRequiredService<IRequestHandler<TRequest, TResponse>>()
                .Handle(request, t == default ? cancellationToken : t);

        return serviceProvider
            .GetServices<IPipelineBehavior<TRequest, TResponse>>()
            .Reverse()
            .Aggregate(
                (RequestHandlerDelegate<TResponse>)Handler,
                (next, pipeline) => (t) => pipeline.Handle(request, next, t == default ? cancellationToken : t))();
    }
}

/// <summary>
/// Implementation of <see cref="SyncRequestHandlerWrapper"/> that prioritizes
/// <see cref="ISyncRequestHandler{TRequest}"/> for optimized sync paths.
/// </summary>
public class SyncRequestHandlerWrapperImpl<TRequest> : SyncRequestHandlerWrapper
    where TRequest : IRequest
{
    public override async Task<object?> Handle(object request, IServiceProvider serviceProvider,
        CancellationToken cancellationToken) =>
        await Handle((IRequest)request, serviceProvider, cancellationToken).ConfigureAwait(false);

    public override Task<Unit> Handle(IRequest request, IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        // Try to resolve ISyncRequestHandler first (priority)
        var syncHandler = serviceProvider.GetService<ISyncRequestHandler<TRequest>>();
        if (syncHandler is not null)
        {
            return HandleWithSyncHandler(syncHandler, (TRequest)request, serviceProvider, cancellationToken);
        }

        // Fall back to IRequestHandler
        return HandleWithAsyncHandler((TRequest)request, serviceProvider, cancellationToken);
    }

    private Task<Unit> HandleWithSyncHandler(
        ISyncRequestHandler<TRequest> handler,
        TRequest request,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        async ValueTask<Unit> Handler(CancellationToken t = default)
        {
            await handler.Handle(request, t == default ? cancellationToken : t);
            return Unit.Value;
        }

        var pipeline = serviceProvider
            .GetServices<IPipelineBehavior<TRequest, Unit>>()
            .Reverse()
            .Aggregate(
                (SyncRequestHandlerDelegate<Unit>)Handler,
                (next, behavior) => (t) => new ValueTask<Unit>(
                    behavior.Handle(request, ct => next(ct).AsTask(), t == default ? cancellationToken : t)));

        return pipeline().AsTask();
    }

    private Task<Unit> HandleWithAsyncHandler(
        TRequest request,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        async Task<Unit> Handler(CancellationToken t = default)
        {
            await serviceProvider.GetRequiredService<IRequestHandler<TRequest>>()
                .Handle(request, t == default ? cancellationToken : t);
            return Unit.Value;
        }

        return serviceProvider
            .GetServices<IPipelineBehavior<TRequest, Unit>>()
            .Reverse()
            .Aggregate(
                (RequestHandlerDelegate<Unit>)Handler,
                (next, pipeline) => (t) => pipeline.Handle(request, next, t == default ? cancellationToken : t))();
    }
}
