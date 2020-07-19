using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using FluentValidation;
using FluentValidation.Internal;
using FluentValidation.Validators;
using JetBrains.Annotations;
using Microsoft.OpenApi.Models;

[assembly: InternalsVisibleTo("MicroElements.Swashbuckle.FluentValidation.Tests")]

namespace MicroElements.Swashbuckle.FluentValidation
{
    /// <summary>
    /// Extensions for some swagger specific work.
    /// </summary>
    public static class Extensions
    {
        /// <summary>
        /// Returns validators by property name ignoring name case.
        /// </summary>
        /// <param name="validator">Validator</param>
        /// <param name="name">Property name.</param>
        /// <returns>enumeration or null.</returns>
        public static IEnumerable<IPropertyValidator> GetValidatorsForMemberIgnoreCase(this IValidator validator, string name)
        {
            return (validator as IEnumerable<IValidationRule>)
                .NotNull()
                .GetPropertyRules()
                .Where(propertyRule => propertyRule.HasNoCondition() && propertyRule.PropertyName.EqualsIgnoreAll(name))
                .SelectMany(propertyRule => propertyRule.Validators);
        }

        /// <summary>
        /// Removes all IValidationRules that are not a PropertyRule.
        /// A CollectionPropertyRule should not be exposed in the OpenAPI specification #49.
        /// </summary>
        internal static IEnumerable<PropertyRule> GetPropertyRules(
            this IEnumerable<IValidationRule> validationRules)
        {
            foreach (var validationRule in validationRules)
            {
                if (validationRule.GetType() == typeof(PropertyRule))
                {
                    yield return (PropertyRule)validationRule;
                }
            }
        }

        /// <summary>
        /// Is supported swagger numeric type.
        /// </summary>
        internal static bool IsNumeric(this object value) => value is int || value is long || value is float || value is double || value is decimal;

        /// <summary>
        /// Convert numeric to double.
        /// </summary>
        internal static decimal NumericToDecimal(this object value) => Convert.ToDecimal(value);

        /// <summary>
        /// Converts string to lowerCamelCase.
        /// </summary>
        /// <param name="inputString">Input string.</param>
        /// <returns>lowerCamelCase string.</returns>
        internal static string ToLowerCamelCase(this string inputString)
        {
            if (inputString == null) return null;
            if (inputString == string.Empty) return string.Empty;
            if (char.IsLower(inputString[0])) return inputString;
            return inputString.Substring(0, 1).ToLower() + inputString.Substring(1);
        }

        /// <summary>
        /// Returns not null enumeration.
        /// </summary>
        [NotNull]
        internal static IEnumerable<TValue> NotNull<TValue>([CanBeNull] this IEnumerable<TValue> collection) =>
            collection ?? Array.Empty<TValue>();

        /// <summary>
        /// Overrides source rules with <paramref name="overrides"/> by name.
        /// </summary>
        /// <param name="source">Source rules.</param>
        /// <param name="overrides">Overrides list.</param>
        /// <returns>New rule list.</returns>
        public static IReadOnlyList<FluentValidationRule> OverrideRules(
            this IEnumerable<FluentValidationRule> source,
            IEnumerable<FluentValidationRule> overrides)
        {
            if (overrides != null)
            {
                var validationRules = source.ToDictionary(rule => rule.Name, rule => rule);
                foreach (var validationRule in overrides)
                {
                    validationRules[validationRule.Name] = validationRule;
                }

                return validationRules.Values.ToList();
            }

            return source.ToList();
        }

        /// <summary>
        /// Returns a <see cref="bool"/> indicating if the <paramref name="propertyValidator"/> is conditional.
        /// </summary>
        internal static bool HasNoCondition(this IPropertyValidator propertyValidator)
        {
            return propertyValidator?.Options?.Condition == null && propertyValidator?.Options?.AsyncCondition == null;
        }

        /// <summary>
        /// Returns a <see cref="bool"/> indicating if the <paramref name="propertyRule"/> is conditional.
        /// </summary>
        internal static bool HasNoCondition(this PropertyRule propertyRule)
        {
            return propertyRule?.Condition == null && propertyRule?.AsyncCondition == null;
        }

        /// <summary>
        /// Returns string equality only by symbols ignore case.
        /// It can be used for comparing camelCase, PascalCase, snake_case, kebab-case identifiers.
        /// </summary>
        /// <param name="left">Left string to compare.</param>
        /// <param name="right">Right string to compare.</param>
        /// <returns><c>true</c> if input strings are equals in terms of identifier formatting.</returns>
        internal static bool EqualsIgnoreAll(this string left, string right)
        {
            return IgnoreAllStringComparer.Instance.Equals(left, right);
        }

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
                SetPropertyValue(schemaProperty, prop, newValue);
            }
        }

        internal static void SetNewMax(this OpenApiSchema schemaProperty, Expression<Func<OpenApiSchema, decimal?>> prop, decimal? newValue)
        {
            if (newValue.HasValue)
            {
                var current = prop.Compile()(schemaProperty);
                newValue = NewMaxValue(current, newValue.Value);
                SetPropertyValue(schemaProperty, prop, newValue);
            }
        }

        internal static void SetNewMin(this OpenApiSchema schemaProperty, Expression<Func<OpenApiSchema, int?>> prop, int? newValue)
        {
            if (newValue.HasValue)
            {
                var current = prop.Compile()(schemaProperty);
                newValue = NewMinValue(current, newValue.Value);
                SetPropertyValue(schemaProperty, prop, newValue);
            }
        }

        internal static void SetNewMin(this OpenApiSchema schemaProperty, Expression<Func<OpenApiSchema, decimal?>> prop, decimal? newValue)
        {
            if (newValue.HasValue)
            {
                var current = prop.Compile()(schemaProperty);
                newValue = NewMinValue(current, newValue.Value);
                SetPropertyValue(schemaProperty, prop, newValue);
            }
        }

        private static int NewMaxValue(int? current, int newValue) => current.HasValue ? Math.Min(current.Value, newValue) : newValue;

        private static decimal NewMaxValue(decimal? current, decimal newValue) => current.HasValue ? Math.Min(current.Value, newValue) : newValue;

        private static int NewMinValue(int? current, int newValue) => current.HasValue ? Math.Max(current.Value, newValue) : newValue;

        private static decimal NewMinValue(decimal? current, decimal newValue) => current.HasValue ? Math.Max(current.Value, newValue) : newValue;

        private static void SetPropertyValue<T, TValue>(this T target, Expression<Func<T, TValue>> propertyLambda, TValue value)
        {
            if (propertyLambda.Body is MemberExpression memberSelectorExpression)
            {
                var property = memberSelectorExpression.Member as PropertyInfo;
                if (property != null)
                {
                    property.SetValue(target, value, null);
                }
            }
        }
    }
}