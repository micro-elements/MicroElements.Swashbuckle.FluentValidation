// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;

namespace MicroElements.Swashbuckle.FluentValidation
{
    /// <summary>
    /// Provider for <see cref="FluentValidationRule"/>.
    /// </summary>
    public static class FluentValidationRuleProvider
    {
        /// <summary>
        /// Creates default rules.
        /// </summary>
        /// <returns>Enumeration of <see cref= "FluentValidationRule" />.</returns>
        public static FluentValidationRule[] CreateDefaultRules()
        {
            return DefaultFluentValidationRuleProvider.Instance.GetRules().ToArray();
        }

        /// <summary>
        /// Overrides source rules with <paramref name="overrides"/> by name.
        /// </summary>
        /// <param name="source">Source rules.</param>
        /// <param name="overrides">Overrides list.</param>
        /// <returns>New rule list.</returns>
        public static IReadOnlyList<FluentValidationRule> OverrideRules(
            this IReadOnlyList<FluentValidationRule> source,
            IEnumerable<FluentValidationRule>? overrides)
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