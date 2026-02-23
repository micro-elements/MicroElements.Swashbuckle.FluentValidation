using FluentValidation;
using MicroElements.AspNetCore.OpenApi.FluentValidation;

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;

// Add FV validators
services.AddValidatorsFromAssemblyContaining<Program>();

// Add FV Rules to OpenApi
services.AddFluentValidationRulesToOpenApi();

// Add OpenApi with FluentValidation transformer
services.AddOpenApi(options =>
{
    options.AddFluentValidationRules();
});

var app = builder.Build();

// Map OpenApi endpoint
app.MapOpenApi();

// Map sample endpoints
app.MapPost("/api/customers", (Customer customer) => Results.Ok(customer));
app.MapPost("/api/orders", (Order order) => Results.Ok(order));
app.MapGet("/api/search", ([AsParameters] SearchQuery query) => Results.Ok(query));

app.Run();

// ---- Models ----

/// <summary>
/// Sample customer model.
/// </summary>
public class Customer
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int Age { get; set; }
    public string? Phone { get; set; }
    public Address? Address { get; set; }
}

/// <summary>
/// Sample address model.
/// </summary>
public class Address
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
}

/// <summary>
/// Sample order model.
/// </summary>
public class Order
{
    public string OrderNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int Quantity { get; set; }
    public List<string> Items { get; set; } = new();
}

/// <summary>
/// Sample search query.
/// </summary>
public record SearchQuery(string? Query, int Page);

// ---- Validators ----

public class CustomerValidator : AbstractValidator<Customer>
{
    public CustomerValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(50);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Age).GreaterThanOrEqualTo(0).LessThanOrEqualTo(150);
        RuleFor(x => x.Phone).Matches(@"^\+?[\d\s\-]+$").When(x => x.Phone != null);
    }
}

public class AddressValidator : AbstractValidator<Address>
{
    public AddressValidator()
    {
        RuleFor(x => x.Street).NotEmpty().MaximumLength(200);
        RuleFor(x => x.City).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ZipCode).NotEmpty().Length(5, 10);
    }
}

public class OrderValidator : AbstractValidator<Order>
{
    public OrderValidator()
    {
        RuleFor(x => x.OrderNumber).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Amount).GreaterThan(0).LessThanOrEqualTo(1_000_000);
        RuleFor(x => x.Quantity).InclusiveBetween(1, 1000);
        RuleFor(x => x.Items).NotEmpty();
    }
}

public class SearchQueryValidator : AbstractValidator<SearchQuery>
{
    public SearchQueryValidator()
    {
        RuleFor(x => x.Query).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Page).GreaterThan(0);
    }
}
