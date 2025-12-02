using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using MediateX.Core;

namespace MediateX;

public interface INotificationPublisher
{
    Task Publish(IEnumerable<NotificationHandlerExecutor> handlerExecutors, INotification notification,
        CancellationToken cancellationToken);
}