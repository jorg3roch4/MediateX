namespace MediateX.Examples.AspNetCore;

public record PingQuery(string Message) : IRequest<string>;

public class PingHandler : IRequestHandler<PingQuery, string>
{
    public Task<string> Handle(PingQuery query, CancellationToken ct)
    {
        return Task.FromResult($"{query.Message} Pong");
    }
}
