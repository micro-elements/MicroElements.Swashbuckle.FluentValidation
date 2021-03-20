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
            public readonly PropertyRule PropertyRule;

            /// <summary>
            /// Flag indication whether the <see cref="PropertyRule"/> is the CollectionRule.
            /// </summary>
            public readonly bool IsCollectionRule;

            /// <summary>
            /// Initializes a new instance of the <see cref="ValidationRuleContext"/> struct.
            /// </summary>
            /// <param name="propertyRule">PropertyRule.</param>
            /// <param name="isCollectionRule">Is a CollectionPropertyRule.</param>
            public ValidationRuleContext(PropertyRule propertyRule, bool isCollectionRule)
            {
                PropertyRule = propertyRule;
                IsCollectionRule = isCollectionRule;
            }
        }

        /// <summary>
        /// Returns validation rules by property name ignoring name case.
        /// </summary>
        /// <param name="validator">Validator</param>
        /// <param name="name">Property name.</param>
        /// <returns>enumeration.</returns>
        public static IEnumerable<ValidationRuleContext> GetValidationRulesForMemberIgnoreCase(this IValidator validator, string name)
        {
            return (validator as IEnumerable<IValidationRule>)
                .NotNull()
                .GetPropertyRules()
                .Where(propertyRule => HasNoCondition((PropertyRule) propertyRule.PropertyRule) && StringExtensions.EqualsIgnoreAll(propertyRule.PropertyRule.PropertyName, name));
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
                .SelectMany(propertyRule => propertyRule.PropertyRule.Validators);
        }

        /// <summary>
        /// Returns all IValidationRules that are PropertyRule.
        /// If rule is CollectionPropertyRule then isCollectionRule set to true.
        /// </summary>
        internal static IEnumerable<ValidationRuleContext> GetPropertyRules(
            this IEnumerable<IValidationRule> validationRules)
        {
            foreach (var validationRule in validationRules)
            {
                if (validationRule is PropertyRule propertyRule)
                {
                    // CollectionPropertyRule<T, TElement> is also a PropertyRule.
                    var isCollectionRule = propertyRule.GetType().Name.StartsWith("CollectionPropertyRule");
                    yield return new ValidationRuleContext(propertyRule, isCollectionRule);
                }
            }
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
    }
}