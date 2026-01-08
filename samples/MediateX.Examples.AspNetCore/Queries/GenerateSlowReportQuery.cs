using System;
using System.Threading;
using System.Threading.Tasks;
using MediateX.Behaviors;
using MediateX.Contracts;

namespace MediateX.Examples.AspNetCore;

public record GenerateSlowReportQuery(int DelayMs) : IResultRequest<string>, IHasTimeout
{
    // Custom timeout: 5 seconds for this specific query
    // If DelayMs exceeds this, TimeoutBehavior will return a failure
    public TimeSpan? Timeout => TimeSpan.FromSeconds(5);
}

public class GenerateSlowReportHandler : IRequestHandler<GenerateSlowReportQuery, Result<string>>
{
    public async Task<Result<string>> Handle(GenerateSlowReportQuery query, CancellationToken ct)
    {
        // Simulate slow operation
        // If query.DelayMs > 5000, the TimeoutBehavior will cancel this and return failure
        await Task.Delay(query.DelayMs, ct);

        return Result<string>.Success(
            $"Report generated successfully after {query.DelayMs}ms");
    }
}
