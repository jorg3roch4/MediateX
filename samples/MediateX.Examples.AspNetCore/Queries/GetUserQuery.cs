using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MediateX.Examples.AspNetCore;

public record GetUserQuery(int Id) : IResultRequest<User>;

public class GetUserHandler : IRequestHandler<GetUserQuery, Result<User>>
{
    // Simulated database
    private static readonly Dictionary<int, User> _users = new()
    {
        [1] = new User(1, "Alice", "alice@example.com"),
        [2] = new User(2, "Bob", "bob@example.com"),
        [3] = new User(3, "Charlie", "charlie@example.com")
    };

    public Task<Result<User>> Handle(GetUserQuery query, CancellationToken ct)
    {
        if (_users.TryGetValue(query.Id, out var user))
        {
            return Task.FromResult(Result<User>.Success(user));
        }

        return Task.FromResult(
            Result<User>.Failure("NotFound", $"User with ID {query.Id} not found"));
    }
}
