using System;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
#endif
using MicroElements.OpenApi.FluentValidation;
using MicroElements.Swashbuckle.FluentValidation.AspNetCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MicroElements.Swashbuckle.FluentValidation.Tests;

public class SwaggerTestHost
{
    private readonly Lazy<IServiceProvider> _serviceProvider;
    public IServiceCollection Services { get; }
    public IServiceProvider ServiceProvider => _serviceProvider.Value;
    public SchemaRepository SchemaRepository { get; }

    public SwaggerTestHost()
    {
        Services = new ServiceCollection();
        Services.AddOptions();

        _serviceProvider = new Lazy<IServiceProvider>(() => Services.BuildServiceProvider());
        SchemaRepository = new SchemaRepository();
    }

    public SwaggerTestHost Configure(
        Action<SchemaGenerationOptions>? configureSchemaGenerationOptions = null,
        Action<RegistrationOptions>? configureRegistration = null)
    {
        // Json options by default no name policy.
        Services.Configure<JsonOptions>(options => options.JsonSerializerOptions.PropertyNamingPolicy = null);

        // Add Swagger
        Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo{ Title = "API", Version = "v1" });
            c.EnableAnnotations(enableAnnotationsForInheritance: true, enableAnnotationsForPolymorphism: true);
        });

        // Add FV Rules to swagger
        Services.AddFluentValidationRulesToSwagger(configureSchemaGenerationOptions, configureRegistration);

        return this;
    }

    public SwaggerTestHost RegisterValidator<TValidator>()
        where TValidator : IValidator
    {
        // Add FV validators
        Services.AddValidatorsFromAssemblyContaining<SwaggerTestHost>(
            lifetime: ServiceLifetime.Scoped,
            filter: result => result.ValidatorType == typeof(TValidator));

        return this;
    }

    public SwaggerTestHost GenerateSchema<TModel>(out OpenApiSchema schema)
    {
        var schemaGenerator = ServiceProvider.GetService<ISchemaGenerator>();
        var openApiSchema = schemaGenerator.GenerateSchema(typeof(TModel), SchemaRepository);
        schema = SchemaRepository.Schemas[openApiSchema.Reference.Id];
        return this;
    }
}