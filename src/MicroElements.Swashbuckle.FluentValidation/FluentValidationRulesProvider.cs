// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using FluentValidation.Validators;

namespace MicroElements.Swashbuckle.FluentValidation
{
    /// <summary>
    /// Default implementation of <see cref="IFluentValidationRulesProvider"/>.
    /// </summary>
    public class FluentValidationRulesProvider : IFluentValidationRulesProvider
    {
        /// <summary>
        /// Default rules provider.
        /// </summary>
        public static readonly IFluentValidationRulesProvider Instance = new FluentValidationRulesProvider();

        /// <inheritdoc/>
        public IEnumerable<FluentValidationRule> GetRules()
        {
            yield return new FluentValidationRule("Required")
            {
                Matches = propertyValidator => (propertyValidator is INotNullValidator || propertyValidator is INotEmptyValidator) && propertyValidator.HasNoCondition(),
                Apply = context =>
                {
                    if (context.Schema.Required == null)
                        context.Schema.Required = new SortedSet<string>();
                    if (!context.Schema.Required.Contains(context.PropertyKey))
                        context.Schema.Required.Add(context.PropertyKey);
                },
            };

            yield return new FluentValidationRule("NotNull")
            {
                Matches = propertyValidator => propertyValidator is INotNullValidator && propertyValidator.HasNoCondition(),
                Apply = context =>
                {
                    var schemaProperty = context.Schema.Properties[context.PropertyKey];
                    schemaProperty.Nullable = false;
                },
            };

            yield return new FluentValidationRule("NotEmpty")
            {
                Matches = propertyValidator => propertyValidator is INotEmptyValidator && propertyValidator.HasNoCondition(),
                Apply = context =>
                {
                    var schemaProperty = context.Schema.Properties[context.PropertyKey];
                    schemaProperty.SetNewMin(p => p.MinLength, 1);
                    schemaProperty.SetNotNullableIfMinLengthGreaterThenZero();
                },
            };

            yield return new FluentValidationRule("Length")
            {
                Matches = propertyValidator => propertyValidator is ILengthValidator && propertyValidator.HasNoCondition(),
                Apply = context =>
                {
                    var lengthValidator = (ILengthValidator) context.PropertyValidator;
                    var schemaProperty = context.Schema.Properties[context.PropertyKey];

                    if (lengthValidator.Max > 0)
                        schemaProperty.SetNewMax(p => p.MaxLength, lengthValidator.Max);

                    if (lengthValidator.Min > 0)
                        schemaProperty.SetNewMin(p => p.MinLength, lengthValidator.Min);

                    schemaProperty.SetNotNullableIfMinLengthGreaterThenZero();
                },
            };

            yield return new FluentValidationRule("Pattern")
            {
                Matches = propertyValidator => propertyValidator is IRegularExpressionValidator && propertyValidator.HasNoCondition(),
                Apply = context =>
                {
                    var regularExpressionValidator = (IRegularExpressionValidator) context.PropertyValidator;
                    context.Schema.Properties[context.PropertyKey].Pattern = regularExpressionValidator.Expression;
                },
            };

            yield return new FluentValidationRule("EMail")
            {
                Matches = propertyValidator => propertyValidator.GetType().Name.Contains("EmailValidator") && propertyValidator.HasNoCondition(),
                Apply = context =>
                {
                    context.Schema.Properties[context.PropertyKey].Format = "email";
                },
            };

            yield return new FluentValidationRule("Comparison")
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
                },
            };

            yield return new FluentValidationRule("Between")
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
                },
            };
        }
    }
}