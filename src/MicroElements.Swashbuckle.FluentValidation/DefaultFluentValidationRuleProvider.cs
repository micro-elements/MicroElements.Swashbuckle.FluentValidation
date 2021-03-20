// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using FluentValidation.Validators;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;

namespace MicroElements.Swashbuckle.FluentValidation
{
    /// <summary>
    /// Default rule provider.
    /// </summary>
    public class DefaultFluentValidationRuleProvider : IFluentValidationRuleProvider
    {
        /// <summary>
        /// Gets global static default <see cref="IFluentValidationRuleProvider"/>.
        /// </summary>
        public static DefaultFluentValidationRuleProvider Instance { get; } = new DefaultFluentValidationRuleProvider();

        private readonly IOptions<FluentValidationSwaggerGenOptions> _options;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultFluentValidationRuleProvider"/> class.
        /// </summary>
        /// <param name="options">Schema generation options.</param>
        public DefaultFluentValidationRuleProvider(IOptions<FluentValidationSwaggerGenOptions>? options = null)
        {
            _options = options ?? new OptionsWrapper<FluentValidationSwaggerGenOptions>(new FluentValidationSwaggerGenOptions());
        }

        /// <inheritdoc />
        public IEnumerable<FluentValidationRule> GetRules()
        {
            return new[]
            {
                new FluentValidationRule("BeforeAll")
                {
                    Matches = propertyValidator => propertyValidator.HasNoCondition(),
                    Apply = context =>
                    {
                        var property = context.Schema.Properties[context.PropertyKey];
                        property.Nullable = property.Nullable;
                    },
                },
                new FluentValidationRule("Required")
                {
                    Matches = propertyValidator => (propertyValidator is INotNullValidator || propertyValidator is INotEmptyValidator) && propertyValidator.HasNoCondition(),
                    Apply = context =>
                    {
                        if (context.Schema.Required == null)
                            context.Schema.Required = new SortedSet<string>();
                        if (!context.Schema.Required.Contains(context.PropertyKey))
                            context.Schema.Required.Add(context.PropertyKey);
                        context.Schema.Properties[context.PropertyKey].Nullable = false;
                    },
                },
                new FluentValidationRule("NotEmpty")
                {
                    Matches = propertyValidator => propertyValidator is INotEmptyValidator && propertyValidator.HasNoCondition(),
                    Apply = context =>
                    {
                        var schemaProperty = context.Schema.Properties[context.PropertyKey];
                        schemaProperty.SetNewMin(p => p.MinLength, 1, _options.Value.SetNotNullableIfMinLengthGreaterThenZero);
                    },
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
                            schemaProperty.SetNewMin(p => p.MinLength, lengthValidator.Min, _options.Value.SetNotNullableIfMinLengthGreaterThenZero);
                    },
                },
                new FluentValidationRule("Pattern")
                {
                    Matches = propertyValidator => propertyValidator is IRegularExpressionValidator && propertyValidator.HasNoCondition(),
                    Apply = context =>
                    {
                        var regularExpressionValidator = (IRegularExpressionValidator)context.PropertyValidator;
                        var schemaProperty = context.Schema.Properties[context.PropertyKey];

                        bool isSupportsAllOff = true;

                        if (isSupportsAllOff)
                        {
                            if (schemaProperty.Pattern != null || schemaProperty.AllOf.Count(schema => schema.Pattern != null) > 0)
                            {
                                if (schemaProperty.AllOf.Count(schema => schema.Pattern != null) == 0)
                                {
                                    // Add first pattern as AllOf
                                    schemaProperty.AllOf.Add(new OpenApiSchema()
                                    {
                                        Pattern = schemaProperty.Pattern,
                                    });
                                }

                                // Add another pattern as AllOf
                                schemaProperty.AllOf.Add(new OpenApiSchema()
                                {
                                    Pattern = regularExpressionValidator.Expression,
                                });

                                schemaProperty.Pattern = null;
                            }
                            else
                            {
                                // First and only pattern
                                schemaProperty.Pattern = regularExpressionValidator.Expression;
                            }
                        }
                        else
                        {
                            // Set new pattern
                            schemaProperty.Pattern = regularExpressionValidator.Expression;
                        }
                    },
                },
                new FluentValidationRule("EMail")
                {
                    Matches = propertyValidator => propertyValidator.GetType().Name.Contains("EmailValidator") && propertyValidator.HasNoCondition(),
                    Apply = context =>
                    {
                        context.Property.Format = "email";
                    },
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
                                schemaProperty.SetNewMin(p => p.Minimum, valueToCompare, _options.Value.SetNotNullableIfMinLengthGreaterThenZero);
                            }
                            else if (comparisonValidator.Comparison == Comparison.GreaterThan)
                            {
                                schemaProperty.SetNewMin(p => p.Minimum, valueToCompare, _options.Value.SetNotNullableIfMinLengthGreaterThenZero);
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
                            schemaProperty.SetNewMin(p => p.Minimum, betweenValidator.From.NumericToDecimal(), _options.Value.SetNotNullableIfMinLengthGreaterThenZero);

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
                },
            };
        }
    }
}