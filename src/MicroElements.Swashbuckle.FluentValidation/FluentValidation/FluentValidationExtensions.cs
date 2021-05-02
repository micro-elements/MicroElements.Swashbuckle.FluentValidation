// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using FluentValidation.Internal;
using FluentValidation.Validators;

namespace MicroElements.Swashbuckle.FluentValidation
{
    /// <summary>
    /// Extensions for FluentValidation specific work.
    /// </summary>
    public static class FluentValidationExtensions
    {
        /// <summary>
        /// Contains <see cref="PropertyRule"/> and additional info.
        /// </summary>
        public readonly struct ValidationRuleContext
        {
            /// <summary>
            /// PropertyRule.
            /// </summary>
            public readonly IValidationRule PropertyRule;

            /// <summary>
            /// Flag indication whether the <see cref="PropertyRule"/> is the CollectionRule.
            /// </summary>
            public readonly bool IsCollectionRule;

            /// <summary>
            /// Initializes a new instance of the <see cref="ValidationRuleContext"/> struct.
            /// </summary>
            /// <param name="propertyRule">PropertyRule.</param>
            /// <param name="isCollectionRule">Is a CollectionPropertyRule.</param>
            public ValidationRuleContext(IValidationRule propertyRule, bool isCollectionRule)
            {
                PropertyRule = propertyRule;
                IsCollectionRule = isCollectionRule;
            }
        }

        /// <summary>
        /// Gets validation rules for validator.
        /// </summary>
        /// <param name="validator">Validator.</param>
        /// <returns>enumeration.</returns>
        public static IEnumerable<ValidationRuleContext> GetValidationRules(this IValidator validator)
        {
            return (validator as IEnumerable<IValidationRule>)
                .NotNull()
                .GetPropertyRules();
        }

        /// <summary>
        /// Returns validation rules by property name ignoring name case.
        /// </summary>
        /// <param name="validator">Validator</param>
        /// <param name="name">Property name.</param>
        /// <returns>enumeration.</returns>
        public static IEnumerable<ValidationRuleContext> GetValidationRulesForMemberIgnoreCase(this IValidator validator, string name)
        {
            return validator
                .GetValidationRules()
                .Where(propertyRule => propertyRule.PropertyRule.PropertyName.EqualsIgnoreAll(name));
        }

        /// <summary>
        /// Returns property validators by property name ignoring name case.
        /// </summary>
        /// <param name="validator">Validator</param>
        /// <param name="name">Property name.</param>
        /// <returns>enumeration.</returns>
        public static IEnumerable<IPropertyValidator> GetValidatorsForMemberIgnoreCase(this IValidator validator, string name)
        {
            return GetValidationRulesForMemberIgnoreCase(validator, name)
                .SelectMany(propertyRule => propertyRule.PropertyRule.Components)
                .OfType<IPropertyValidator>();
        }

        /// <summary>
        /// Returns all IValidationRules that are PropertyRule.
        /// If rule is CollectionPropertyRule then isCollectionRule set to true.
        /// </summary>
        internal static IEnumerable<ValidationRuleContext> GetPropertyRules(
            this IEnumerable<IValidationRule> validationRules)
        {
            return validationRules
                .Where(rule => rule.HasNoCondition())
                .Select(rule =>
                {
                    // CollectionPropertyRule<T, TElement> is also a PropertyRule.
                    var isCollectionRule = rule.GetType().Name.StartsWith("CollectionPropertyRule");
                    return new ValidationRuleContext(rule, isCollectionRule);
                });
        }

        /// <summary>
        /// Returns a <see cref="bool"/> indicating if the <paramref name="propertyRule"/> is conditional.
        /// </summary>
        internal static bool HasNoCondition(this IValidationRule propertyRule)
        {
            var hasCondition = propertyRule.HasCondition || propertyRule.HasAsyncCondition;
            return !hasCondition;
        }

        /// <summary>
        /// Returns a <see cref="bool"/> indicating if the <paramref name="propertyRule"/> is conditional.
        /// </summary>
        internal static bool HasNoCondition(this IRuleComponent ruleComponent)
        {
            var hasCondition = ruleComponent.HasCondition || ruleComponent.HasAsyncCondition;
            return !hasCondition;
        }

        /// <summary>
        /// Gets validators for <see cref="IValidationRule"/>.
        /// </summary>
        /// <param name="validationRule">Validator.</param>
        /// <returns>enumeration.</returns>
        public static IEnumerable<IPropertyValidator> GetValidators(this IValidationRule validationRule)
        {
            return validationRule
                .Components
                .NotNull()
                .Where(component => component.HasNoCondition())
                .Select(component => component.Validator);
        }
    }
}