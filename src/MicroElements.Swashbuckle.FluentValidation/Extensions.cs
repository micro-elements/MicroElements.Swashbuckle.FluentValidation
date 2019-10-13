using System;
using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using FluentValidation.Internal;
using FluentValidation.Validators;
using JetBrains.Annotations;

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
                .OfType<PropertyRule>()
                .Where(propertyRule => propertyRule.Condition == null && propertyRule.AsyncCondition == null && propertyRule.PropertyName?.Equals(name, StringComparison.InvariantCultureIgnoreCase) == true)
                .SelectMany(propertyRule => propertyRule.Validators);
        }

        /// <summary>
        /// Is supported swagger numeric type.
        /// </summary>
        internal static bool IsNumeric(this object value) => value is int || value is long || value is float || value is double || value is decimal;

        /// <summary>
        /// Convert numeric to int.
        /// </summary>
        internal static int NumericToInt(this object value) => Convert.ToInt32(value);

        /// <summary>
        /// Convert numeric to double.
        /// </summary>
        internal static double NumericToDouble(this object value) => Convert.ToDouble(value);

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
        internal static IReadOnlyList<FluentValidationRule> OverrideRules(this IReadOnlyList<FluentValidationRule> source, IEnumerable<FluentValidationRule> overrides)
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

            return source;
        }
    }
}
