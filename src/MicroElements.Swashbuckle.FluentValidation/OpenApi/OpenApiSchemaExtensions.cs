using System;
using System.Linq.Expressions;
using Microsoft.OpenApi.Models;
using static MicroElements.Swashbuckle.FluentValidation.Include;

namespace MicroElements.Swashbuckle.FluentValidation
{
    public static class OpenApiSchemaExtensions
    {
        /// <summary>
        /// Sets Nullable to false if MinLength > 0.
        /// </summary>
        internal static void SetNotNullableIfMinLengthGreaterThenZero(this OpenApiSchema schemaProperty)
        {
            var shouldBeNotEmpty = schemaProperty.MinLength.HasValue && schemaProperty.MinLength > 0;
            schemaProperty.Nullable = !shouldBeNotEmpty;
        }

        internal static void SetNewMax(this OpenApiSchema schemaProperty, Expression<Func<OpenApiSchema, int?>> prop, int? newValue)
        {
            if (newValue.HasValue)
            {
                var current = prop.Compile()(schemaProperty);
                newValue = NewMaxValue(current, newValue.Value);
                schemaProperty.SetPropertyValue(prop, newValue);
            }
        }

        internal static void SetNewMax(this OpenApiSchema schemaProperty, Expression<Func<OpenApiSchema, decimal?>> prop, decimal? newValue)
        {
            if (newValue.HasValue)
            {
                var current = prop.Compile()(schemaProperty);
                newValue = NewMaxValue(current, newValue.Value);
                schemaProperty.SetPropertyValue(prop, newValue);
            }
        }

        internal static void SetNewMin(this OpenApiSchema schemaProperty, Expression<Func<OpenApiSchema, int?>> prop, int? newValue)
        {
            if (newValue.HasValue)
            {
                var current = prop.Compile()(schemaProperty);
                newValue = NewMinValue(current, newValue.Value);
                schemaProperty.SetPropertyValue(prop, newValue);
            }
        }

        internal static void SetNewMin(this OpenApiSchema schemaProperty, Expression<Func<OpenApiSchema, decimal?>> prop, decimal? newValue)
        {
            if (newValue.HasValue)
            {
                var current = prop.Compile()(schemaProperty);
                newValue = NewMinValue(current, newValue.Value);
                schemaProperty.SetPropertyValue(prop, newValue);
            }
        }
    }
}