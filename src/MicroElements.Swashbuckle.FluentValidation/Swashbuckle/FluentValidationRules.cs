// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using MicroElements.Swashbuckle.FluentValidation.Generation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MicroElements.Swashbuckle.FluentValidation
{
    /// <summary>
    /// Swagger <see cref="ISchemaFilter"/> that uses FluentValidation validators instead System.ComponentModel based attributes.
    /// </summary>
    public class FluentValidationRules : ISchemaFilter
    {
        private readonly ILogger _logger;

        private readonly IValidatorFactory? _validatorFactory;

        private readonly IReadOnlyList<FluentValidationRule> _rules;
        private readonly ISchemaGenerationOptions _schemaGenerationOptions;
        private readonly SchemaGenerationSettings _schemaGenerationSettings;

        /// <summary>
        /// Initializes a new instance of the <see cref="FluentValidationRules"/> class.
        /// </summary>
        /// <param name="loggerFactory"><see cref="ILoggerFactory"/> for logging. Can be null.</param>
        /// <param name="validatorFactory">Validator factory.</param>
        /// <param name="rules">External FluentValidation rules. External rule overrides default rule with the same name.</param>
        /// <param name="schemaGenerationOptions">Schema generation options.</param>
        /// <param name="nameResolver">Optional name resolver.</param>
        /// <param name="swaggerGenOptions">SwaggerGenOptions.</param>
        public FluentValidationRules(
            /* System services */
            ILoggerFactory? loggerFactory = null,

            /* FluentValidation services */
            IValidatorFactory? validatorFactory = null,

            // MicroElements services
            IEnumerable<FluentValidationRule>? rules = null,
            IOptions<SchemaGenerationOptions>? schemaGenerationOptions = null,
            INameResolver? nameResolver = null,

            // Swashbuckle services
            IOptions<SwaggerGenOptions>? swaggerGenOptions = null)
        {
            // System services
            _logger = loggerFactory?.CreateLogger(typeof(FluentValidationRules)) ?? NullLogger.Instance;

            _logger.LogDebug("FluentValidationRules Created");

            // FluentValidation services
            _validatorFactory = validatorFactory;

            // MicroElements services
            _rules = new DefaultFluentValidationRuleProvider(schemaGenerationOptions).GetRules().ToArray().OverrideRules(rules);
            _schemaGenerationOptions = schemaGenerationOptions?.Value ?? new SchemaGenerationOptions();
            _schemaGenerationSettings = new SchemaGenerationSettings
            {
                NameResolver = nameResolver,
            };

            // Swashbuckle services
            _schemaGenerationSettings = _schemaGenerationSettings with
            {
                SchemaIdSelector = swaggerGenOptions?.Value?.SchemaGeneratorOptions.SchemaIdSelector ?? new SchemaGeneratorOptions().SchemaIdSelector,
            };
        }

        /// <inheritdoc />
        public void Apply(OpenApiSchema schema, SchemaFilterContext context)
        {
            if (_validatorFactory == null)
            {
                _logger.LogWarning(0, "ValidatorFactory is not provided. Please register FluentValidation.");
                return;
            }

            if (schema == null)
                return;

            IValidator? validator = null;
            try
            {
                validator = _validatorFactory.GetValidator(context.Type);
            }
            catch (Exception e)
            {
                _logger.LogWarning(0, e, $"GetValidator for type '{context.Type}' fails.");
            }

            if (validator == null)
                return;

            var schemaProvider = new SwashbuckleSchemaProvider(context.SchemaRepository, context.SchemaGenerator, _schemaGenerationSettings.SchemaIdSelector);

            var schemaContext = new SchemaGenerationContext(
                schema: schema,
                schemaType: context.Type,
                rules: _rules,
                schemaGenerationOptions: _schemaGenerationOptions,
                schemaGenerationSettings: _schemaGenerationSettings,
                schemaProvider: schemaProvider);

            if (context.Type.Name == "Club")
            {

            }

            ApplyRulesToSchema(context, validator, schemaContext);

            try
            {
                AddRulesFromIncludedValidators(context, validator, schemaContext);
            }
            catch (Exception e)
            {
                _logger.LogWarning(0, e, $"Applying IncludeRules for type '{context.Type}' fails.");
            }
        }

        private void ApplyRulesToSchema(SchemaFilterContext context, IValidator validator, SchemaGenerationContext schemaGenerationContext)
        {
            FluentValidationSchemaBuilder.ApplyRulesToSchema(
                schemaType: context.Type,
                schemaPropertyNames: null,
                validator: validator,
                logger: _logger,
                schemaGenerationContext: schemaGenerationContext,
                schemaGenerationOptions: _schemaGenerationOptions);
        }

        private void AddRulesFromIncludedValidators(SchemaFilterContext context, IValidator validator, SchemaGenerationContext schemaGenerationContext)
        {
            FluentValidationSchemaBuilder.AddRulesFromIncludedValidators(
                validator: validator,
                logger: _logger,
                schemaGenerationContext: schemaGenerationContext,
                schemaGenerationOptions: _schemaGenerationOptions);
        }
    }
}