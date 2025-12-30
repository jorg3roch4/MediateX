using System.Threading;
using System.Threading.Tasks;
using MediateX.Validation;

namespace MediateX.Examples.AspNetCore;

public class CreateUserValidator : IRequestValidator<CreateUserCommand>
{
    public ValueTask<ValidationResult> ValidateAsync(CreateUserCommand cmd, CancellationToken ct)
    {
        var builder = new ValidationResultBuilder();

        if (string.IsNullOrWhiteSpace(cmd.Name))
            builder.AddError("Name", "Name is required");
        else if (cmd.Name.Length < 2)
            builder.AddError("Name", "Name must be at least 2 characters");

        if (string.IsNullOrWhiteSpace(cmd.Email))
            builder.AddError("Email", "Email is required");
        else if (!cmd.Email.Contains('@'))
            builder.AddError("Email", "Email must be a valid email address");

        return ValueTask.FromResult(builder.Build());
    }
}
