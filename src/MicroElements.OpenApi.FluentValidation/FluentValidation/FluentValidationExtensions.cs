// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FluentValidation;
using FluentValidation.Internal;
using FluentValidation.Validators;
using MicroElements.OpenApi.Core;

namespace MicroElements.OpenApi.FluentValidation
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
        /// <returns>enumeration.</returns>
        public static IEnumerable<ValidationRuleInfo> GetValidationRules(this IValidator validator)
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
        public static IEnumerable<ValidationRuleInfo> GetValidationRulesForMemberIgnoreCase(this IValidator validator, string name)
        {
            return validator
                .GetValidationRules()
                .Where(propertyRule => propertyRule.PropertyRule.PropertyName.EqualsIgnoreAll(name));
        }

        /// <summary>
        /// Returns all IValidationRules that are PropertyRule.
        /// If rule is CollectionPropertyRule then isCollectionRule set to true.
        /// </summary>
        internal static IEnumerable<ValidationRuleInfo> GetPropertyRules(
            this IEnumerable<IValidationRule> validationRules)
        {
            return validationRules
                .Where(rule => rule.HasNoCondition())
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

        internal static bool IsMatchesRule(this ValidationRuleInfo validationRuleInfo, string schemaPropertyName, ISchemaGenerationSettings schemaGenerationSettings)
        {
            if (schemaGenerationSettings.NameResolver != null && validationRuleInfo.ReflectionContext?.PropertyInfo is PropertyInfo propertyInfo)
            {
                var rulePropertyName = schemaGenerationSettings.NameResolver.GetPropertyName(propertyInfo);
                return rulePropertyName.EqualsIgnoreAll(schemaPropertyName);
            }
            else
            {
                var rulePropertyName = validationRuleInfo.PropertyRule.PropertyName;
                return rulePropertyName.EqualsIgnoreAll(schemaPropertyName);
            }
        }

        internal static IValidator? GetValidatorFromChildValidatorAdapter(this IChildValidatorAdaptor childValidatorAdapter)
        {
            // Try to validator with reflection.
            var childValidatorAdapterType = childValidatorAdapter.GetType();
            var genericTypeArguments = childValidatorAdapterType.GenericTypeArguments;
            if (genericTypeArguments.Length != 2)
                return null;

            var getValidatorGeneric = typeof(FluentValidationExtensions)
                .GetMethod(nameof(GetValidatorGeneric), BindingFlags.Static | BindingFlags.NonPublic)
                ?.MakeGenericMethod(genericTypeArguments[0]);

            if (getValidatorGeneric != null)
            {
                var validator = (IValidator)getValidatorGeneric.Invoke(null, new []{ childValidatorAdapter });
                return validator;
            }

            return null;
        }

        internal static IValidator? GetValidatorGeneric<T>(this IChildValidatorAdaptor childValidatorAdapter)
        {
            // public class ChildValidatorAdaptor<T,TProperty>
            // public virtual IValidator GetValidator(ValidationContext<T> context, TProperty value) {
            var getValidatorMethodName = nameof(ChildValidatorAdaptor<object, object>.GetValidator);
            var getValidatorMethod = childValidatorAdapter.GetType().GetMethod(getValidatorMethodName);
            if (getValidatorMethod != null)
            {
                // Fake context. We have not got real context because no validation yet.
                var fakeContext = new ValidationContext<T>(default);
                object? value = null;

                var validator = (IValidator)getValidatorMethod.Invoke(childValidatorAdapter, new[] { fakeContext, value });
                return validator;
            }

            return null;
        }        
    }
}