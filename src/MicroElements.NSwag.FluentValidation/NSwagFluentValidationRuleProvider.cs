// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using FluentValidation.Validators;
using MicroElements.OpenApi.Core;
using MicroElements.OpenApi.FluentValidation;
using Microsoft.Extensions.Options;
using NJsonSchema;
using NJsonSchema.Generation;

namespace MicroElements.NSwag.FluentValidation
{
    public class NSwagFluentValidationRuleProvider : IFluentValidationRuleProvider<SchemaProcessorContext>
    {
        private readonly IOptions<SchemaGenerationOptions> _options;

        /// <summary>
        /// Initializes a new instance of the <see cref="NSwagFluentValidationRuleProvider"/> class.
        /// </summary>
        /// <param name="options">Schema generation options.</param>
        public NSwagFluentValidationRuleProvider(IOptions<SchemaGenerationOptions>? options = null)
        {
            _options = options ?? new OptionsWrapper<SchemaGenerationOptions>(new SchemaGenerationOptions());
        }

        /// <inheritdoc />
        public IEnumerable<IFluentValidationRule<SchemaProcessorContext>> GetRules()
        {
            return CreateDefaultRules();
        }

        /// <summary>
        /// Creates default rules.
        /// Can be overriden by name.
        /// </summary>
        public FluentValidationRule[] CreateDefaultRules()
        {
            return new[]
            {
                new FluentValidationRule("Required")
                {
                    Matches = propertyValidator => propertyValidator is INotNullValidator || propertyValidator is INotEmptyValidator,
                    Apply = context =>
                    {
                        var schema = context.Schema.Schema;

                        if (schema == null)
                            return;

                        if (!schema.RequiredProperties.Contains(context.PropertyKey))
                            schema.RequiredProperties.Add(context.PropertyKey);
                    },
                },
                new FluentValidationRule("NotNull")
                {
                    Matches = propertyValidator => propertyValidator is INotNullValidator,
                    Apply = context =>
                    {
                        var schema = context.Schema.Schema;

                        JsonSchemaProperty property = schema.Properties[context.PropertyKey];
                        property.IsNullableRaw = false;

                        if (property.Type.HasFlag(JsonObjectType.Null))
                        {
                            property.Type &= ~JsonObjectType.Null; // Remove nullable
                        }

                        var oneOfsWithReference = property.OneOf
                            .Where(x => x.Reference != null).ToList();

                        if (oneOfsWithReference.Count == 1)
                        {
                            // Set the Reference directly instead and clear the OneOf collection
                            property.Reference = oneOfsWithReference.Single();
                            property.OneOf.Clear();
                        }
                    },
                },
                new FluentValidationRule("NotEmpty")
                {
                    Matches = propertyValidator => propertyValidator is INotEmptyValidator,
                    Apply = context =>
                    {
                        var schema = context.Schema.Schema;

                        var property = schema.Properties[context.PropertyKey];
                        property.IsNullableRaw = false;

                        if (property.Type.HasFlag(JsonObjectType.Null))
                        {
                            property.Type &= ~JsonObjectType.Null; // Remove nullable
                        }

                        var oneOfsWithReference = property.OneOf
                            .Where(x => x.Reference != null).ToList();

                        if (oneOfsWithReference.Count == 1)
                        {
                            // Set the Reference directly instead and clear the OneOf collection
                            property.Reference = oneOfsWithReference.Single();
                            property.OneOf.Clear();
                        }

                        property.MinLength = 1;

                        if (_options.Value.SetNotNullableIfMinLengthGreaterThenZero)
                        {
                            SetNSwagNotNullable(property);
                        }
                    },
                },
                new FluentValidationRule("Length")
                {
                    Matches = propertyValidator => propertyValidator is ILengthValidator,
                    Apply = context =>
                    {
                        var schema = context.Schema.Schema;

                        var lengthValidator = (ILengthValidator) context.PropertyValidator;
                        var property = schema.Properties[context.PropertyKey];

                        if (lengthValidator.Max > 0)
                            property.MaxLength = lengthValidator.Max;

                        if (lengthValidator.GetType() == typeof(MinimumLengthValidator<>)
                            || lengthValidator.GetType() == typeof(ExactLengthValidator<>)
                            || property.MinLength == null)
                            property.MinLength = lengthValidator.Min;

                        if (_options.Value.SetNotNullableIfMinLengthGreaterThenZero && property.MinLength > 0)
                        {
                            SetNSwagNotNullable(property);
                        }
                    },
                },
                new FluentValidationRule("Pattern")
                {
                    Matches = propertyValidator => propertyValidator is IRegularExpressionValidator,
                    Apply = context =>
                    {
                        var regularExpressionValidator = (IRegularExpressionValidator) context.PropertyValidator;

                        var schema = context.Schema.Schema;
                        schema.Properties[context.PropertyKey].Pattern = regularExpressionValidator.Expression;
                    },
                },
                new FluentValidationRule("Comparison")
                {
                    Matches = propertyValidator => propertyValidator is IComparisonValidator,
                    Apply = context =>
                    {
                        var comparisonValidator = (IComparisonValidator) context.PropertyValidator;

                        if (comparisonValidator.ValueToCompare.IsNumeric())
                        {
                            var valueToCompare = Convert.ToDecimal(comparisonValidator.ValueToCompare);
                            var schema = context.Schema.Schema;
                            var schemaProperty = schema.Properties[context.PropertyKey];

                            if (comparisonValidator.Comparison == Comparison.GreaterThanOrEqual)
                            {
                                schemaProperty.Minimum = valueToCompare;
                                if (ShouldSetNotNullableForNumericMinimum)
                                    SetNSwagNotNullableIfMinimumGreaterThenZero(schemaProperty);
                            }
                            else if (comparisonValidator.Comparison == Comparison.GreaterThan)
                            {
                                schemaProperty.Minimum = valueToCompare;
                                schemaProperty.IsExclusiveMinimum = true;
                                if (ShouldSetNotNullableForNumericMinimum)
                                    SetNSwagNotNullableIfMinimumGreaterThenZero(schemaProperty);
                            }
                            else if (comparisonValidator.Comparison == Comparison.LessThanOrEqual)
                            {
                                schemaProperty.Maximum = valueToCompare;
                            }
                            else if (comparisonValidator.Comparison == Comparison.LessThan)
                            {
                                schemaProperty.Maximum = valueToCompare;
                                schemaProperty.IsExclusiveMaximum = true;
                            }
                        }
                    },
                },
                new FluentValidationRule("Between")
                {
                    Matches = propertyValidator => propertyValidator is IBetweenValidator,
                    Apply = context =>
                    {
                        var betweenValidator = (IBetweenValidator) context.PropertyValidator;
                        var schema = context.Schema.Schema;
                        var schemaProperty = schema.Properties[context.PropertyKey];

                        if (betweenValidator.From.IsNumeric())
                        {
                            if (betweenValidator.GetType().IsSubClassOfGeneric(typeof(ExclusiveBetweenValidator<,>)))
                            {
                                schemaProperty.ExclusiveMinimum = Convert.ToDecimal(betweenValidator.From);
                            }
                            else
                            {
                                schemaProperty.Minimum = Convert.ToDecimal(betweenValidator.From);
                            }

                            if (ShouldSetNotNullableForNumericMinimum)
                                SetNSwagNotNullableIfMinimumGreaterThenZero(schemaProperty);
                        }

                        if (betweenValidator.To.IsNumeric())
                        {
                            if (betweenValidator.GetType().IsSubClassOfGeneric(typeof(ExclusiveBetweenValidator<,>)))
                            {
                                schemaProperty.ExclusiveMaximum = Convert.ToDecimal(betweenValidator.To);
                            }
                            else
                            {
                                schemaProperty.Maximum = Convert.ToDecimal(betweenValidator.To);
                            }
                        }
                    },
                },
                new FluentValidationRule("AspNetCoreCompatibleEmail")
                {
                    Matches = propertyValidator => propertyValidator.GetType()
                        .IsSubClassOfGeneric(
                            typeof(AspNetCoreCompatibleEmailValidator<>)
                        ),
                    Apply = context =>
                    {
                        var schema = context.Schema.Schema;
                        schema.Properties[context.PropertyKey].Pattern = "^[^@]+@[^@]+$"; // [^@] All chars except @
                    },
                },
            };
        }

