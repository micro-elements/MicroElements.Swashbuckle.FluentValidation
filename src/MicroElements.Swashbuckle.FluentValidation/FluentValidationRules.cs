using System;
using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using FluentValidation.Validators;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MicroElements.Swashbuckle.FluentValidation
{
    /// <summary>
    /// Swagger <see cref="ISchemaFilter"/> that uses FluentValidation validators instead System.ComponentModel based attributes.
    /// </summary>
    public class FluentValidationRules : ISchemaFilter
    {
        private readonly IValidatorFactory _validatorFactory;
        private readonly IReadOnlyList<FluentValidationRule> _rules;

        /// <summary>
        /// Creates new instance of <see cref="FluentValidationRules"/>
        /// </summary>
        /// <param name="validatorFactory">Validator factory.</param>
        /// <param name="rules">External Fluentvalidation rules. Rule with the same name replaces default rule.</param>
        public FluentValidationRules(IValidatorFactory validatorFactory, IEnumerable<FluentValidationRule> rules = null)
        {
            _validatorFactory = validatorFactory;
            _rules = CreateDefaultRules();
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

        public static FluentValidationRule[] CreateDefaultRules()
        {
            return new[]
            {
                new FluentValidationRule("Required")
                {
                    Matches = propertyValidator => propertyValidator is INotNullValidator || propertyValidator is INotEmptyValidator,
                    Apply = context =>
                    {
                        context.Schema.Required.Add(context.PropertyKey);
                    }
                },
                new FluentValidationRule("NotEmpty")
                {
                    Matches = propertyValidator => propertyValidator is INotEmptyValidator,
                    Apply = context =>
                    {
                        context.Schema.Properties[context.PropertyKey].MinLength = 1;
                    }
                },
                new FluentValidationRule("Length")
                {
                    Matches = propertyValidator => propertyValidator is ILengthValidator,
                    Apply = context =>
                    {
                        var lengthValidator = (ILengthValidator)context.PropertyValidator;
                        if(lengthValidator.Max > 0)
                            context.Schema.Properties[context.PropertyKey].MaxLength = lengthValidator.Max;
                        context.Schema.Properties[context.PropertyKey].MinLength = lengthValidator.Min;
                    }
                },
                new FluentValidationRule("Pattern")
                {
                    Matches = propertyValidator => propertyValidator is IRegularExpressionValidator,
                    Apply = context =>
                    {
                        var regularExpressionValidator = (IRegularExpressionValidator)context.PropertyValidator;
                        context.Schema.Properties[context.PropertyKey].Pattern = regularExpressionValidator.Expression;
                    }
                },
                new FluentValidationRule("Comparison")
                {
                    Matches = propertyValidator => propertyValidator is IComparisonValidator,
                    Apply = context =>
                    {
                        var comparisonValidator = (IComparisonValidator)context.PropertyValidator;
                        if (comparisonValidator.ValueToCompare is int)
                        {
                            int valueToCompare = (int)comparisonValidator.ValueToCompare;
                            var schemaProperty = context.Schema.Properties[context.PropertyKey];

                            if (comparisonValidator.Comparison == Comparison.GreaterThanOrEqual)
                            {
                                schemaProperty.Minimum = valueToCompare;
                            }
                            else if (comparisonValidator.Comparison == Comparison.GreaterThan)
                            {
                                schemaProperty.Minimum = valueToCompare;
                                schemaProperty.ExclusiveMinimum = true;
                            }
                            else if (comparisonValidator.Comparison == Comparison.LessThanOrEqual)
                            {
                                schemaProperty.Maximum = valueToCompare;
                            }
                            else if (comparisonValidator.Comparison == Comparison.LessThan)
                            {
                                schemaProperty.Maximum = valueToCompare;
                                schemaProperty.ExclusiveMaximum = true;
                            }
                        }
                    }
                },
                new FluentValidationRule("Between")
                {
                    Matches = propertyValidator => propertyValidator is IBetweenValidator,
                    Apply = context =>
                    {
                        var betweenValidator = (IBetweenValidator)context.PropertyValidator;
                        var schemaProperty = context.Schema.Properties[context.PropertyKey];

                        if (betweenValidator.From is int && betweenValidator.To is int)
                        {
                            schemaProperty.Minimum = (int)betweenValidator.From;
                            schemaProperty.Maximum = (int)betweenValidator.To;

                            if (betweenValidator is ExclusiveBetweenValidator)
                            {
                                schemaProperty.ExclusiveMinimum = true;
                                schemaProperty.ExclusiveMaximum = true;
                            }
                        }
                    }
                },
            };
        }

        /// <inheritdoc />
        public void Apply(Schema schema, SchemaFilterContext context)
        {
            var validator = _validatorFactory.GetValidator(context.SystemType);
            if (validator == null)
                return;

            if (schema.Required == null)
                schema.Required = new List<string>();

            var validatorDescriptor = validator.CreateDescriptor();

            foreach (var key in schema.Properties.Keys)
            {
                foreach (var propertyValidator in validatorDescriptor.GetValidatorsForMember(ToCamelCase(key)))
                {
                    foreach (var rule in _rules)
                    {
                        if (rule.Matches(propertyValidator))
                        {
                            try
                            {
                                rule.Apply(new RuleContext(schema, context, key, propertyValidator));
                            }
                            catch (Exception e)
                            {
                                //todo: logger
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Converts string to CamelCase to match .net standard naming conventions.
        /// </summary>
        /// <param name="inputString">Input string.</param>
        /// <returns>CamelCase variant of input string.</returns>
        private static string ToCamelCase(string inputString)
        {
            if (inputString == null) return null;
            if (inputString.Length < 2) return inputString.ToUpper();
            return inputString.Substring(0, 1).ToUpper() + inputString.Substring(1);
        }
    }

    /// <summary>
    /// FluentValidationRule.
    /// </summary>
    public class FluentValidationRule
    {
        /// <summary>
        /// Rule name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Predicate to match property validator.
        /// </summary>
        public Func<IPropertyValidator, bool> Matches { get; set; }

        /// <summary>
        /// Modify Swagger schema action.
        /// </summary>
        public Action<RuleContext> Apply { get; set; }

        /// <summary>
        /// Creates new instance of <see cref="FluentValidationRule"/>.
        /// </summary>
        /// <param name="name">Rule name.</param>
        public FluentValidationRule(string name)
        {
            Name = name;
        }
    }

    /// <summary>
    /// RuleContext.
    /// </summary>
    public class RuleContext
    {
        /// <summary>
        /// Swagger schema.
        /// </summary>
        public Schema Schema { get; }

        /// <summary>
        /// SchemaFilterContext.
        /// </summary>
        public SchemaFilterContext SchemaFilterContext { get; }

        /// <summary>
        /// Property name.
        /// </summary>
        public string PropertyKey { get; }

        /// <summary>
        /// Property validator.
        /// </summary>
        public IPropertyValidator PropertyValidator { get; }

        /// <summary>
        /// Creates new instance of <see cref="RuleContext"/>.
        /// </summary>
        /// <param name="schema">Swagger schema.</param>
        /// <param name="schemaFilterContext">SchemaFilterContext.</param>
        /// <param name="propertyKey">Property name.</param>
        /// <param name="propertyValidator">Property validator.</param>
        public RuleContext(Schema schema, SchemaFilterContext schemaFilterContext, string propertyKey, IPropertyValidator propertyValidator)
        {
            Schema = schema;
            SchemaFilterContext = schemaFilterContext;
            PropertyKey = propertyKey;
            PropertyValidator = propertyValidator;
        }
    }
}