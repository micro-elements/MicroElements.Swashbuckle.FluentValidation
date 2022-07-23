using System;
using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using MicroElements.OpenApi.Core;
using MicroElements.OpenApi.FluentValidation;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MicroElements.Swashbuckle.FluentValidation
{
    /// <summary>
    /// Experimental document filter.
    /// </summary>
    public class FluentValidationDocumentFilter : IDocumentFilter
    {
        private readonly ILogger _logger;

        private readonly IValidatorRegistry? _validatorRegistry;

        private readonly IReadOnlyList<IFluentValidationRule<OpenApiSchema>> _rules;
        private readonly ISchemaGenerationOptions _schemaGenerationOptions;
        private readonly SchemaGenerationSettings _schemaGenerationSettings;

        public FluentValidationDocumentFilter(
            /* System services */
            ILoggerFactory? loggerFactory = null,
            IServiceProvider? serviceProvider = null,

            /* MicroElements services */
            IValidatorRegistry? validatorRegistry = null,
            IEnumerable<FluentValidationRule>? rules = null,
            IOptions<SchemaGenerationOptions>? schemaGenerationOptions = null,
            INameResolver? nameResolver = null,

            /* Swashbuckle services */
            IOptions<SwaggerGenOptions>? swaggerGenOptions = null)
        {
            // System services
            _logger = loggerFactory?.CreateLogger(typeof(FluentValidationRules)) ?? NullLogger.Instance;

            _logger.LogDebug("FluentValidationRules Created");

            // FluentValidation services
            _validatorRegistry = validatorRegistry;

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

        record SchemaItem
        {
            public Type ModelType { get; init; }
            public string SchemaName { get; init; }
            public OpenApiSchema Schema { get; init; }
        };

        record ParameterItem
        {
            public ApiDescription ApiDescription { get; init; }
            public ApiParameterDescription ParameterDescription { get; init; }
            public Type ModelType { get; init; }
            public string SchemaName { get; init; }
            public OpenApiSchema Schema { get; init; }
            public OpenApiSchema ParameterSchema { get; init; }
        };

        /// <inheritdoc />
        public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
        {
            var schemaRepositorySchemas = context.SchemaRepository.Schemas;
            var schemaIdSelector = _schemaGenerationSettings.SchemaIdSelector;
            var schemaProvider = new SwashbuckleSchemaProvider(context.SchemaRepository, context.SchemaGenerator, _schemaGenerationSettings.SchemaIdSelector);

            var apiDescriptions = context.ApiDescriptions.ToArray();

            var modelTypes = apiDescriptions
                .SelectMany(description => description.ParameterDescriptions)
                .Where(description => description.ModelMetadata.ContainerType is null)
                .Select(description => description.ModelMetadata.ModelType)
                .Distinct();

            var containerTypes = apiDescriptions
                .SelectMany(description => description.ParameterDescriptions)
                .Where(description => description.ModelMetadata.ContainerType != null)
                .Select(description => description.ModelMetadata.ContainerType)
                .Distinct();

            var schemasForTypes = modelTypes
                .Concat(containerTypes)
                .Distinct()
                .Select(modelType => new SchemaItem { ModelType = modelType })
                .Select(item => item with { SchemaName = schemaIdSelector.Invoke(item.ModelType) })
                .Select(item => item with { Schema = schemaProvider.GetSchemaForType(item.ModelType) })
                .ToArray();

            var schemasForParameters = apiDescriptions
                .SelectMany(description => description.ParameterDescriptions)
                .Where(description => description.ModelMetadata.ContainerType != null)
                .Select(description => new ParameterItem { ParameterDescription = description, ModelType = description.ModelMetadata.ContainerType })
                .Select(item => item with { SchemaName = schemaIdSelector.Invoke(item.ModelType) })
                .Select(item => item with { Schema = schemaProvider.GetSchemaForType(item.ModelType) })
                .ToArray();

            IEnumerable<ParameterItem> GetParameters()
            {
                foreach (var apiDescription in apiDescriptions)
                {
                    foreach (var apiParameterDescription in apiDescription.ParameterDescriptions)
                    {
                        var containerType = apiParameterDescription.ModelMetadata.ContainerType;
                        if (containerType != null)
                        {
                            var parameterItem = new ParameterItem
                            {
                                ApiDescription = apiDescription,
                                ParameterDescription = apiParameterDescription,
                                ModelType = containerType,
                                SchemaName = schemaIdSelector.Invoke(containerType),
                                Schema = schemaProvider.GetSchemaForType(containerType),
                            };

                            var parameterSchema = FindParam(parameterItem);
                            parameterItem = parameterItem with { ParameterSchema = parameterSchema };

                            yield return parameterItem;
                        }
                    }
                }
            }

            schemasForParameters = GetParameters().ToArray();

            OpenApiSchema FindParam(ParameterItem item)
            {
                //return many?
                var path = swaggerDoc.Paths.FirstOrDefault(pair => pair.Key.TrimStart('/') == item.ApiDescription.RelativePath);
                var openApiParameter = path.Value.Operations.Values.FirstOrDefault().Parameters.FirstOrDefault(parameter => parameter.Name == item.ParameterDescription.Name);
                return openApiParameter?.Schema;
            }

            foreach (var item in schemasForTypes)
            {
                IValidator? validator = null;
                try
                {
                    validator = _validatorRegistry.GetValidator(item.ModelType);
                }
                catch (Exception e)
                {
                    _logger.LogWarning(0, e, $"GetValidator for type '{item.ModelType}' fails.");
                }

                if (validator == null)
                    continue;

                var schemaContext = new SchemaGenerationContext(
                    schemaRepository: context.SchemaRepository,
                    schemaGenerator: context.SchemaGenerator,
                    schema: item.Schema,
                    schemaType: item.ModelType,
                    rules: _rules,
                    schemaGenerationOptions: _schemaGenerationOptions,
                    schemaGenerationSettings: _schemaGenerationSettings);

                ApplyRulesToSchema(schemaContext, validator);

                try
                {
                    AddRulesFromIncludedValidators(schemaContext, validator);
                }
                catch (Exception e)
                {
                    _logger.LogWarning(0, e, $"Applying IncludeRules for type '{item.ModelType}' fails.");
                }
            }

            foreach (var item in schemasForParameters)
            {
                var itemParameterDescription = item.ParameterDescription;
                var schemaPropertyName = itemParameterDescription.ModelMetadata.BinderModelName ?? itemParameterDescription.Name;
                var parameterSchema = item.ParameterSchema;
                var schema = item.Schema;
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