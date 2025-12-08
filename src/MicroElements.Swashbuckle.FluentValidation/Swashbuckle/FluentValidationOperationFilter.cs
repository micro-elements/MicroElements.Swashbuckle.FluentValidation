// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using MicroElements.OpenApi;
using MicroElements.OpenApi.Core;
using MicroElements.OpenApi.FluentValidation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
#if !OPENAPI_V2
using Microsoft.OpenApi.Models;
#endif
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
        /// <param name="fluentValidationRuleProvider">Rules provider.</param>
        /// <param name="rules">External FluentValidation rules. External rule overrides default rule with the same name.</param>
        /// <param name="schemaGenerationOptions">Schema generation options.</param>
        public FluentValidationOperationFilter(
            /* System services */
            ILoggerFactory? loggerFactory = null,
            IServiceProvider? serviceProvider = null,

            /* MicroElements services */
            IValidatorRegistry? validatorRegistry = null,
            IFluentValidationRuleProvider<OpenApiSchema>? fluentValidationRuleProvider = null,
            IEnumerable<FluentValidationRule>? rules = null,
            IOptions<SchemaGenerationOptions>? schemaGenerationOptions = null)
        {
            // System services
            _logger = loggerFactory?.CreateLogger(typeof(FluentValidationRules)) ?? NullLogger.Instance;

            // FluentValidation services
            _validatorRegistry = validatorRegistry ?? new ServiceProviderValidatorRegistry(serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider)));

            // MicroElements services
            fluentValidationRuleProvider ??= new DefaultFluentValidationRuleProvider(schemaGenerationOptions);
            _rules = fluentValidationRuleProvider.GetRules().ToArray().OverrideRules(rules);
            _schemaGenerationOptions = schemaGenerationOptions?.Value ?? new SchemaGenerationOptions();

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
            if (_validatorRegistry == null)
            {
                _logger.LogWarning(0, "ValidatorFactory is not provided. Please register FluentValidation.");
                return;
            }

            var schemaProvider = new SwashbuckleSchemaProvider(context.SchemaRepository, context.SchemaGenerator, _schemaGenerationOptions.SchemaIdSelector);

            // Process operation parameters (FromQuery, FromRoute, FromHeader)
            if (operation.Parameters != null)
            {
                ApplyRulesToParameters(operation, context, schemaProvider);
            }

            // Process RequestBody for FromForm and FromBody parameters
            ApplyRulesToRequestBody(operation, context, schemaProvider);
        }

        private void ApplyRulesToParameters(OpenApiOperation operation, OperationFilterContext context, SwashbuckleSchemaProvider schemaProvider)
        {
            foreach (var operationParameter in operation.Parameters!)
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

                        var apiProperty = OpenApiSchemaCompatibility.GetProperties(schema)
                            .FirstOrDefault(property => property.Key.EqualsIgnoreAll(schemaPropertyName));
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

                        if (OpenApiSchemaCompatibility.RequiredContains(schema, schemaPropertyName))
                        {
#if OPENAPI_V2
                            // In OpenApi 2.x, IOpenApiParameter.Required is read-only
                            // We need to cast to OpenApiParameter to set it
                            if (operationParameter is OpenApiParameter openApiParameter)
                                openApiParameter.Required = true;
#else
                            operationParameter.Required = true;
#endif
                        }

                        var parameterSchema = operationParameter.Schema;
                        if (parameterSchema != null)
                        {
                            if (OpenApiSchemaCompatibility.TryGetProperty(schema, schemaPropertyName.ToLowerCamelCase(), out var property)
                                || OpenApiSchemaCompatibility.TryGetProperty(schema, schemaPropertyName, out property))
                            {
                                if (property != null)
                                {
#if OPENAPI_V2
                                    // In OpenApi 2.x, IOpenApiSchema properties are read-only
                                    // We need to cast to OpenApiSchema to set them
                                    if (parameterSchema is OpenApiSchema targetSchema)
                                    {
                                        // Copy from property schema to parameter schema.
                                        targetSchema.Description = property.Description;
                                        targetSchema.MinLength = property.MinLength;
                                        OpenApiSchemaCompatibility.SetNullable(targetSchema, OpenApiSchemaCompatibility.GetNullable(property));
                                        targetSchema.MaxLength = property.MaxLength;
                                        targetSchema.Pattern = property.Pattern;
                                        targetSchema.Minimum = property.Minimum;
                                        targetSchema.Maximum = property.Maximum;
                                        targetSchema.ExclusiveMaximum = property.ExclusiveMaximum;
                                        targetSchema.ExclusiveMinimum = property.ExclusiveMinimum;
                                        targetSchema.Enum = property.Enum;
                                        targetSchema.AllOf = property.AllOf;
                                    }
#else
                                    // Copy from property schema to parameter schema.
                                    parameterSchema.Description = property.Description;
                                    parameterSchema.MinLength = property.MinLength;
                                    OpenApiSchemaCompatibility.SetNullable(parameterSchema, OpenApiSchemaCompatibility.GetNullable(property));
                                    parameterSchema.MaxLength = property.MaxLength;
                                    parameterSchema.Pattern = property.Pattern;
                                    parameterSchema.Minimum = property.Minimum;
                                    parameterSchema.Maximum = property.Maximum;
                                    parameterSchema.ExclusiveMaximum = property.ExclusiveMaximum;
                                    parameterSchema.ExclusiveMinimum = property.ExclusiveMinimum;
                                    parameterSchema.Enum = property.Enum;
                                    parameterSchema.AllOf = property.AllOf;
#endif
                                }
                            }
                        }
                    }
                }
            }
        }

        private void ApplyRulesToRequestBody(OpenApiOperation operation, OperationFilterContext context, SwashbuckleSchemaProvider schemaProvider)
        {
#if OPENAPI_V2
            var requestBody = operation.RequestBody as OpenApiRequestBody;
#else
            var requestBody = operation.RequestBody;
#endif
            if (requestBody?.Content == null)
                return;

            // Content types used by [FromForm] attribute
            var formContentTypes = new[] { "multipart/form-data", "application/x-www-form-urlencoded" };

            foreach (var contentType in requestBody.Content)
            {
                if (!formContentTypes.Contains(contentType.Key, StringComparer.OrdinalIgnoreCase))
                    continue;

#if OPENAPI_V2
                var rawSchema = contentType.Value.Schema;
                var contentSchema = rawSchema as OpenApiSchema;
                string? schemaRefId = rawSchema is OpenApiSchemaReference schemaRef ? schemaRef.Reference?.Id : null;
#else
                var contentSchema = contentType.Value.Schema;
                string? schemaRefId = contentSchema?.Reference?.Id;
#endif
                if (contentSchema == null)
                    continue;

                // Find the parameter type from ApiDescription
                var bodyParameter = context.ApiDescription.ParameterDescriptions
                    .FirstOrDefault(p => p.Source?.Id == "Form" || p.Source?.Id == "Body");

                Type? parameterType = null;
                if (bodyParameter != null)
                {
                    parameterType = bodyParameter.ModelMetadata?.ContainerType ?? bodyParameter.ModelMetadata?.ModelType;
                }

                // If we couldn't find it from body parameter, try to find from schema reference
                if (parameterType == null && schemaRefId != null)
                {
                    parameterType = context.ApiDescription.ParameterDescriptions
                        .Select(p => p.ModelMetadata?.ModelType)
                        .FirstOrDefault(t => t != null && _schemaGenerationOptions.SchemaIdSelector(t) == schemaRefId);
                }

                if (parameterType == null)
                    continue;

                var validator = _validatorRegistry!.GetValidator(parameterType);
                if (validator == null)
                    continue;

                // Resolve the actual schema (dereference if needed)
                OpenApiSchema resolvedSchema = contentSchema;
                if (schemaRefId != null)
                {
                    resolvedSchema = schemaProvider.GetSchemaForType(parameterType);
                }

                if (resolvedSchema.Properties == null || resolvedSchema.Properties.Count == 0)
                    continue;

                var schemaContext = new SchemaGenerationContext(
                    schemaRepository: context.SchemaRepository,
                    schemaGenerator: context.SchemaGenerator,
                    schema: resolvedSchema,
                    schemaType: parameterType,
                    rules: _rules,
                    schemaGenerationOptions: _schemaGenerationOptions,
                    schemaProvider: schemaProvider);

                // Apply validation rules to all properties
                FluentValidationSchemaBuilder.ApplyRulesToSchema(
                    schemaType: parameterType,
                    schemaPropertyNames: schemaContext.Properties,
                    validator: validator,
                    logger: _logger,
                    schemaGenerationContext: schemaContext);
            }
        }
    }
}
