# MicroElements.Swashbuckle.FluentValidation
Use FluentValidation rules instead ComponentModel attributes to define swagger schema.

## Latest Builds, Packages

[![Travis](https://img.shields.io/travis/micro-elements/MicroElements.Swashbuckle.FluentValidation/master.svg?label=travis%20build)](https://travis-ci.org/micro-elements/MicroElements.Swashbuckle.FluentValidation)
[![NuGet](https://img.shields.io/nuget/v/MicroElements.Swashbuckle.FluentValidation.svg)](https://www.nuget.org/packages/MicroElements.Swashbuckle.FluentValidation/)

## Usage

```csharp
        public void ConfigureServices(IServiceCollection services)
        {
            services
                .AddMvc()
                // Adds fluent validators to Asp.net
                .AddFluentValidation(fv => fv.RegisterValidatorsFromAssemblyContaining<CustomerValidator>());

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new Info { Title = "My API", Version = "v1" });
                // Adds fluent validation rules in swagger
                c.AddFluentValidationRules();
            });
        }
```

## Sample application
See sample project: https://github.com/micro-elements/MicroElements.Swashbuckle.FluentValidation/tree/master/src/SampleWebApi