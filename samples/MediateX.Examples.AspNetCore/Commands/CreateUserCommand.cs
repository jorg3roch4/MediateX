using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MediateX.Examples.AspNetCore;

public record CreateUserCommand(string Name, string Email) : IResultRequest<User>;

public class CreateUserHandler : IRequestHandler<CreateUserCommand, Result<User>>
{
    private static int _nextId = 1;
    private static readonly List<User> _users = [];

    public Task<Result<User>> Handle(CreateUserCommand cmd, CancellationToken ct)
    {
        // Check for duplicate email
        if (_users.Exists(u => u.Email.Equals(cmd.Email, StringComparison.OrdinalIgnoreCase)))
        {
            return Task.FromResult(
                Result<User>.Failure("DuplicateEmail", $"Email '{cmd.Email}' already exists"));
        }

        var user = new User(_nextId++, cmd.Name, cmd.Email);
        _users.Add(user);

        return Task.FromResult(Result<User>.Success(user));
    }
}
