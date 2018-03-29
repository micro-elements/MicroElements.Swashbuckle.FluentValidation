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
        /// <param name="rules">External Fluentvalidation rules.</param>
        public FluentValidationRules(IValidatorFactory validatorFactory, IEnumerable<FluentValidationRule> rules = null)
        {
            _validatorFactory = validatorFactory;
            _rules = CreateDefaultRules();
            if (rules != null)
                _rules = _rules.Concat(rules).ToList();
        }

        private static FluentValidationRule[] CreateDefaultRules()
        {
            return new[]
            {
                new FluentValidationRule("Required")
                {
                    Matches = propertyValidator => propertyValidator is INotNullValidator || propertyValidator is INotEmptyValidator,
                    Apply = context => context.Schema.Required.Add(context.PropertyKey)
                },
                new FluentValidationRule("MinMax")
                {
                    Matches = propertyValidator => propertyValidator is ILengthValidator,
                    Apply = context =>
                    {
                        var lengthValidator = (ILengthValidator)context.PropertyValidator;
                        if (lengthValidator.Max > 0)
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
                new FluentValidationRule("GreaterThan")
                {
                    Matches = propertyValidator => propertyValidator is IComparisonValidator,
                    Apply = context =>
                    {
                        var comparisonValidator = (IComparisonValidator)context.PropertyValidator;
                        if (comparisonValidator.Comparison == Comparison.GreaterThan)
                        {
                            //todo
                        }

                        //context.Schema.Properties[context.PropertyKey].Example
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

    public class FluentValidationRule
    {
        public string Name { get; }
        public Func<IPropertyValidator, bool> Matches { get; set; }
        public Action<RuleContext> Apply;

        public FluentValidationRule(string name)
        {
            Name = name;
        }
    }

    public class RuleContext
    {
        public Schema Schema { get; }
        public SchemaFilterContext SchemaFilterContext { get; }
        public string PropertyKey { get; }
        public IPropertyValidator PropertyValidator { get; }

        /// <inheritdoc />
        public RuleContext(Schema schema, SchemaFilterContext schemaFilterContext, string propertyKey, IPropertyValidator propertyValidator)
        {
            Schema = schema;
            SchemaFilterContext = schemaFilterContext;
            PropertyKey = propertyKey;
            PropertyValidator = propertyValidator;
        }
    }
}