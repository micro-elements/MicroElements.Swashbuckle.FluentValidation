using System;
using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using JetBrains.Annotations;
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
        private readonly SwaggerGenOptions _swaggerGenOptions;
        private readonly IValidatorFactory _validatorFactory;
        private readonly IApiModelResolver _apiModelResolver;
        private readonly IReadOnlyList<FluentValidationRule> _rules;

        public FluentValidationOperationFilter(
            IOptions<SwaggerGenOptions> swaggerGenOptions,
            [CanBeNull] IValidatorFactory validatorFactory = null,
            [CanBeNull] IEnumerable<FluentValidationRule> rules = null,
            [CanBeNull] ILoggerFactory loggerFactory = null,
            [CanBeNull] IApiModelResolver apiModelResolver = null)
        {
            _swaggerGenOptions = swaggerGenOptions.Value;
            _validatorFactory = validatorFactory;
            _apiModelResolver = apiModelResolver;
            _logger = loggerFactory?.CreateLogger(typeof(FluentValidationRules)) ?? NullLogger.Instance;
            _rules = FluentValidationRules.CreateDefaultRules().OverrideRules(rules);
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
                _logger.LogWarning(0, e, $"Error on apply rules for operation '{operation.OperationId}'.");
            }
        }

        void ApplyInternal(OpenApiOperation operation, OperationFilterContext context)
        {
            if (operation.Parameters == null)
                return;

            var schemaIdSelector = _swaggerGenOptions.SchemaGeneratorOptions.SchemaIdSelector ?? new SchemaGeneratorOptions().SchemaIdSelector;

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
                    var validator = _validatorFactory.GetValidator(parameterType);
                    if (validator == null)
                        continue;

                    var schemaPropertyName = operationParameter.Name;
                    if (_apiModelResolver.ResolveApiModelFor(parameterType) is ApiObject apiObject)
                    {
                        var apiProperty = apiObject.ApiProperties.FirstOrDefault(property =>
                            string.Equals(property.ApiName, schemaPropertyName, StringComparison.OrdinalIgnoreCase));
                        if (apiProperty != null)
                            schemaPropertyName = apiProperty.ApiName;
                    }

                    var validatorsForMember = validator.GetValidatorsForMemberIgnoreCase(schemaPropertyName);

                    var lazyLog = new LazyLog(_logger,
                        logger => logger.LogDebug($"Applying FluentValidation rules to swagger schema for type '{parameterType}' from operation '{operation.OperationId}'."));

                    OpenApiSchema schema = null;
                    foreach (var propertyValidator in validatorsForMember)
                    {
                        foreach (var rule in _rules)
                        {
                            if (rule.Matches(propertyValidator))
                            {
                                try
                                {
                                    var schemaId = schemaIdSelector(parameterType);

                                    if (!context.SchemaRepository.Schemas.TryGetValue(schemaId, out schema))
                                    {
                                        schema = context.SchemaGenerator.GenerateSchema(parameterType, context.SchemaRepository);
                                    }

                                    if ((schema.Properties == null || schema.Properties.Count == 0) && context.SchemaRepository.Schemas.ContainsKey(schemaId))
                                    {
                                        schema = context.SchemaRepository.Schemas[schemaId];
                                    }

                                    if (schema.Properties != null && schema.Properties.Count > 0)
                                    {
                                        lazyLog.LogOnce();
                                        var schemaFilterContext = new SchemaFilterContext(new ApiModel(parameterType), context.SchemaRepository, context.SchemaGenerator);
                                        rule.Apply(new RuleContext(schema, schemaFilterContext, schemaPropertyName, propertyValidator));
                                        _logger.LogDebug($"Rule '{rule.Name}' applied for property '{parameterType.Name}.{operationParameter.Name}'.");
                                    }
                                    else
                                    {
                                        _logger.LogDebug($"Rule '{rule.Name}' skipped for property '{parameterType.Name}.{operationParameter.Name}'.");
                                    }
                                }
                                catch (Exception e)
                                {
                                    _logger.LogWarning(0, e, $"Error on apply rule '{rule.Name}' for property '{parameterType.Name}.{schemaPropertyName}'.");
                                }
                            }
                        }
                    }

                    if (schema?.Required != null)
                        operationParameter.Required = schema.Required.Contains(schemaPropertyName, StringComparer.InvariantCultureIgnoreCase);

                    if (schema?.Properties != null)
                    {
                        var parameterSchema = operationParameter.Schema;
                        if (parameterSchema != null)
                        {
                            if (schema.Properties.TryGetValue(schemaPropertyName.ToLowerCamelCase(), out var property)
                                || schema.Properties.TryGetValue(schemaPropertyName, out property))
                            {
                                parameterSchema.MinLength = property.MinLength;
                                parameterSchema.MaxLength = property.MaxLength;
                                parameterSchema.Pattern = property.Pattern;
                                parameterSchema.Minimum = property.Minimum;
                                parameterSchema.Maximum = property.Maximum;
                                parameterSchema.ExclusiveMaximum = property.ExclusiveMaximum;
                                parameterSchema.ExclusiveMinimum = property.ExclusiveMinimum;
                            }
                        }
                    }
                }
            }  
        }
    }
}