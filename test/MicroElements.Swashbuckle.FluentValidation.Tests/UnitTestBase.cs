using System;
using System.Linq.Expressions;
using System.Text.Json;
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
        public FluentValidator<T> Validator { get; } = new FluentValidator<T>();

        public SchemaRepository SchemaRepository { get; } = new SchemaRepository();

        private readonly FluentValidationSwaggerGenOptions _fluentValidationSwaggerGenOptions = new FluentValidationSwaggerGenOptions();

        public SchemaBuilder<T> ConfigureFVSwaggerGenOptions(Action<FluentValidationSwaggerGenOptions> configureFVSwaggerGenOptions)
        {
            configureFVSwaggerGenOptions(_fluentValidationSwaggerGenOptions);
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
            var schema = SchemaRepository.GenerateSchemaForValidator(Validator, _fluentValidationSwaggerGenOptions);

            var property = schema.Properties[expressionBody.Member.Name];

            schemaCheck?.Invoke(property);

            return property;
        }
    }

    public static class TestExtensions
    {
        public static OpenApiSchema GenerateSchemaForValidator<T>(this SchemaRepository schemaRepository, IValidator<T> validator, FluentValidationSwaggerGenOptions? fluentValidationSwaggerGenOptions)
        {
            OpenApiSchema schema = CreateSchemaGenerator(
                    new []{ validator },
                    fluentValidationSwaggerGenOptions: fluentValidationSwaggerGenOptions)
                .GenerateSchema(typeof(T), schemaRepository);

            if (schema.Reference?.Id != null)
                schema = schemaRepository.Schemas[schema.Reference.Id];

            return schema;
        }

        public static OpenApiSchema Generate<T, TProperty>(
            Expression<Func<T, TProperty>> expression,
            Action<IRuleBuilderInitial<T, TProperty>> action)
        {
            var validator = new FluentValidator<T>();
            IRuleBuilderInitial<T, TProperty> ruleBuilder = validator.RuleFor(expression);
            action(ruleBuilder);

            var expressionBody = expression.Body as MemberExpression;

            var schema = new SchemaRepository().GenerateSchemaForValidator(validator, new FluentValidationSwaggerGenOptions());

            var property = schema.Properties[expressionBody.Member.Name];
            return property;
        }

        public static SchemaGenerator CreateSchemaGenerator(
            IValidator[] validators,
            FluentValidationSwaggerGenOptions? fluentValidationSwaggerGenOptions = null)
        {
            return CreateSchemaGenerator(options =>
            {
                IValidatorFactory validatorFactory = new CustomValidatorFactory(validators);

                options.SchemaFilters.Add(new FluentValidationRules(
                    validatorFactory: validatorFactory, 
                    rules: null, 
                    loggerFactory: null,
                    options: fluentValidationSwaggerGenOptions != null ? new OptionsWrapper<FluentValidationSwaggerGenOptions>(fluentValidationSwaggerGenOptions) : null));
            });
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

    public class FluentValidator<T> : AbstractValidator<T>
    {
        public FluentValidator()
        {
        }
    }
}