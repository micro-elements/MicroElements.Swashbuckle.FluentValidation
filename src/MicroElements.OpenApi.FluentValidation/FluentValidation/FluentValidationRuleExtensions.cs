using System.Collections.Generic;
using System.Linq;
using FluentValidation.Validators;

namespace MicroElements.OpenApi.FluentValidation
{
    public static class FluentValidationRuleExtensions
    {
        /// <summary>
        /// Overrides source rules with <paramref name="overrides"/> by name.
        /// </summary>
        /// <param name="source">Source rules.</param>
        /// <param name="overrides">Overrides list.</param>
        /// <returns>New rule list.</returns>
        public static IReadOnlyList<IFluentValidationRule<TSchema>> OverrideRules<TSchema>(
            this IReadOnlyList<IFluentValidationRule<TSchema>> source,
            IEnumerable<IFluentValidationRule<TSchema>>? overrides)
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

        /// <summary>
        /// Checks that validator is matches rule.
        /// </summary>
        /// <param name="validator">Validator.</param>
        /// <returns>True if validator matches rule.</returns>
        public static bool IsMatches(this IFluentValidationRule rule, IPropertyValidator validator)
        {
            foreach (var match in rule.Conditions)
            {
                if (!match(validator))
                    return false;
            }

            return true;
        }
    }
}