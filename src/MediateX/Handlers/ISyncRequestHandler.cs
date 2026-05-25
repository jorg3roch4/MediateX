using System.Threading;
using System.Threading.Tasks;

namespace MediateX;

/// <summary>
/// Optimized handler for requests that often complete synchronously.
/// Use this for cache lookups, validations, and in-memory operations.
/// </summary>
/// <remarks>
/// <para>ValueTask restrictions apply: await only once, don't store for later.</para>
/// <para>For general use cases, prefer <see cref="IRequestHandler{TRequest,TResponse}"/>.</para>
/// </remarks>
/// <typeparam name="TRequest">The type of request being handled</typeparam>
/// <typeparam name="TResponse">The type of response from the handler</typeparam>
public interface ISyncRequestHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <summary>
    /// Handles a request
    /// </summary>
    /// <param name="request">The request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Response from the request</returns>
    ValueTask<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Optimized handler for void requests that often complete synchronously.
/// </summary>
/// <remarks>
/// <para>ValueTask restrictions apply: await only once, don't store for later.</para>
/// <para>For general use cases, prefer <see cref="IRequestHandler{TRequest}"/>.</para>
/// </remarks>
/// <typeparam name="TRequest">The type of request being handled</typeparam>
public interface ISyncRequestHandler<in TRequest>
    where TRequest : IRequest
{
    /// <summary>
    /// Handles a request
    /// </summary>
    /// <param name="request">The request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A ValueTask representing the asynchronous operation</returns>
    ValueTask Handle(TRequest request, CancellationToken cancellationToken);
}
