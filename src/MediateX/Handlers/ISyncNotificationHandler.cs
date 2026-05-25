using System.Threading;
using System.Threading.Tasks;

namespace MediateX;

/// <summary>
/// Optimized handler for notifications that often complete synchronously.
/// </summary>
/// <remarks>
/// <para>ValueTask restrictions apply: await only once, don't store for later.</para>
/// <para>For general use cases, prefer <see cref="INotificationHandler{TNotification}"/>.</para>
/// </remarks>
/// <typeparam name="TNotification">The type of notification being handled</typeparam>
public interface ISyncNotificationHandler<in TNotification>
    where TNotification : INotification
{
    /// <summary>
    /// Handles a notification
    /// </summary>
    /// <param name="notification">The notification</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A ValueTask representing the asynchronous operation</returns>
    ValueTask Handle(TNotification notification, CancellationToken cancellationToken);
}