        /// <summary>
        /// Returns true when either numeric minimum option OR legacy min-length option is enabled,
        /// preserving backwards compatibility for users who relied on SetNotNullableIfMinLengthGreaterThenZero
        /// to also affect numeric minimum constraints.
        /// </summary>
        private bool ShouldSetNotNullableForNumericMinimum =>
            _options.Value.SetNotNullableIfMinLengthGreaterThenZero || _options.Value.SetNotNullableIfMinimumGreaterThenZero;

        /// <summary>
        /// Sets NJsonSchema property as not nullable.
        /// </summary>
        private static void SetNSwagNotNullable(JsonSchemaProperty property)
        {
            property.IsNullableRaw = false;

            if (property.Type.HasFlag(JsonObjectType.Null))
            {
                property.Type &= ~JsonObjectType.Null;
            }
        }

        /// <summary>
        /// Sets NJsonSchema property as not nullable if Minimum > 0 or ExclusiveMinimum >= 0.
        /// </summary>
        private static void SetNSwagNotNullableIfMinimumGreaterThenZero(JsonSchemaProperty property)
        {
            var minimum = property.Minimum ?? property.ExclusiveMinimum;
            var isExclusive = property.IsExclusiveMinimum || property.ExclusiveMinimum.HasValue;

            if (minimum.HasValue && (isExclusive ? minimum >= 0 : minimum > 0))
            {
                SetNSwagNotNullable(property);
            }
        }
    }
}
