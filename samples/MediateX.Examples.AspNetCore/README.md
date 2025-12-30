# MediateX.Examples.AspNetCore

ASP.NET Core Minimal API demonstrating MediateX v3.1.0 pipeline behaviors.

## How to Run

```bash
dotnet run --project samples/MediateX.Examples.AspNetCore
```

Then open http://localhost:5000 (or the URL shown in console).

## Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/` | GET | List all available endpoints |
| `/users` | POST | Create user (validation + Result) |
| `/users/{id}` | GET | Get user by ID (Result) |
| `/external-api` | GET | Call external API (retry demo) |
| `/slow-report?delayMs=2000` | GET | Generate report (timeout demo) |

## Features Demonstrated

### Result&lt;T&gt; Pattern
All handlers return `Result<T>` instead of throwing exceptions:

```csharp
public record CreateUserCommand(string Name, string Email) : IResultRequest<User>;

public class CreateUserHandler : IRequestHandler<CreateUserCommand, Result<User>>
{
    public Task<Result<User>> Handle(CreateUserCommand cmd, CancellationToken ct)
    {
        if (_users.Exists(u => u.Email == cmd.Email))
            return Task.FromResult(Result<User>.Failure("DuplicateEmail", "Email already exists"));

        return Task.FromResult(Result<User>.Success(new User(...)));
    }
}
```

### ValidationBehavior
Automatic validation before handler execution:

```csharp
public class CreateUserValidator : IRequestValidator<CreateUserCommand>
{
    public ValueTask<ValidationResult> ValidateAsync(CreateUserCommand cmd, CancellationToken ct)
    {
        var builder = new ValidationResultBuilder();
        if (string.IsNullOrWhiteSpace(cmd.Name))
            builder.AddError("Name", "Name is required");
        return ValueTask.FromResult(builder.Build());
    }
}
```

### RetryBehavior
Automatic retries with exponential backoff. Call `/external-api` to see it in action - the handler fails twice before succeeding.

### TimeoutBehavior
Request timeout enforcement. Call `/slow-report?delayMs=6000` to trigger a timeout (limit is 5 seconds).

```csharp
public record GenerateSlowReportQuery(int DelayMs) : IResultRequest<string>, IHasTimeout
{
    public TimeSpan? Timeout => TimeSpan.FromSeconds(5);
}
```

## Key Files

| File | Description |
|------|-------------|
| `Program.cs` | Minimal API setup with full v3.1.0 pipeline |
| `Commands/CreateUserCommand.cs` | Command with Result pattern |
| `Queries/GetUserQuery.cs` | Query with Result pattern |
| `Queries/CallExternalApiQuery.cs` | Retry behavior demo |
| `Queries/GenerateSlowReportQuery.cs` | Timeout behavior demo |
| `Validators/CreateUserValidator.cs` | Request validator |

## Pipeline Configuration

```csharp
builder.Services.AddMediateX(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();

    // Recommended order: timeout -> retry -> logging -> validation
    cfg.AddTimeoutBehavior(opt => opt.DefaultTimeout = TimeSpan.FromSeconds(30));
    cfg.AddRetryResultBehavior(opt =>
    {
        opt.MaxRetryAttempts = 3;
        opt.UseExponentialBackoff = true;
    });
    cfg.AddLoggingBehavior(opt => opt.SlowRequestThresholdMs = 500);
    cfg.AddValidationResultBehavior();
    cfg.AddRequestValidator<CreateUserValidator>();
});
```

## Testing with curl

```bash
# Create user (success)
curl -X POST http://localhost:5000/users \
  -H "Content-Type: application/json" \
  -d '{"name":"John","email":"john@example.com"}'

# Create user (validation error)
curl -X POST http://localhost:5000/users \
  -H "Content-Type: application/json" \
  -d '{"name":"","email":"invalid"}'

# Get user
curl http://localhost:5000/users/1

# Get user (not found)
curl http://localhost:5000/users/999

# External API (retry demo - watch logs)
curl http://localhost:5000/external-api

# Slow report (success - 2 seconds)
curl http://localhost:5000/slow-report?delayMs=2000

# Slow report (timeout - 6 seconds exceeds 5s limit)
curl http://localhost:5000/slow-report?delayMs=6000
```
