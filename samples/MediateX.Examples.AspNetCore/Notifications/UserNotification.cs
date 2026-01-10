namespace MediateX.Examples.AspNetCore;

public record UserNotification(int UserId, string Message) : INotification;

public class UserNotificationHandler : INotificationHandler<UserNotification>
{
    public Task Handle(UserNotification notification, CancellationToken ct)
    {
        Console.WriteLine($"[Notification] User {notification.UserId}: {notification.Message}");
        return Task.CompletedTask;
    }
}

public class UserNotificationEmailHandler : INotificationHandler<UserNotification>
{
    public Task Handle(UserNotification notification, CancellationToken ct)
    {
        Console.WriteLine($"[Email] Sending email to user {notification.UserId}: {notification.Message}");
        return Task.CompletedTask;
    }
}
