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

        /// <summary>
        /// Initializes a new instance of the <see cref="FluentValidationRules"/> class.
        /// </summary>
        /// <param name="validatorFactory">Validator factory.</param>
        /// <param name="rules">External FluentValidation rules. External rule overrides default rule with the same name.</param>
        /// <param name="loggerFactory"><see cref="ILoggerFactory"/> for logging. Can be null.</param>
        /// <param name="schemaGenerationOptions">Schema generation options.</param>
        public FluentValidationRules(
            IValidatorFactory? validatorFactory = null,
            IEnumerable<FluentValidationRule>? rules = null,
            ILoggerFactory? loggerFactory = null,
            IOptions<SchemaGenerationOptions>? schemaGenerationOptions = null)
        {
            _validatorFactory = validatorFactory;
            _schemaGenerationOptions = schemaGenerationOptions?.Value ?? new SchemaGenerationOptions();
            _logger = loggerFactory?.CreateLogger(typeof(FluentValidationRules)) ?? NullLogger.Instance;
            _rules = new DefaultFluentValidationRuleProvider(schemaGenerationOptions).GetRules().ToArray().OverrideRules(rules);
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

            var schemaContext = new SchemaGenerationContext
            {
                Schema = schema,
                SchemaType = context.Type,
                Rules = _rules,
                SchemaGenerationOptions = _schemaGenerationOptions,
                ReflectionContext = new ReflectionContext(type: context.Type, propertyInfo: context.MemberInfo, parameterInfo: context.ParameterInfo),
                SchemaProvider = new SwashbuckleSchemaProvider(context.SchemaRepository, context.SchemaGenerator),
            };

            if (context.MemberInfo != null || context.ParameterInfo != null)
            {
                int i = 0;
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
                schemaGenerationContext: schemaGenerationContext);
        }

        private void AddRulesFromIncludedValidators(SchemaFilterContext context, IValidator validator, SchemaGenerationContext schemaGenerationContext)
        {
            FluentValidationSchemaBuilder.AddRulesFromIncludedValidators(
                validator: validator,
                logger: _logger,
                schemaGenerationContext: schemaGenerationContext);
        }
    }
}