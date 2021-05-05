using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using FluentValidation;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using SampleWebApi.ValidatorFactories;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MicroElements.Swashbuckle.FluentValidation.Tests
{
    public class UnitTestBase
    {
        public SchemaGenerator SchemaGenerator(params IValidator[] validators)
        {
            return SchemaGenerator(options => ConfigureGenerator(options, validators));
        }

        public SchemaGenerator SchemaGenerator(
            Action<SchemaGeneratorOptions> configureGenerator = null,
            Action<JsonSerializerOptions> configureSerializer = null)
        {
            var generatorOptions = new SchemaGeneratorOptions();
            configureGenerator?.Invoke(generatorOptions);

            var serializerOptions = new JsonSerializerOptions();
            configureSerializer?.Invoke(serializerOptions);

            return new SchemaGenerator(generatorOptions, new JsonSerializerDataContractResolver(serializerOptions));
        }

        private void ConfigureGenerator(SchemaGeneratorOptions options, params IValidator[] validators)
        {
            IValidatorFactory validatorFactory = new CustomValidatorFactory(validators);
            options.SchemaFilters.Add(new FluentValidationRules(validatorFactory));
        }
    }

    public class SchemaBuilder<T>
    {
        public InlineValidator<T> Validator { get; } = new InlineValidator<T>();

        public SchemaRepository SchemaRepository { get; } = new SchemaRepository();

        private readonly SchemaGenerationOptions _schemaGenerationOptions = new SchemaGenerationOptions();

        public SchemaBuilder<T> ConfigureSchemaGenerationOptions(Action<SchemaGenerationOptions> configureFVSwaggerGenOptions)
        {
            configureFVSwaggerGenOptions(_schemaGenerationOptions);
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
            var schema = SchemaRepository.GenerateSchemaForValidator(Validator, _schemaGenerationOptions);


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
            Action<JsonSerializerOptions>? configureSerializer = null)
        {
            SchemaGenerator schemaGenerator = CreateSchemaGenerator(
                new []{ validator },
                fluentValidationSwaggerGenOptions: schemaGenerationOptions,
                configureSerializer: configureSerializer);

            OpenApiSchema schema = schemaGenerator
                .GenerateSchema(typeof(T), schemaRepository);

            if (schema.Reference?.Id != null)
                schema = schemaRepository.Schemas[schema.Reference.Id];

            return schema;
        }

        public static SchemaGenerator CreateSchemaGenerator(
            IValidator[] validators,
            SchemaGenerationOptions? fluentValidationSwaggerGenOptions = null,
            Action<JsonSerializerOptions>? configureSerializer = null)
        {
            return CreateSchemaGenerator(
                configureGenerator: options =>
                {
                    IValidatorFactory validatorFactory = new CustomValidatorFactory(validators);

                    options.SchemaFilters.Add(new FluentValidationRules(
                        validatorFactory: validatorFactory, 
                        rules: null, 
                        loggerFactory: null,
                        schemaGenerationOptions: fluentValidationSwaggerGenOptions != null ? new OptionsWrapper<SchemaGenerationOptions>(fluentValidationSwaggerGenOptions) : null));
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