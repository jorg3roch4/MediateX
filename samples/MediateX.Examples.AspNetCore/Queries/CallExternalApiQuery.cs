using System.Threading;
using System.Threading.Tasks;

namespace MediateX.Examples.AspNetCore;

public record CallExternalApiQuery : IResultRequest<string>;

public class CallExternalApiHandler : IRequestHandler<CallExternalApiQuery, Result<string>>
{
    // Track attempts to demonstrate retry behavior
    private static int _globalAttempts;

    public Task<Result<string>> Handle(CallExternalApiQuery query, CancellationToken ct)
    {
        var attempt = Interlocked.Increment(ref _globalAttempts);

        // Fail first 2 attempts to demonstrate retry
        // RetryBehavior will automatically retry and succeed on 3rd attempt
        if (attempt % 3 != 0)
        {
            return Task.FromResult(
                Result<string>.Failure("ConnectionError", $"Attempt {attempt}: Connection to external API failed"));
        }

        return Task.FromResult(
            Result<string>.Success($"Success on attempt {attempt}: Data from external API"));
    }
}
