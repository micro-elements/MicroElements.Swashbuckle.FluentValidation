// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
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
    /// Swagger <see cref="IOperationFilter"/> that applies FluentValidation rules 
    /// for GET parameters bounded from validatable models.
    /// </summary>
    public class FluentValidationOperationFilter : IOperationFilter
    {
        private readonly ILogger _logger;

        private readonly IValidatorRegistry? _validatorRegistry;

        private readonly IReadOnlyList<IFluentValidationRule<OpenApiSchema>> _rules;
        private readonly SchemaGenerationOptions _schemaGenerationOptions;

        /// <summary>
        /// Initializes a new instance of the <see cref="FluentValidationOperationFilter"/> class.
        /// </summary>
        /// <param name="loggerFactory"><see cref="ILoggerFactory"/> for logging. Can be null.</param>
        /// <param name="serviceProvider">Validator factory.</param>
        /// <param name="validatorRegistry">Gets validators for a particular type.</param>
        /// <param name="rules">External FluentValidation rules. External rule overrides default rule with the same name.</param>
        /// <param name="schemaGenerationOptions">Schema generation options.</param>
        /// <param name="swaggerGenOptions">SwaggerGenOptions.</param>
        public FluentValidationOperationFilter(
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
            _validatorRegistry = validatorRegistry ?? new ServiceProviderValidatorRegistry(serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider)));

            // MicroElements services
            _rules = new DefaultFluentValidationRuleProvider(schemaGenerationOptions).GetRules().ToArray().OverrideRules(rules);
            _schemaGenerationOptions = schemaGenerationOptions?.Value ?? new SchemaGenerationOptions();

            // Swashbuckle services
            _schemaGenerationOptions.FillFromSwashbuckleOptions(swaggerGenOptions);

            _schemaGenerationOptions.FillDefaultValues(serviceProvider);

            _logger.LogDebug("FluentValidationOperationFilter Created");
        }

        /// <inheritdoc />
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            try
            {
                ApplyInternal(operation, context);
            }
            catch (Exception e)
            {
                var operationId = operation.OperationId ?? context.ApiDescription.RelativePath;
                _logger.LogWarning(0, e, $"Error on apply rules for operation '{operationId}'.");
            }
        }

        private void ApplyInternal(OpenApiOperation operation, OperationFilterContext context)
        {
            if (operation.Parameters == null)
                return;

            if (_validatorRegistry == null)
            {
                _logger.LogWarning(0, "ValidatorFactory is not provided. Please register FluentValidation.");
                return;
            }

            var schemaProvider = new SwashbuckleSchemaProvider(context.SchemaRepository, context.SchemaGenerator, _schemaGenerationOptions.SchemaIdSelector);

            foreach (var operationParameter in operation.Parameters)
            {
                var apiParameterDescription = context.ApiDescription.ParameterDescriptions.FirstOrDefault(description =>
                    description.Name.Equals(operationParameter.Name, StringComparison.InvariantCultureIgnoreCase));

                var modelMetadata = apiParameterDescription?.ModelMetadata;
                if (modelMetadata != null)
                {
                    var parameterType = modelMetadata.ContainerType;
                    if (parameterType == null)
                        continue;

                    var validator = _validatorRegistry.GetValidator(parameterType);
                    if (validator == null)
                        continue;

                    OpenApiSchema schema = schemaProvider.GetSchemaForType(parameterType);

                    if (schema.Properties != null && schema.Properties.Count > 0)
                    {
                        var schemaPropertyName = operationParameter.Name;

                        KeyValuePair<string, OpenApiSchema> apiProperty = schema.Properties.FirstOrDefault(property => property.Key.EqualsIgnoreAll(schemaPropertyName));
                        if (apiProperty.Key != null)
                        {
                            schemaPropertyName = apiProperty.Key;
                        }
                        else
                        {
                            var propertyInfo = parameterType.GetProperty(schemaPropertyName);
                            if (propertyInfo != null && _schemaGenerationOptions.NameResolver != null)
                            {
                                schemaPropertyName = _schemaGenerationOptions.NameResolver.GetPropertyName(propertyInfo);
                            }
                        }

                        var schemaContext = new SchemaGenerationContext(
                            schemaRepository: context.SchemaRepository,
                            schemaGenerator: context.SchemaGenerator,
                            schema: schema,
                            schemaType: parameterType,
                            rules: _rules,
                            schemaGenerationOptions: _schemaGenerationOptions,
                            schemaProvider: schemaProvider);

                        FluentValidationSchemaBuilder.ApplyRulesToSchema(
                            schemaType: parameterType,
                            schemaPropertyNames: new[] { schemaPropertyName },
                            validator: validator,
                            logger: _logger,
                            schemaGenerationContext: schemaContext);

                        if (schema.Required != null)
                            operationParameter.Required = schema.Required.Contains(schemaPropertyName, IgnoreAllStringComparer.Instance);

                        var parameterSchema = operationParameter.Schema;
                        if (parameterSchema != null)
                        {
                            if (schema.Properties.TryGetValue(schemaPropertyName.ToLowerCamelCase(), out var property)
                                || schema.Properties.TryGetValue(schemaPropertyName, out property))
                            {
                                // Copy from property schema to parameter schema.
                                parameterSchema.Description = property.Description;
                                parameterSchema.MinLength = property.MinLength;
                                parameterSchema.Nullable = property.Nullable;
                                parameterSchema.MaxLength = property.MaxLength;
                                parameterSchema.Pattern = property.Pattern;
                                parameterSchema.Minimum = property.Minimum;
                                parameterSchema.Maximum = property.Maximum;
                                parameterSchema.ExclusiveMaximum = property.ExclusiveMaximum;
                                parameterSchema.ExclusiveMinimum = property.ExclusiveMinimum;
                                parameterSchema.Enum = property.Enum;
                                parameterSchema.AllOf = property.AllOf;
                            }
                        }
                    }
                }
            }
        }
    }
}
