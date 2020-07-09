using System;
using System.Text.Json;
using FluentValidation;
using SampleWebApi.ValidatorFactories;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MicroElements.Swashbuckle.FluentValidation.Tests
{
    public class UnitTestBase
    {
        public SchemaGenerator SchemaGenerator(
            Action<SchemaGeneratorOptions> configureGenerator = null,
            Action<JsonSerializerOptions> configureSerializer = null)
        {
            var generatorOptions = new SchemaGeneratorOptions();
            configureGenerator?.Invoke(generatorOptions);

            var serializerOptions = new JsonSerializerOptions();
            configureSerializer?.Invoke(serializerOptions);

            return new SchemaGenerator(generatorOptions, new JsonSerializerMetadataResolver(serializerOptions));
        }

        public SchemaGenerator SchemaGenerator(params IValidator[] validators)
        {
            return SchemaGenerator(options => ConfigureGenerator(options, validators));
        }

        private void ConfigureGenerator(SchemaGeneratorOptions options, params IValidator[] validators)
        {
            IValidatorFactory validatorFactory = new CustomValidatorFactory(validators);
            options.SchemaFilters.Add(new FluentValidationRules(validatorFactory));
        }
    }
}