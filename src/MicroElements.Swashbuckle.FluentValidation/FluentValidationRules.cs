using System;
using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using FluentValidation.Internal;
using FluentValidation.Validators;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MicroElements.Swashbuckle.FluentValidation
{
    /// <summary>
    /// Swagger <see cref="ISchemaFilter"/> that uses FluentValidation validators instead System.ComponentModel based attributes.
    /// </summary>
    public class FluentValidationRules : ISchemaFilter
    {
        private readonly IValidatorFactory _validatorFactory;
        private readonly ILogger _logger;
        private readonly IReadOnlyList<FluentValidationRule> _rules;

        /// <summary>
        /// Creates new instance of <see cref="FluentValidationRules"/>
        /// </summary>
        /// <param name="validatorFactory">Validator factory.</param>
        /// <param name="rules">External FluentValidation rules. Rule with the same name replaces default rule.</param>
        /// <param name="loggerFactory"><see cref="ILoggerFactory"/> for logging. Can be null.</param>
        public FluentValidationRules(
            [CanBeNull] IValidatorFactory validatorFactory = null,
            [CanBeNull] IEnumerable<FluentValidationRule> rules = null,
            [CanBeNull] ILoggerFactory loggerFactory = null)
        {
            _validatorFactory = validatorFactory;
            _logger = loggerFactory?.CreateLogger(typeof(FluentValidationRules)) ?? NullLogger.Instance;
            _rules = CreateDefaultRules().OverrideRules(rules);
        }

        /// <inheritdoc />
        public void Apply(OpenApiSchema schema, SchemaFilterContext context)
        {
            if (_validatorFactory == null)
            {
                _logger.LogWarning(0, "ValidatorFactory is not provided. Please register FluentValidation.");
                return;
            }

            IValidator validator = null;
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

            ApplyRulesToSchema(schema, context, validator);

            try
            {
                AddRulesFromIncludedValidators(schema, context, validator);
            }
            catch (Exception e)
            {
                _logger.LogWarning(0, e, $"Applying IncludeRules for type '{context.Type}' fails.");
            }
        }

        private void ApplyRulesToSchema(OpenApiSchema schema, SchemaFilterContext context, IValidator validator)
        {
            var lazyLog = new LazyLog(_logger,
                logger => logger.LogDebug($"Applying FluentValidation rules to swagger schema for type '{context.Type}'."));

            foreach (var schemaPropertyName in schema?.Properties?.Keys ?? Array.Empty<string>())
            {
                var validationRules = validator.GetValidationRulesForMemberIgnoreCase(schemaPropertyName).ToArrayDebug();

                foreach (var ruleContext in validationRules)
                {
                    foreach (var propertyValidator in ruleContext.PropertyRule.Validators)
                    {
                        foreach (var rule in _rules)
                        {
                            if (rule.Matches(propertyValidator))
                            {
                                try
                                {
                                    lazyLog.LogOnce();

                                    rule.Apply(new RuleContext(schema, schemaPropertyName, propertyValidator, context, isCollectionValidator: ruleContext.IsCollectionRule));
                                    _logger.LogDebug($"Rule '{rule.Name}' applied for property '{context.Type.Name}.{schemaPropertyName}'");
                                }
                                catch (Exception e)
                                {
                                    _logger.LogWarning(0, e, $"Error on apply rule '{rule.Name}' for property '{context.Type.Name}.{schemaPropertyName}'.");
                                }
                            }
                        }
                    }
                }
            }
        }

        private void AddRulesFromIncludedValidators(OpenApiSchema schema, SchemaFilterContext context, IValidator validator)
        {
            // Note: IValidatorDescriptor doesn't return IncludeRules so we need to get validators manually.
            var childAdapters = (validator as IEnumerable<IValidationRule>)
                .NotNull()
                .Where(rule => rule is IIncludeRule)
                .OfType<PropertyRule>()
                .Where(includeRule => includeRule.HasNoCondition())
                .SelectMany(includeRule => includeRule.Validators)
                .OfType<IChildValidatorAdaptor>();

            foreach (var childAdapter in childAdapters)
            {
                IValidator includedValidator = GetValidatorFromChildValidatorAdapter(childAdapter);
                if (includedValidator != null)
                {
                    ApplyRulesToSchema(schema, context, includedValidator);
                    AddRulesFromIncludedValidators(schema, context, includedValidator);
                }
            }
        }

        private IValidator GetValidatorFromChildValidatorAdapter(IChildValidatorAdaptor childValidatorAdapter)
        {
            // Fake context. We have not got real context because no validation yet. 
            var fakeContext = new PropertyValidatorContext(new ValidationContext<object>(null), null, string.Empty);

            // Try to validator with reflection. 
            var childValidatorAdapterType = childValidatorAdapter.GetType();
            var getValidatorMethod = childValidatorAdapterType.GetMethod(nameof(ChildValidatorAdaptor<object, object>.GetValidator));
            if (getValidatorMethod != null)
            {
                var validator = (IValidator)getValidatorMethod.Invoke(childValidatorAdapter, new[] { fakeContext });
                return validator;
            }

            return null;
        }

        /// <summary>
        /// Creates default rules.
        /// Can be overriden by name.
        /// </summary>
        public static FluentValidationRule[] CreateDefaultRules()
        {
            return new[]
            {
                new FluentValidationRule("Required")
                {
                    Matches = propertyValidator => (propertyValidator is INotNullValidator || propertyValidator is INotEmptyValidator) && propertyValidator.HasNoCondition(),
                    Apply = context =>
                    {
                        if (context.Schema.Required == null)
                            context.Schema.Required = new SortedSet<string>();
                        if(!context.Schema.Required.Contains(context.PropertyKey))
                            context.Schema.Required.Add(context.PropertyKey);
                        context.Schema.Properties[context.PropertyKey].Nullable = false;
                    }
                },
                new FluentValidationRule("NotEmpty")
                {
                    Matches = propertyValidator => propertyValidator is INotEmptyValidator && propertyValidator.HasNoCondition(),
                    Apply = context =>
                    {
                        var schemaProperty = context.Schema.Properties[context.PropertyKey];
                        schemaProperty.SetNewMin(p => p.MinLength, 1);
                        schemaProperty.SetNotNullableIfMinLengthGreaterThenZero();
                    }
                },
                new FluentValidationRule("Length")
                {
                    Matches = propertyValidator => propertyValidator is ILengthValidator && propertyValidator.HasNoCondition(),
                    Apply = context =>
                    {
                        var lengthValidator = (ILengthValidator)context.PropertyValidator;
                        var schemaProperty = context.Schema.Properties[context.PropertyKey];

                        if (lengthValidator.Max > 0)
                            schemaProperty.SetNewMax(p => p.MaxLength, lengthValidator.Max);

                        if (lengthValidator.Min > 0)
                            schemaProperty.SetNewMin(p => p.MinLength, lengthValidator.Min);

                        schemaProperty.SetNotNullableIfMinLengthGreaterThenZero();
                    }
                },
                new FluentValidationRule("Pattern")
                {
                    Matches = propertyValidator => propertyValidator is IRegularExpressionValidator && propertyValidator.HasNoCondition(),
                    Apply = context =>
                    {
                        var regularExpressionValidator = (IRegularExpressionValidator)context.PropertyValidator;
                        context.Schema.Properties[context.PropertyKey].Pattern = regularExpressionValidator.Expression;
                    }
                },
                new FluentValidationRule("EMail")
                {
                    Matches = propertyValidator => propertyValidator.GetType().Name.Contains("EmailValidator") && propertyValidator.HasNoCondition(),
                    Apply = context =>
                    {
                        context.Property.Format = "email";
                    }
                },
                new FluentValidationRule("Comparison")
                {
                    Matches = propertyValidator => propertyValidator is IComparisonValidator && propertyValidator.HasNoCondition(),
                    Apply = context =>
                    {
                        var comparisonValidator = (IComparisonValidator)context.PropertyValidator;
                        if (comparisonValidator.ValueToCompare.IsNumeric())
                        {
                            var valueToCompare = comparisonValidator.ValueToCompare.NumericToDecimal();
                            var schemaProperty = context.Schema.Properties[context.PropertyKey];

                            if (comparisonValidator.Comparison == Comparison.GreaterThanOrEqual)
                            {
                                schemaProperty.SetNewMin(p => p.Minimum, valueToCompare);
                            }
                            else if (comparisonValidator.Comparison == Comparison.GreaterThan)
                            {
                                schemaProperty.SetNewMin(p => p.Minimum, valueToCompare);
                                schemaProperty.ExclusiveMinimum = true;
                            }
                            else if (comparisonValidator.Comparison == Comparison.LessThanOrEqual)
                            {
                                schemaProperty.SetNewMax(p => p.Maximum, valueToCompare);
                            }
                            else if (comparisonValidator.Comparison == Comparison.LessThan)
                            {
                                schemaProperty.SetNewMax(p => p.Maximum, valueToCompare);
                                schemaProperty.ExclusiveMaximum = true;
                            }
                        }
                    }
                },
                new FluentValidationRule("Between")
                {
                    Matches = propertyValidator => propertyValidator is IBetweenValidator && propertyValidator.HasNoCondition(),
                    Apply = context =>
                    {
                        var betweenValidator = (IBetweenValidator)context.PropertyValidator;
                        var schemaProperty = context.Schema.Properties[context.PropertyKey];

                        if (betweenValidator.From.IsNumeric())
                        {
                            schemaProperty.SetNewMin(p => p.Minimum, betweenValidator.From.NumericToDecimal());

                            if (betweenValidator is ExclusiveBetweenValidator)
                            {
                                schemaProperty.ExclusiveMinimum = true;
                            }
                        }

                        if (betweenValidator.To.IsNumeric())
                        {
                            schemaProperty.SetNewMax(p => p.Maximum, betweenValidator.To.NumericToDecimal());

                            if (betweenValidator is ExclusiveBetweenValidator)
                            {
                                schemaProperty.ExclusiveMaximum = true;
                            }
                        }
                    }
                },

            };
        }
    }
}