// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using MicroElements.OpenApi.Core;
using MicroElements.OpenApi.FluentValidation;
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

        private readonly IValidatorRegistry _validatorRegistry;

        private readonly IReadOnlyList<IFluentValidationRule<OpenApiSchema>> _rules;
        private readonly SchemaGenerationOptions _schemaGenerationOptions;

        /// <summary>
        /// Initializes a new instance of the <see cref="FluentValidationRules"/> class.
        /// </summary>
        /// <param name="loggerFactory"><see cref="ILoggerFactory"/> for logging. Can be null.</param>
        /// <param name="serviceProvider">Validator factory.</param>
        /// <param name="validatorRegistry">Gets validators for a particular type.</param>
        /// <param name="rules">External FluentValidation rules. External rule overrides default rule with the same name.</param>
        /// <param name="schemaGenerationOptions">Schema generation options.</param>
        /// <param name="swaggerGenOptions">SwaggerGenOptions.</param>
        public FluentValidationRules(
            /* System services */
            ILoggerFactory? loggerFactory = null,
            IServiceProvider? serviceProvider = null,

            /* MicroElements services */
            IValidatorRegistry? validatorRegistry = null,
            IEnumerable<FluentValidationRule>? rules = null,
            IOptions<SchemaGenerationOptions>? schemaGenerationOptions = null,

            /* Swashbuckle services */
            IOptions<SwaggerGenOptions>? swaggerGenOptions = null)
        {
            // System services
            _logger = loggerFactory?.CreateLogger(typeof(FluentValidationRules)) ?? NullLogger.Instance;

            // FluentValidation services
            _validatorRegistry = validatorRegistry ?? new ServiceProviderValidatorRegistry(serviceProvider, schemaGenerationOptions);

            // MicroElements services
            //TODO: Inject IFluentValidationRuleProvider
            _rules = new DefaultFluentValidationRuleProvider(schemaGenerationOptions).GetRules().ToArray().OverrideRules(rules);
            _schemaGenerationOptions = schemaGenerationOptions?.Value ?? new SchemaGenerationOptions();

            // Swashbuckle services
            _schemaGenerationOptions.FillFromSwashbuckleOptions(swaggerGenOptions);

            _schemaGenerationOptions.FillDefaultValues(serviceProvider);

            _logger.LogDebug("FluentValidationRules Created");
        }

        /// <inheritdoc />
        public void Apply(OpenApiSchema? schema, SchemaFilterContext context)
        {
            if (schema is null)
                return;

            // Do not process simple types.
            if (context.Type.IsPrimitiveType())
                return;

            var (validators, _) = Functional
                .Try(() => _validatorRegistry.GetValidators(context.Type).ToArray())
                .OnError(e => _logger.LogWarning(0, e, "GetValidators for type '{ModelType}' failed", context.Type));

            if (validators == null)
                return;

            if (validators.Length > 1)
            {
                //TODO: remove debug
                int i = 0;
            }

            foreach (var validator in validators)
            {
                var schemaContext = new SchemaGenerationContext(
                    schemaRepository: context.SchemaRepository,
                    schemaGenerator: context.SchemaGenerator,
                    schemaType: context.Type,
                    schema: schema,
                    rules: _rules,
                    schemaGenerationOptions: _schemaGenerationOptions);

                ApplyRulesToSchema(schemaContext, validator);

                try
                {
                    AddRulesFromIncludedValidators(schemaContext, validator);
                }
                catch (Exception e)
                {
                    _logger.LogWarning(0, e, "Applying IncludeRules for type '{ModelType}' failed", context.Type);
                }
            }
        }

        private void ApplyRulesToSchema(SchemaGenerationContext schemaGenerationContext, IValidator validator)
        {
            FluentValidationSchemaBuilder.ApplyRulesToSchema(
                schemaType: schemaGenerationContext.SchemaType,
                schemaPropertyNames: schemaGenerationContext.Properties,
                validator: validator,
                logger: _logger,
                schemaGenerationContext: schemaGenerationContext);
        }

        private void AddRulesFromIncludedValidators(SchemaGenerationContext schemaGenerationContext, IValidator validator)
        {
            FluentValidationSchemaBuilder.AddRulesFromIncludedValidators(
                validator: validator,
                logger: _logger,
                schemaGenerationContext: schemaGenerationContext);
        }
    }
}