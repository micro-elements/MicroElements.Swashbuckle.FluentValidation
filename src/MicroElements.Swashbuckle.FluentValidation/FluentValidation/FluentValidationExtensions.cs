// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
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
        /// Gets validation rules for validator.
        /// </summary>
        /// <param name="validator">Validator.</param>
        /// <param name="schemaGenerationOptions">SchemaGenerationOptions</param>
        /// <returns>enumeration.</returns>
        public static IEnumerable<ValidationRuleInfo> GetValidationRules(
            this IValidator validator,
            ISchemaGenerationOptions schemaGenerationOptions)
        {
            return (validator as IEnumerable<IValidationRule>)
                .NotNull()
                .GetPropertyRules(schemaGenerationOptions);
        }

        /// <summary>
        /// Returns validation rules by property name ignoring name case.
        /// </summary>
        /// <param name="validator">Validator</param>
        /// <param name="schemaGenerationOptions">SchemaGenerationOptions</param>
        /// <param name="name">Property name.</param>
        /// <returns>enumeration.</returns>
        public static IEnumerable<ValidationRuleInfo> GetValidationRulesForMemberIgnoreCase(
            this IValidator validator,
            ISchemaGenerationOptions schemaGenerationOptions,
            string name)
        {
            return validator
                .GetValidationRules(schemaGenerationOptions)
                .Where(propertyRule => propertyRule.PropertyRule.PropertyName.EqualsIgnoreAll(name));
        }

        /// <summary>
        /// Returns all IValidationRules that are PropertyRule.
        /// If rule is CollectionPropertyRule then isCollectionRule set to true.
        /// </summary>
        internal static IEnumerable<ValidationRuleInfo> GetPropertyRules(
              this IEnumerable<IValidationRule> validationRules,
              ISchemaGenerationOptions schemaGenerationOptions)
        {
            return validationRules
                .Where(rule => schemaGenerationOptions.AllowConditionalRules || rule.HasNoCondition())
                .Select(rule =>
                {
                    // CollectionPropertyRule<T, TElement> is also a PropertyRule.
                    var isCollectionRule = rule.GetType().Name.StartsWith("CollectionPropertyRule");

                    ReflectionContext? reflectionContext = null;
                    if (rule.Member != null)
                    {
                        reflectionContext = ReflectionContext.CreateFromProperty(propertyInfo: rule.Member);
                    }

                    return new ValidationRuleInfo(rule, isCollectionRule, reflectionContext);
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
        /// Returns a <see cref="bool"/> indicating if the <paramref name="ruleComponent"/> is conditional.
        /// </summary>
        internal static bool HasNoCondition(this IRuleComponent ruleComponent)
        {
            var hasCondition = ruleComponent.HasCondition || ruleComponent.HasAsyncCondition;
            return !hasCondition;
        }

        /// <summary>
        /// Returns a <see cref="bool"/> indicating if the <paramref name="ruleComponent"/> is conditional.
        /// </summary>
        internal static bool HasCondition(this IRuleComponent ruleComponent)
        {
            return !ruleComponent.HasNoCondition();
        }

        /// <summary>
        /// Returns a <see cref="bool"/> indicating if the <paramref name="propertyRule"/> is conditional.
        /// </summary>
        internal static bool HasCondition(this IValidationRule propertyRule)
        {
            return !propertyRule.HasNoCondition();
        }

        /// <summary>
        /// Gets validators for <see cref="IValidationRule"/>.
        /// </summary>
        /// <param name="validationRule">Validator.</param>
        /// <param name="schemaGenerationOptions">SchemaGenerationOptions</param>
        /// <returns>enumeration.</returns>
        public static IEnumerable<IPropertyValidator> GetValidators(
            this IValidationRule validationRule,
            ISchemaGenerationOptions schemaGenerationOptions)
        {
            return validationRule
                .Components
                .NotNull()
                .Where(component =>
                {
                    if (schemaGenerationOptions.AllowConditionalValidators
                    && component.HasCondition())
                    {
                        return component.Validator.IsAllowedAsConditionalValidator(schemaGenerationOptions);
                    }

                    if (schemaGenerationOptions.AllowConditionalRules
                    && validationRule.HasCondition())
                    {
                        return component.Validator.IsAllowedAsConditionalValidator(schemaGenerationOptions);
                    }

                    return component.HasNoCondition();
                })
                .Select(component => component.Validator);
        }

        private static bool IsAllowedAsConditionalValidator(
              this IPropertyValidator validator
            , ISchemaGenerationOptions schemaGenerationOptions)
        {
            Type validatorType = validator.GetType();
            return schemaGenerationOptions.AllowedConditionalValidatorTypes.Any(x => x.IsAssignableFrom(validatorType));
        }
    }
}