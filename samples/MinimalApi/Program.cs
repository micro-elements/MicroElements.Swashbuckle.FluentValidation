using FluentValidation;
using MicroElements.Swashbuckle.FluentValidation.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;

// Asp.Net stuff
services.AddControllers();
services.AddEndpointsApiExplorer();

// Add Swagger
services.AddSwaggerGen();

// Add FV validators
services.AddValidatorsFromAssemblyContaining<Program>();

// Add FV Rules to swagger (this sample exercises the document-filter pipeline)
services.AddFluentValidationRulesToSwagger(
    configureRegistration: options => options.UseDocumentFilter = true);

var app = builder.Build();

// Use Swagger
app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

// Issue #180: Minimal API endpoint with [AsParameters].
// SearchQuery should NOT appear in components/schemas because Swashbuckle
// expands [AsParameters] records into individual query parameters.
app.MapGet("/api/search", ([AsParameters] SearchQuery query) => Results.Ok(query));

app.Run();

/// <summary>
/// Issue #180: Record used with [AsParameters] in minimal API.
/// </summary>
public record SearchQuery(string? Query, int Page);

/// <summary>
/// Validator for SearchQuery.
/// </summary>
public class SearchQueryValidator : AbstractValidator<SearchQuery>
{
    public SearchQueryValidator()
    {
        RuleFor(x => x.Query).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Page).GreaterThan(0);
    }
}