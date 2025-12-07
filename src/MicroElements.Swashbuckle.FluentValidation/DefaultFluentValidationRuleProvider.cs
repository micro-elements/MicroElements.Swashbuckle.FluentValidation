// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using FluentValidation.Validators;
using MicroElements.OpenApi;
using MicroElements.OpenApi.Core;
using MicroElements.OpenApi.FluentValidation;
using Microsoft.Extensions.Options;
#if !OPENAPI_V2
using Microsoft.OpenApi.Models;
#endif

namespace MicroElements.Swashbuckle.FluentValidation
{
    /// <summary>
    /// Default rule provider.
    /// </summary>
    public class DefaultFluentValidationRuleProvider : IFluentValidationRuleProvider<OpenApiSchema>
    {
        private readonly IOptions<SchemaGenerationOptions> _options;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultFluentValidationRuleProvider"/> class.
        /// </summary>
        /// <param name="options">Schema generation options.</param>
        public DefaultFluentValidationRuleProvider(IOptions<SchemaGenerationOptions>? options = null)
        {
            _options = options ?? new OptionsWrapper<SchemaGenerationOptions>(new SchemaGenerationOptions());
        }

        /// <inheritdoc />
        public IEnumerable<IFluentValidationRule<OpenApiSchema>> GetRules()
        {
            yield return new FluentValidationRule("Required")
                .WithCondition(validator => validator is INotNullValidator || validator is INotEmptyValidator)
                .WithApply(context =>
                {
                    OpenApiSchemaCompatibility.AddRequired(context.Schema, context.PropertyKey);
                    OpenApiSchemaCompatibility.SetNotNullable(context.Property);
                });

            yield return new FluentValidationRule("NotEmpty")
                .WithCondition(validator => validator is INotEmptyValidator)
                .WithApply(context =>
                {
                    if (OpenApiSchemaCompatibility.IsStringType(context.Property))
                        context.Property.SetNewMin(p => p.MinLength, 1, _options.Value.SetNotNullableIfMinLengthGreaterThenZero);

                    if (OpenApiSchemaCompatibility.IsArrayType(context.Property))
                        context.Property.SetNewMin(p => p.MinItems, 1, _options.Value.SetNotNullableIfMinLengthGreaterThenZero);
                });

            yield return new FluentValidationRule("Length")
                .WithCondition(validator => validator is ILengthValidator)
                .WithApply(context =>
                {
                    var lengthValidator = (ILengthValidator) context.PropertyValidator;
                    var schemaProperty = context.Property;

                    if (OpenApiSchemaCompatibility.IsArrayType(schemaProperty))
                    {
                        if (lengthValidator.Max > 0)
                            schemaProperty.SetNewMax(p => p.MaxItems, lengthValidator.Max);

                        if (lengthValidator.Min > 0)
                            schemaProperty.SetNewMin(p => p.MinItems, lengthValidator.Min, _options.Value.SetNotNullableIfMinLengthGreaterThenZero);
                    }
                    else
                    {
                        if (lengthValidator.Max > 0)
                            schemaProperty.SetNewMax(p => p.MaxLength, lengthValidator.Max);

                        if (lengthValidator.Min > 0)
                            schemaProperty.SetNewMin(p => p.MinLength, lengthValidator.Min, _options.Value.SetNotNullableIfMinLengthGreaterThenZero);
                    }
                });

            yield return new FluentValidationRule("Pattern")
                .WithCondition(validator => validator is IRegularExpressionValidator)
                .WithApply(context =>
                {
                    var regularExpressionValidator = (IRegularExpressionValidator) context.PropertyValidator;
                    var schemaProperty = context.Property;

                    if (_options.Value.UseAllOfForMultipleRules)
                    {
                        if (schemaProperty.Pattern != null ||
                            OpenApiSchemaCompatibility.AllOfCountWhere(schemaProperty, schema => schema.Pattern != null) > 0)
                        {
                            if (OpenApiSchemaCompatibility.AllOfCountWhere(schemaProperty, schema => schema.Pattern != null) == 0)
                            {
                                // Add first pattern as AllOf
                                OpenApiSchemaCompatibility.AddAllOf(schemaProperty, new OpenApiSchema()
                                {
                                    Pattern = schemaProperty.Pattern,
                                });
                            }

                            // Add another pattern as AllOf
                            OpenApiSchemaCompatibility.AddAllOf(schemaProperty, new OpenApiSchema()
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
                });

            yield return new FluentValidationRule("EMail")
                .WithCondition(propertyValidator => propertyValidator.GetType().Name.Contains("EmailValidator"))
                .WithApply(context => context.Property.Format = "email");

            yield return new FluentValidationRule("Comparison")
                .WithCondition(validator => validator is IComparisonValidator)
                .WithApply(context =>
                {
                    var comparisonValidator = (IComparisonValidator)context.PropertyValidator;
                    if (comparisonValidator.ValueToCompare.IsNumeric())
                    {
                        var valueToCompare = comparisonValidator.ValueToCompare.NumericToDecimal();
                        var schemaProperty = context.Property;

                        if (comparisonValidator.Comparison == Comparison.GreaterThanOrEqual)
                        {
                            OpenApiSchemaCompatibility.SetNewMinimum(schemaProperty, valueToCompare);
                            if (_options.Value.SetNotNullableIfMinLengthGreaterThenZero)
                                OpenApiSchemaCompatibility.SetNotNullable(schemaProperty);
                        }
                        else if (comparisonValidator.Comparison == Comparison.GreaterThan)
                        {
                            OpenApiSchemaCompatibility.SetNewMinimum(schemaProperty, valueToCompare);
                            OpenApiSchemaCompatibility.SetExclusiveMinimum(schemaProperty, true);
                            if (_options.Value.SetNotNullableIfMinLengthGreaterThenZero)
                                OpenApiSchemaCompatibility.SetNotNullable(schemaProperty);
                        }
                        else if (comparisonValidator.Comparison == Comparison.LessThanOrEqual)
                        {
                            OpenApiSchemaCompatibility.SetNewMaximum(schemaProperty, valueToCompare);
                        }
                        else if (comparisonValidator.Comparison == Comparison.LessThan)
                        {
                            OpenApiSchemaCompatibility.SetNewMaximum(schemaProperty, valueToCompare);
                            OpenApiSchemaCompatibility.SetExclusiveMaximum(schemaProperty, true);
                        }
                    }
                });

            yield return new FluentValidationRule("Between")
                .WithCondition(validator => validator is IBetweenValidator)
                .WithApply(context =>
                {
                    var betweenValidator = (IBetweenValidator)context.PropertyValidator;
                    var schemaProperty = context.Property;

                    //OpenApi: date-time has not support range validations see: https://github.com/json-schema-org/json-schema-spec/issues/116

                    if (betweenValidator.From.IsNumeric())
                    {
                        OpenApiSchemaCompatibility.SetNewMinimum(schemaProperty, betweenValidator.From.NumericToDecimal());
                        if (_options.Value.SetNotNullableIfMinLengthGreaterThenZero)
                            OpenApiSchemaCompatibility.SetNotNullable(schemaProperty);

                        if (betweenValidator.Name == "ExclusiveBetweenValidator")
                        {
                            OpenApiSchemaCompatibility.SetExclusiveMinimum(schemaProperty, true);
                        }
                    }

                    if (betweenValidator.To.IsNumeric())
                    {
                        OpenApiSchemaCompatibility.SetNewMaximum(schemaProperty, betweenValidator.To.NumericToDecimal());

                        if (betweenValidator.Name == "ExclusiveBetweenValidator")
                        {
                            OpenApiSchemaCompatibility.SetExclusiveMaximum(schemaProperty, true);
                        }
                    }
                });
        }
    }
}