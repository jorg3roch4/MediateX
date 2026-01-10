using MediateX;
using MediateX.Examples.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Configure MediateX
builder.Services.AddMediateX(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();
});

var app = builder.Build();

// ========== User Endpoints ==========

app.MapPost("/users", async (CreateUserCommand cmd, IMediator mediator) =>
{
    try
    {
        var user = await mediator.Send(cmd);
        return Results.Created($"/users/{user.Id}", user);
    }
    catch (InvalidOperationException ex)
    {
        return Results.Conflict(new { message = ex.Message });
    }
});

app.MapGet("/users/{id:int}", async (int id, IMediator mediator) =>
{
    var user = await mediator.Send(new GetUserQuery(id));
    return user is not null
        ? Results.Ok(user)
        : Results.NotFound(new { message = $"User with ID {id} not found" });
});

// ========== Ping Endpoint ==========

app.MapGet("/ping", async (IMediator mediator) =>
{
    var response = await mediator.Send(new PingQuery("Hello"));
    return Results.Ok(new { response });
});

// ========== Notification Endpoint ==========

app.MapPost("/users/{id:int}/notify", async (int id, IMediator mediator) =>
{
    await mediator.Publish(new UserNotification(id, "Profile updated"));
    return Results.Ok(new { message = "Notification sent" });
});

// ========== Info Endpoint ==========

app.MapGet("/", () => Results.Ok(new
{
    name = "MediateX v3.2.0 Examples",
    endpoints = new[]
    {
        "POST /users - Create user",
        "GET /users/{id} - Get user by ID",
        "GET /ping - Ping/Pong example",
        "POST /users/{id}/notify - Send notification"
    }
}));

app.Run();

// Make Program accessible for assembly scanning
public partial class Program { }
