using MediateX;
using MediateX.Behaviors;
using MediateX.Validation;
using MediateX.Examples.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Configure MediateX with v3.1.0 pipeline behaviors
builder.Services.AddMediateX(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();

    // Recommended pipeline order: timeout -> retry -> logging -> validation
    // Use *ResultBehavior variants for IResultRequest<T> types
    cfg.AddTimeoutResultBehavior(opt => opt.DefaultTimeout = TimeSpan.FromSeconds(30));
    cfg.AddRetryResultBehavior(opt =>
    {
        opt.MaxRetryAttempts = 3;
        opt.UseExponentialBackoff = true;
        opt.BaseDelay = TimeSpan.FromMilliseconds(200);
        opt.ShouldRetryResultError = error => error.Code == "ConnectionError";
    });
    cfg.AddLoggingBehavior(opt => opt.SlowRequestThresholdMs = 500);
    cfg.AddValidationResultBehavior();

    // Register validators
    cfg.AddRequestValidator<CreateUserValidator>();
});

var app = builder.Build();

// ========== User Endpoints (Result<T> + Validation) ==========

app.MapPost("/users", async (CreateUserCommand cmd, IMediator mediator) =>
{
    var result = await mediator.Send(cmd);
    return result.Match(
        onSuccess: user => Results.Created($"/users/{user.Id}", user),
        onFailure: error => error.Code == "Validation"
            ? Results.BadRequest(new { error.Code, error.Message })
            : Results.Conflict(new { error.Code, error.Message })
    );
});

app.MapGet("/users/{id:int}", async (int id, IMediator mediator) =>
{
    var result = await mediator.Send(new GetUserQuery(id));
    return result.Match(
        onSuccess: user => Results.Ok(user),
        onFailure: error => error.Code == "NotFound"
            ? Results.NotFound(new { error.Message })
            : Results.BadRequest(new { error.Message })
    );
});

// ========== External API Endpoint (Retry Demo) ==========

app.MapGet("/external-api", async (IMediator mediator) =>
{
    var result = await mediator.Send(new CallExternalApiQuery());
    return result.Match(
        onSuccess: data => Results.Ok(new { data }),
        onFailure: error => Results.StatusCode(503)
    );
});

// ========== Slow Report Endpoint (Timeout Demo) ==========

app.MapGet("/slow-report", async (int? delayMs, IMediator mediator) =>
{
    var result = await mediator.Send(new GenerateSlowReportQuery(delayMs ?? 2000));
    return result.Match(
        onSuccess: report => Results.Ok(new { report }),
        onFailure: error => error.Code == "Timeout"
            ? Results.StatusCode(504)
            : Results.BadRequest(new { error.Message })
    );
});

// ========== Info Endpoint ==========

app.MapGet("/", () => Results.Ok(new
{
    name = "MediateX v3.1.0 Examples",
    endpoints = new[]
    {
        "POST /users - Create user (validation + Result<T>)",
        "GET /users/{id} - Get user (Result<T>)",
        "GET /external-api - Call external API (retry demo)",
        "GET /slow-report?delayMs=2000 - Generate report (timeout demo)"
    }
}));

app.Run();

// Make Program accessible for assembly scanning
public partial class Program { }
