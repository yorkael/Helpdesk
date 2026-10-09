using Helpdesk.Api.Authentication;
using Helpdesk.Api.Errors;
using Helpdesk.Application;
using Helpdesk.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddJwtAuthentication();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ProblemDetailsExceptionHandler>();

// FluentValidation is the single source of input rules, so MVC must not add its own
// implicit [Required] for non-nullable properties.
builder.Services.AddControllers(options => options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true)
    // A body that cannot be deserialized (e.g. a number where text is expected) gets a generic message
    // instead of the JSON exception, which names internal types.
    .AddJsonOptions(options => options.AllowInputFormatterExceptionMessages = false);
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler();
// Gives a problem+json body to empty error responses, such as the 401 and 403 from authentication.
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
// Anonymous so container and platform probes can call it without a token. The body is only the overall status.
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();
