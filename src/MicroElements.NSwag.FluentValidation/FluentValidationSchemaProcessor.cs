using System;
using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using MicroElements.OpenApi.FluentValidation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NJsonSchema.Generation;

namespace MicroElements.NSwag.FluentValidation
{
    /// <summary>
    /// Swagger <see cref="ISchemaProcessor"/> that uses FluentValidation validators instead System.ComponentModel based attributes.
    /// </summary>
    public class FluentValidationSchemaProcessor : ISchemaProcessor
    {
        private readonly ILogger _logger;

        private readonly IValidatorFactory _validatorFactory;

        private readonly IReadOnlyList<IFluentValidationRule<SchemaProcessorContext>> _rules;
        private readonly ISchemaGenerationOptions _schemaGenerationOptions;
        private readonly SchemaGenerationSettings _schemaGenerationSettings;

        /// <summary>
        /// Initializes a new instance of the <see cref="FluentValidationSchemaProcessor"/> class.
        /// </summary>
        /// <param name="validatorFactory">Validator factory.</param>
        /// <param name="rules">External FluentValidation rules. Rule with the same name replaces default rule.</param>
        /// <param name="loggerFactory"><see cref="ILoggerFactory"/> for logging. Can be null.</param>
        public FluentValidationSchemaProcessor(
            /* System services */
            ILoggerFactory? loggerFactory = null,

            /* FluentValidation services */
            IValidatorFactory? validatorFactory = null,

            // MicroElements services
            IEnumerable<FluentValidationRule>? rules = null,
            IOptions<SchemaGenerationOptions>? schemaGenerationOptions = null,
            INameResolver? nameResolver = null,

            // Swashbuckle services
            IOptions<JsonSchemaGeneratorSettings>? swaggerGenOptions = null)
        {
            // System services
            _logger = loggerFactory?.CreateLogger(typeof(FluentValidationSchemaProcessor)) ?? NullLogger.Instance;

            _logger.LogDebug("FluentValidationRules Created");

            // FluentValidation services
            _validatorFactory = validatorFactory;

            // MicroElements services
            _rules = new NSwagFluentValidationRuleProvider().GetRules().ToArray().OverrideRules(rules);
            _schemaGenerationOptions = schemaGenerationOptions?.Value ?? new SchemaGenerationOptions();
            _schemaGenerationSettings = new SchemaGenerationSettings
            {
                NameResolver = nameResolver,
            };

            // Swashbuckle services
            // _schemaGenerationSettings = _schemaGenerationSettings with
            // {
            //     SchemaIdSelector = swaggerGenOptions?.Value?.SchemaGeneratorOptions.SchemaIdSelector ?? new SchemaGeneratorOptions().SchemaIdSelector,
            // };
            // context.Settings.SchemaNameGenerator.Generate()
        }

        /// <inheritdoc />
        public void Process(SchemaProcessorContext context)
        {
            if (!context.Schema.IsObject || context.Schema.Properties.Count == 0)
            {
                // Ignore other 
                // Ignore objects with no properties
                return;
            }

            IValidator? validator = null;

            try
            {
                validator = _validatorFactory.GetValidator(context.Type);
            }
            catch (Exception e)
            {
                _logger.LogWarning(0, e, $"GetValidator for type '{context.Type}' fails");
            }

            // Check if a validator exists for this property
            if (validator == null)
            {
                return;
            }

            var processorContext = new NSwagSchemaProcessorContext(
                schema: context,
                schemaType: context.Type,
                rules: _rules,
                schemaProvider: null,
                schemaGenerationOptions: _schemaGenerationOptions,
                schemaGenerationSettings: _schemaGenerationSettings);

            _logger.LogDebug($"Applying FluentValidation rules to swagger schema for type '{context.Type}'");

            ApplyRulesToSchema(processorContext, validator);

            try
            {
                AddRulesFromIncludedValidators(processorContext, validator);
            }
            catch (Exception e)
            {
                _logger.LogWarning(0, e, $"Applying IncludeRules for type '{context.Type}' fails");
            }
        }

        private void ApplyRulesToSchema(NSwagSchemaProcessorContext context, IValidator validator)
        {
            FluentValidationSchemaBuilder.ApplyRulesToSchema(
                schemaType: context.SchemaType,
                schemaPropertyNames: context.Properties,
                validator: validator,
                logger: _logger,
                schemaGenerationContext: context);
        }

        private void AddRulesFromIncludedValidators(NSwagSchemaProcessorContext context, IValidator validator)
        {
            FluentValidationSchemaBuilder.AddRulesFromIncludedValidators(
                validator: validator,
                logger: _logger,
                schemaGenerationContext: context);
        }
    }
}