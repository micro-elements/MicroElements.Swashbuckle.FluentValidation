# MicroElements.Swashbuckle.FluentValidation
Use FluentValidation rules instead of ComponentModel attributes to define swagger schema.

Note: For WebApi see: https://github.com/micro-elements/MicroElements.Swashbuckle.FluentValidation.WebApi

## Statuses

[![License](https://img.shields.io/github/license/micro-elements/MicroElements.Swashbuckle.FluentValidation.svg)](https://raw.githubusercontent.com/micro-elements/MicroElements.Swashbuckle.FluentValidation/master/LICENSE)
[![NuGetVersion](https://img.shields.io/nuget/v/MicroElements.Swashbuckle.FluentValidation.svg)](https://www.nuget.org/packages/MicroElements.Swashbuckle.FluentValidation)
![NuGetDownloads](https://img.shields.io/nuget/dt/MicroElements.Swashbuckle.FluentValidation.svg)
[![MyGetVersion](https://img.shields.io/myget/micro-elements/v/MicroElements.Swashbuckle.FluentValidation.svg)](https://www.myget.org/feed/micro-elements/package/nuget/MicroElements.Swashbuckle.FluentValidation)

[![Travis](https://img.shields.io/travis/micro-elements/MicroElements.Swashbuckle.FluentValidation/master.svg?logo=travis)](https://travis-ci.org/micro-elements/MicroElements.Swashbuckle.FluentValidation)
[![AppVeyor](https://img.shields.io/appveyor/ci/micro-elements/microelements-swashbuckle-fluentvalidation.svg?logo=appveyor)](https://ci.appveyor.com/project/micro-elements/microelements-swashbuckle-fluentvalidation)
[![Coverage Status](https://img.shields.io/coveralls/micro-elements/MicroElements.Swashbuckle.FluentValidation.svg)](https://coveralls.io/r/micro-elements/MicroElements.Swashbuckle.FluentValidation)

[![Gitter](https://img.shields.io/gitter/room/micro-elements/MicroElements.Swashbuckle.FluentValidation.svg)](https://gitter.im/micro-elements/MicroElements.Swashbuckle.FluentValidation)

## Usage

### 1. Reference packages in your web project:

```xml
<PackageReference Include="FluentValidation.AspNetCore" Version="7.5.2" />
<PackageReference Include="MicroElements.Swashbuckle.FluentValidation" Version="1.1.0" />
<PackageReference Include="Swashbuckle.AspNetCore" Version="3.0.0" />
```

### 2. Change Startup.cs

```csharp
// This method gets called by the runtime. Use this method to add services to the container.
public void ConfigureServices(IServiceCollection services)
{
    services
        .AddMvc()
        // Adds fluent validators to Asp.net
        .AddFluentValidation(fv => fv.RegisterValidatorsFromAssemblyContaining<CustomerValidator>());

    services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new Info { Title = "My API", Version = "v1" });
        // Adds fluent validation rules to swagger
        c.AddFluentValidationRules();
    });

    // Adds logging
    services.AddLogging(builder => builder.AddConsole());
}

// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
public void Configure(IApplicationBuilder app, IHostingEnvironment env)
{
    app
        .UseMvc()
        // Adds swagger
        .UseSwagger();

    // Adds swagger UI
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1");
    });
}
```

## Version compatibility

MicroElements.Swashbuckle.FluentValidation | Swashbuckle.AspNetCore | FluentValidation
---------|----------|---------
 <=1.1.0 | <=3.0.0 | >=7.2.0
 >=2.0.0 | >=4.0.0 | >=7.2.0

## Sample application

See sample project: https://github.com/micro-elements/MicroElements.Swashbuckle.FluentValidation/tree/master/src/SampleWebApi

## Supported validators

* INotNullValidator (NotNull)
* INotEmptyValidator (NotEmpty)
* ILengthValidator (Length, MinimumLength, MaximumLength, ExactLength)
* IRegularExpressionValidator (Email, Matches)
* IComparisonValidator (GreaterThan, GreaterThanOrEqual, LessThan, LessThanOrEqual)
* IBetweenValidator (InclusiveBetween, ExclusiveBetween)

## Extensibility

You can register FluentValidationRule in ServiceCollection.

User defined rule name replaces default rule with the same.
Full list of default rules can be get by `FluentValidationRules.CreateDefaultRules()`

List or default rules:

* Required
* NotEmpty
* Length
* Pattern
* Comparison
* Between

Example of rule:

```csharp
new FluentValidationRule("Pattern")
{
    Matches = propertyValidator => propertyValidator is IRegularExpressionValidator,
    Apply = context =>
    {
        var regularExpressionValidator = (IRegularExpressionValidator)context.PropertyValidator;
        context.Schema.Properties[context.PropertyKey].Pattern = regularExpressionValidator.Expression;
    }
},
```

## Samples

### Swagger Sample model and validator

```csharp
public class Sample
{
    public string PropertyWithNoRules { get; set; }

    public string NotNull { get; set; }
    public string NotEmpty { get; set; }
    public string EmailAddress { get; set; }
    public string RegexField { get; set; }

    public int ValueInRange { get; set; }
    public int ValueInRangeExclusive { get; set; }

    public float ValueInRangeFloat { get; set; }
    public double ValueInRangeDouble { get; set; }
}

public class SampleValidator : AbstractValidator<Sample>
{
    public SampleValidator()
    {
        RuleFor(sample => sample.NotNull).NotNull();
        RuleFor(sample => sample.NotEmpty).NotEmpty();
        RuleFor(sample => sample.EmailAddress).EmailAddress();
        RuleFor(sample => sample.RegexField).Matches(@"(\d{4})-(\d{2})-(\d{2})");

        RuleFor(sample => sample.ValueInRange).GreaterThanOrEqualTo(5).LessThanOrEqualTo(10);
        RuleFor(sample => sample.ValueInRangeExclusive).GreaterThan(5).LessThan(10);

        // WARNING: Swashbuckle implements minimum and maximim as int so you will loss fraction part of float and double numbers
        RuleFor(sample => sample.ValueInRangeFloat).InclusiveBetween(1.1f, 5.3f);
        RuleFor(sample => sample.ValueInRangeDouble).ExclusiveBetween(2.2, 7.5f);
    }
}
```

### Swagger Sample model screenshot

![SwaggerSample](image/swagger_sample.png "SwaggerSample")

### Validator with Include

```csharp
public class CustomerValidator : AbstractValidator<Customer>
{
    public CustomerValidator()
    {
        RuleFor(customer => customer.Surname).NotEmpty();
        RuleFor(customer => customer.Forename).NotEmpty().WithMessage("Please specify a first name");

        Include(new CustomerAddressValidator());
    }
}

internal class CustomerAddressValidator : AbstractValidator<Customer>
{
    public CustomerAddressValidator()
    {
        RuleFor(customer => customer.Address).Length(20, 250);
    }
}
```

## Get params bounded to validatable models

MicroElements.Swashbuckle.FluentValidation updates swagger schema for operation parameters bounded to validatable models.

## Defining rules dynamically from database

See BlogValidator in sample.

## Common problems and workarounds

### Error: `System.InvalidOperationException: 'Cannot resolve 'IValidator<T>' from root provider because it requires scoped service 'TDependency'`

Workarounds in order or preference:

#### Workaround 1 (Use ScopedSwaggerMiddleware)

Replace `UseSwagger` for `UseScopedSwagger`:

```csharp
public void Configure(IApplicationBuilder app, IHostingEnvironment env)
{
    app
        .UseMvc()
        // Use scoped swagger if you have problems with scoped services in validators
        .UseScopedSwagger();
```

#### Workaround 2 (Set ValidateScopes to false)

```csharp
public static IWebHost BuildWebHost(string[] args) =>
    WebHost.CreateDefaultBuilder(args)
        // Needed for using scoped services (for example DbContext) in validators
        .UseDefaultServiceProvider(options => options.ValidateScopes = false)
        .UseStartup<Startup>()
        .Build();
```

#### Workaround 3 (Use ServiceProviderScopedValidatorFactory)

```csharp
public void ConfigureServices(IServiceCollection services)
{
    services
        .AddMvc()
        // Adds fluent validators to Asp.net
        .AddFluentValidation(c =>
        {
            c.RegisterValidatorsFromAssemblyContaining<Startup>();
            // Optionally set validator factory if you have problems with scope resolve inside validators.
            c.ValidatorFactoryType = typeof(ServiceProviderScopedValidatorFactory);
        });

```

See source of ScopedServiceProviderValidatorFactory: https://github.com/micro-elements/MicroElements.Swashbuckle.FluentValidation/tree/master/src/SampleWebApi

## Problem: I cant use several validators of one type

Example: You split validator into several small validators but AspNetCore uses only one of them.

Workaround: Hide dependent validators with `internal` and use `Include` to include other validation rules to one "Main" validator.

## Credits

Initial version of this project was based on
[Mujahid Daud Khan](https://stackoverflow.com/users/1735196/mujahid-daud-khan) answer on StackOverflow:
https://stackoverflow.com/questions/44638195/fluent-validation-with-swagger-in-asp-net-core/49477995#49477995
