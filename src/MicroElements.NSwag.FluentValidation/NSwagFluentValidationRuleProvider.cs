using System;
using System.Collections.Generic;
using System.Linq;
using FluentValidation.Validators;
using MicroElements.OpenApi.Core;
using MicroElements.OpenApi.FluentValidation;
using NJsonSchema;
using NJsonSchema.Generation;

namespace MicroElements.NSwag.FluentValidation
{
    public class NSwagFluentValidationRuleProvider : IFluentValidationRuleProvider<SchemaProcessorContext>
    {
        /// <inheritdoc />
        public IEnumerable<IFluentValidationRule<SchemaProcessorContext>> GetRules()
        {
            return CreateDefaultRules();
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
                    },
                },
                new FluentValidationRule("Length")
                {
                    Matches = propertyValidator => propertyValidator is ILengthValidator,
                    Apply = context =>
                    {
                        var schema = context.Schema.Schema;

                        var lengthValidator = (ILengthValidator) context.PropertyValidator;

                        if (lengthValidator.Max > 0)
                            schema.Properties[context.PropertyKey].MaxLength = lengthValidator.Max;

                        if (lengthValidator.GetType() == typeof(MinimumLengthValidator<>)
                            || lengthValidator.GetType() == typeof(ExactLengthValidator<>)
                            || schema.Properties[context.PropertyKey].MinLength == null)
                            schema.Properties[context.PropertyKey].MinLength = lengthValidator.Min;
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
                            }
                            else if (comparisonValidator.Comparison == Comparison.GreaterThan)
                            {
                                schemaProperty.Minimum = valueToCompare;
                                schemaProperty.IsExclusiveMinimum = true;
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
    }
}