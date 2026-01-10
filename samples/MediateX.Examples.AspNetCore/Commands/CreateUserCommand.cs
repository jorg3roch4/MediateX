namespace MediateX.Examples.AspNetCore;

public record CreateUserCommand(string Name, string Email) : IRequest<User>;

public class CreateUserHandler : IRequestHandler<CreateUserCommand, User>
{
    private static int _nextId = 1;
    private static readonly List<User> _users = [];

    public Task<User> Handle(CreateUserCommand cmd, CancellationToken ct)
    {
        // Basic validation
        if (string.IsNullOrWhiteSpace(cmd.Name))
            throw new ArgumentException("Name is required", nameof(cmd));

        if (string.IsNullOrWhiteSpace(cmd.Email))
            throw new ArgumentException("Email is required", nameof(cmd));

        // Check for duplicate email
        if (_users.Exists(u => u.Email.Equals(cmd.Email, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Email '{cmd.Email}' already exists");
        }

        var user = new User(_nextId++, cmd.Name, cmd.Email);
        _users.Add(user);

        return Task.FromResult(user);
    }
}
