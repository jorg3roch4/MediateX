namespace MediateX.Examples.AspNetCore;

public record GetUserQuery(int Id) : IRequest<User?>;

public class GetUserHandler : IRequestHandler<GetUserQuery, User?>
{
    // Simulated database
    private static readonly Dictionary<int, User> _users = new()
    {
        [1] = new User(1, "Alice", "alice@example.com"),
        [2] = new User(2, "Bob", "bob@example.com"),
        [3] = new User(3, "Charlie", "charlie@example.com")
    };

    public Task<User?> Handle(GetUserQuery query, CancellationToken ct)
    {
        _users.TryGetValue(query.Id, out var user);
        return Task.FromResult(user);
    }
}
