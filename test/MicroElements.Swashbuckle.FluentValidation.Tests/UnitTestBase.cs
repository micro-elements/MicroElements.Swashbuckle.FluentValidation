using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using FluentValidation;
using MicroElements.OpenApi.FluentValidation;
using MicroElements.Swashbuckle.FluentValidation.AspNetCore;
using MicroElements.Swashbuckle.FluentValidation.Generation;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MicroElements.Swashbuckle.FluentValidation.Tests
{
    public class UnitTestBase
    {
        public SwaggerGenerator SwaggerGenerator(
            Action<SwaggerGeneratorOptions> configureSwaggerGenerator = null,
            Action<SchemaGeneratorOptions>? configureGenerator = null,
            Action<JsonSerializerOptions>? configureSerializer = null)
        {
            var swaggerGeneratorOptions = new SwaggerGeneratorOptions();
            configureSwaggerGenerator?.Invoke(swaggerGeneratorOptions);

            var schemaGenerator = SchemaGenerator(configureGenerator, configureSerializer);
            var apiDescriptionGroups = new []{new ApiDescriptionGroup("GroupName", new ApiDescription[]
            {
                new ApiDescription()
                {

                }
            })};

            var apiDescriptionsProvider = new ApiDescriptionGroupCollectionProvider(
                new ApiDescriptionGroupCollection(apiDescriptionGroups, 1));

            return new SwaggerGenerator(swaggerGeneratorOptions, apiDescriptionsProvider, schemaGenerator);
        }

        public SchemaGenerator SchemaGenerator(params IValidator[] validators)
        {
            return SchemaGenerator(options => ConfigureGenerator(options, validators));
        }

        public SchemaGenerator SchemaGenerator(
            Action<SchemaGeneratorOptions>? configureGenerator = null,
            Action<JsonSerializerOptions>? configureSerializer = null)
        {
            var generatorOptions = new SchemaGeneratorOptions();
            configureGenerator?.Invoke(generatorOptions);

            var serializerOptions = new JsonSerializerOptions();
            configureSerializer?.Invoke(serializerOptions);

            return new SchemaGenerator(generatorOptions, new JsonSerializerDataContractResolver(serializerOptions));
        }

        private void ConfigureGenerator(SchemaGeneratorOptions swaggerOptions, IValidator[] validators)
        {
            SchemaGenerationOptions generationOptions = new SchemaGenerationOptions
            {
                NameResolver = new SystemTextJsonNameResolver()
            };
            generationOptions = generationOptions.FillDefaultValues(null);

            var schemaGenerationOptions = new OptionsWrapper<SchemaGenerationOptions>(generationOptions);

            IValidatorRegistry validatorRegistry = new ValidatorRegistry(validators, schemaGenerationOptions);
            swaggerOptions.SchemaFilters.Add(new FluentValidationRules(
                validatorRegistry: validatorRegistry,
                schemaGenerationOptions: schemaGenerationOptions));
        }
    }

    public class ApiDescriptionGroupCollectionProvider : IApiDescriptionGroupCollectionProvider
    {
        public ApiDescriptionGroupCollection ApiDescriptionGroups { get; }

        public ApiDescriptionGroupCollectionProvider(ApiDescriptionGroupCollection apiDescriptionGroups)
        {
            ApiDescriptionGroups = apiDescriptionGroups;
        }
    }

    public class SchemaBuilder<T>
    {
        public InlineValidator<T> Validator { get; } = new InlineValidator<T>();

        public SchemaRepository SchemaRepository { get; } = new SchemaRepository();

        private readonly SchemaGenerationOptions _schemaGenerationOptions = new SchemaGenerationOptions();

        public SchemaBuilder()
        {
            _schemaGenerationOptions.NameResolver = new SystemTextJsonNameResolver();
            _schemaGenerationOptions.SchemaIdSelector = new SchemaGeneratorOptions().SchemaIdSelector;
        }

        public SchemaBuilder<T> ConfigureSchemaGenerationOptions(
            Action<SchemaGenerationOptions> configureSchemaGenerationOptions)
        {
            configureSchemaGenerationOptions(_schemaGenerationOptions);

            return this;
        }

        public OpenApiSchema AddRule<TProperty>(
            Expression<Func<T, TProperty>> propertyExpression,
            Action<IRuleBuilderInitial<T, TProperty>>? configureRule = null,
            Action<OpenApiSchema>? schemaCheck = null)
        {
            IRuleBuilderInitial<T, TProperty> ruleBuilder = Validator.RuleFor(propertyExpression);
            configureRule?.Invoke(ruleBuilder);

            var expressionBody = propertyExpression.Body as MemberExpression;
            var schema = SchemaRepository.GenerateSchemaForValidator(Validator, configureSchemaGenerationOptions: options => options.SetFrom(_schemaGenerationOptions));

            PropertyInfo propertyInfo = expressionBody.Member as PropertyInfo;
            string propertyName = _schemaGenerationOptions.NameResolver.GetPropertyName(propertyInfo);

            var property = schema.Properties[propertyName];

            schemaCheck?.Invoke(property);

            return property;
        }
    }

    public static class TestExtensions
    {
        public static OpenApiSchema GenerateSchemaForValidator<T>(
            this SchemaRepository schemaRepository,
            IValidator<T> validator,
            SchemaGenerationOptions? schemaGenerationOptions = null,
            Action<SchemaGenerationOptions>? configureSchemaGenerationOptions = null,
            Action<JsonSerializerOptions>? configureSerializer = null)
        {
            var services = new ServiceCollection();
            services.AddFluentValidationRulesToSwagger(configureSchemaGenerationOptions);
            var serviceProvider = services.BuildServiceProvider();

            SchemaGenerator schemaGenerator = CreateSchemaGenerator(
                new []{ validator },
                serviceProvider: serviceProvider,
                configureSerializer: configureSerializer);

            OpenApiSchema schema = schemaGenerator
                .GenerateSchema(typeof(T), schemaRepository);

            if (schema.Reference?.Id != null)
                schema = schemaRepository.Schemas[schema.Reference.Id];

            return schema;
        }

        public static SchemaGenerator CreateSchemaGenerator(
            IValidator[] validators,
            IServiceProvider serviceProvider,
            Action<JsonSerializerOptions>? configureSerializer = null)
        {
            return CreateSchemaGenerator(
                configureGenerator: options =>
                {
                    var generationOptions = serviceProvider.GetService<IOptions<SchemaGenerationOptions>>();

                    IValidatorRegistry validatorRegistry = new ValidatorRegistry(validators, generationOptions);

                    FluentValidationRules fluentValidationRules = new FluentValidationRules(
                        loggerFactory: null,
                        serviceProvider: serviceProvider,
                        validatorRegistry: validatorRegistry,
                        rules: null,
                        schemaGenerationOptions: generationOptions);

                    options.SchemaFilters.Add(fluentValidationRules);
                },
                configureSerializer: configureSerializer);
        }

        public static SchemaGenerator CreateSchemaGenerator(
            Action<SchemaGeneratorOptions>? configureGenerator = null,
            Action<JsonSerializerOptions>? configureSerializer = null)
        {
            var generatorOptions = new SchemaGeneratorOptions();
            configureGenerator?.Invoke(generatorOptions);

            var serializerOptions = new JsonSerializerOptions();
            configureSerializer?.Invoke(serializerOptions);

            return new SchemaGenerator(generatorOptions, new JsonSerializerDataContractResolver(serializerOptions));
        }
    }
}