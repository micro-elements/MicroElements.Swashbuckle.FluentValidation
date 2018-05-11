using System;
using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
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
        private readonly IValidatorFactory _validatorFactory;
        private readonly ILogger _logger;
        private readonly IReadOnlyList<FluentValidationRule> _rules;

        public FluentValidationOperationFilter(
            [CanBeNull] IValidatorFactory validatorFactory = null,
            [CanBeNull] IEnumerable<FluentValidationRule> rules = null,
            [CanBeNull] ILoggerFactory loggerFactory = null)
        {
            _validatorFactory = validatorFactory;
            _logger = loggerFactory?.CreateLogger(typeof(FluentValidationRules));
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
                _logger?.LogWarning(0, e, $"Error on apply rules for operation '{operation.OperationId}'.");
            }
        }

        void ApplyInternal(Operation operation, OperationFilterContext context)
        {
            if (operation.Parameters == null)
                return;

            foreach (var operationParameter in operation.Parameters)
            {
                var apiParameterDescription = context.ApiDescription.ParameterDescriptions.FirstOrDefault(description =>
                    description.Name == operationParameter.Name);

                if (apiParameterDescription != null)
                {
                    var parameterType = apiParameterDescription.ModelMetadata.ContainerType;
                    if(parameterType==null)
                        continue;
                    var validator = _validatorFactory.GetValidator(parameterType);

                    var descriptor = validator.CreateDescriptor();
                    var key = apiParameterDescription.ModelMetadata.PropertyName;
                    var validatorsForMember = descriptor.GetValidatorsForMember(key);

                    Schema schema = null;
                    foreach (var propertyValidator in validatorsForMember)
                    {
                        foreach (var rule in _rules)
                        {
                            if (rule.Matches(propertyValidator))
                            {
                                try
                                {
                                    if (!context.SchemaRegistry.Definitions.TryGetValue(parameterType.Name, out schema))
                                        schema = context.SchemaRegistry.GetOrRegister(parameterType);

                                    rule.Apply(new RuleContext(schema, new SchemaFilterContext(parameterType, null, context.SchemaRegistry), key.ToLowerCamelCase(), propertyValidator));
                                }
                                catch (Exception e)
                                {
                                    _logger?.LogWarning(0, e, $"Error on apply rule '{rule.Name}' for key '{key}'.");
                                }
                            }
                        }
                    }

                    if (schema?.Required != null)
                        operationParameter.Required = schema.Required.Contains(key.ToLowerCamelCase());
                    if (schema !=null)
                    {
                        if (operationParameter is PartialSchema partialSchema)
                        {
                            if (schema.Properties.TryGetValue(key.ToLowerCamelCase(), out var property))
                            {
                                partialSchema.MinLength = property.MinLength;
                                partialSchema.MaxLength = property.MaxLength;
                                partialSchema.Pattern = property.Pattern;
                                partialSchema.Minimum = property.Minimum;
                                partialSchema.Maximum = property.Maximum;
                                partialSchema.ExclusiveMaximum = property.ExclusiveMaximum;
                                partialSchema.ExclusiveMinimum = property.ExclusiveMinimum;
                            }
                        }
                    }
                }
            }  
        }
    }
}