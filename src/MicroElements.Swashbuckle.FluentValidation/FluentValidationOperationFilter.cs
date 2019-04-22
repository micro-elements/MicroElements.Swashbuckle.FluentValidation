using System;
using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MicroElements.Swashbuckle.FluentValidation
{
    /// <summary>
    /// Swagger <see cref="IOperationFilter"/> that applies FluentValidation rules 
    /// for GET parameters bounded from validatable models.
    /// </summary>
    public class FluentValidationOperationFilter : IOperationFilter
    {
        private readonly SwaggerGenOptions _swaggerGenOptions;
        private readonly IValidatorFactory _validatorFactory;
        private readonly ILogger _logger;
        private readonly IReadOnlyList<FluentValidationRule> _rules;

        public FluentValidationOperationFilter(
            IOptions<SwaggerGenOptions> swaggerGenOptions,
            [CanBeNull] IValidatorFactory validatorFactory = null,
            [CanBeNull] IEnumerable<FluentValidationRule> rules = null,
            [CanBeNull] ILoggerFactory loggerFactory = null)
        {
            _swaggerGenOptions = swaggerGenOptions.Value;
            _validatorFactory = validatorFactory;
            _logger = loggerFactory?.CreateLogger(typeof(FluentValidationRules)) ?? NullLogger.Instance;
            _rules = FluentValidationRules.CreateDefaultRules();
            if (rules != null)
            {
                var ruleMap = _rules.ToDictionary(rule => rule.Name, rule => rule);
                foreach (var rule in rules)
                {
                    // Add or replace rule
                    ruleMap[rule.Name] = rule;
                }
                _rules = ruleMap.Values.ToList();
            }
        }

        /// <inheritdoc />
        public void Apply(Operation operation, OperationFilterContext context)
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

        void ApplyInternal(Operation operation, OperationFilterContext context)
        {
            if (operation.Parameters == null)
                return;

            var schemaIdSelector = _swaggerGenOptions.SchemaRegistryOptions.SchemaIdSelector ?? new SchemaRegistryOptions().SchemaIdSelector;

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

                    var key = modelMetadata.PropertyName;
                    var validatorsForMember = validator.GetValidatorsForMemberIgnoreCase(key);

                    var lazyLog = new LazyLog(_logger,
                        logger => logger.LogDebug($"Applying FluentValidation rules to swagger schema for type '{parameterType}' from operation '{operation.OperationId}'."));

                    Schema schema = null;
                    foreach (var propertyValidator in validatorsForMember)
                    {
                        foreach (var rule in _rules)
                        {
                            if (rule.Matches(propertyValidator))
                            {
                                try
                                {
                                    var schemaId = schemaIdSelector(parameterType);

                                    if (!context.SchemaRegistry.Definitions.TryGetValue(schemaId, out schema))
                                        schema = context.SchemaRegistry.GetOrRegister(parameterType);

                                    if (schema.Properties == null && context.SchemaRegistry.Definitions.ContainsKey(schemaId))
                                        schema = context.SchemaRegistry.Definitions[schemaId];

                                    if (schema.Properties != null && schema.Properties.Count > 0)
                                    {
                                        lazyLog.LogOnce();
                                        rule.Apply(new RuleContext(schema, new SchemaFilterContext(parameterType, null, context.SchemaRegistry), key.ToLowerCamelCase(), propertyValidator));
                                        _logger.LogDebug($"Rule '{rule.Name}' applied for property '{parameterType.Name}.{key}'.");
                                    }
                                    else
                                    {
                                        _logger.LogDebug($"Rule '{rule.Name}' skipped for property '{parameterType.Name}.{key}'.");
                                    }
                                }
                                catch (Exception e)
                                {
                                    _logger.LogWarning(0, e, $"Error on apply rule '{rule.Name}' for property '{parameterType.Name}.{key}'.");
                                }
                            }
                        }
                    }

                    if (schema?.Required != null)
                        operationParameter.Required = schema.Required.Contains(key, StringComparer.InvariantCultureIgnoreCase);

                    if (schema?.Properties != null)
                    {
                        var parameterSchema = operationParameter as PartialSchema;
                        if (operationParameter != null)
                        {
                            if (schema.Properties.TryGetValue(key.ToLowerCamelCase(), out var property) 
                                || schema.Properties.TryGetValue(key, out property))
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