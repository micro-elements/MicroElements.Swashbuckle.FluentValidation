// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
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
        private static readonly IEnumerable<Type> _defaultAllowedConditionalValidatorTypes = new Type[]
        {
            typeof(ILengthValidator),
            typeof(IRegularExpressionValidator),
            typeof(IComparisonValidator),
            typeof(IEmailValidator),
            typeof(IBetweenValidator),
        };

        private static readonly ConditionalWeakTable<IValidator, IEnumerable<Type>> _allowedConditionalValidators = new ConditionalWeakTable<IValidator, IEnumerable<Type>>();

        private static readonly ConditionalWeakTable<IValidationRule, IEnumerable<Type>> _allowedConditionalRules = new ConditionalWeakTable<IValidationRule, IEnumerable<Type>>();

        private static readonly ConditionalWeakTable<IValidationRule, object> _excludedConditionalRules = new ConditionalWeakTable<IValidationRule, object>();

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

        public static TValidator IncludeConditionalRulesInSchema<TValidator>(this TValidator validator)
            where TValidator : IValidator
        {
            return validator.IncludeConditionalRulesInSchema(_defaultAllowedConditionalValidatorTypes);
        }

        public static TValidator IncludeConditionalRulesInSchema<TValidator>(
            this TValidator validator,
            IEnumerable<Type> allowedConditionalValidatorTypes)
            where TValidator : IValidator
        {
            if (!_allowedConditionalValidators.TryGetValue(validator, out var _))
            {
                _allowedConditionalValidators.Add(validator, allowedConditionalValidatorTypes);
            }

            return validator;
        }

        public static IRuleBuilder<T, TProperty> IncludeConditionalRuleInSchema<T, TProperty>(this IRuleBuilder<T, TProperty> ruleBuilder)
        {
            return ruleBuilder.IncludeConditionalRuleInSchema(_defaultAllowedConditionalValidatorTypes);
        }

        public static IRuleBuilder<T, TProperty> IncludeConditionalRuleInSchema<T, TProperty>(
            this IRuleBuilder<T, TProperty> ruleBuilder,
            IEnumerable<Type> allowedConditionalValidatorTypes)
        {
            if (TryGetRuleFromBuilder(ruleBuilder, out IValidationRule<T, TProperty> rule)
             && !_allowedConditionalRules.TryGetValue(rule, out var _))
            {
                _allowedConditionalRules.Add(rule, allowedConditionalValidatorTypes);
            }

            return ruleBuilder;
        }

        public static IRuleBuilder<T, TProperty> ExcludeConditionalRuleFromSchema<T, TProperty>(this IRuleBuilder<T, TProperty> ruleBuilder)
        {
            if (TryGetRuleFromBuilder(ruleBuilder, out IValidationRule<T, TProperty> rule)
             && !_excludedConditionalRules.TryGetValue(rule, out var _))
            {
                _excludedConditionalRules.Add(rule, null!);
            }

            return ruleBuilder;
        }

        private static bool TryGetRuleFromBuilder<T, TProperty>(
            IRuleBuilder<T, TProperty> builder,
            out IValidationRule<T, TProperty> rule)
        {
            rule = null!;
            object? value = builder.GetType().GetProperty("Rule")?.GetValue(builder, null);

            if (value is IValidationRule<T, TProperty> ruleTmp)
            {
                rule = ruleTmp;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Returns all IValidationRules that are PropertyRule.
        /// If rule is CollectionPropertyRule then isCollectionRule set to true.
        /// </summary>
        internal static IEnumerable<ValidationRuleInfo> GetPropertyRules(
            this IEnumerable<IValidationRule> validationRules)
        {
            return validationRules
                .Where(rule =>
                {
                    if (rule.HasNoCondition()) return true;
                    if (validationRules is not IValidator validator) return false;

                    return _allowedConditionalValidators.TryGetValue(validator, out IEnumerable<Type> _);
                })
                .Select(rule =>
                {
                    // CollectionPropertyRule<T, TElement> is also a PropertyRule.
                    var isCollectionRule = rule.GetType().Name.StartsWith("CollectionPropertyRule");

                    ReflectionContext? reflectionContext = null;
                    if (rule.Member != null)
                    {
                        reflectionContext = ReflectionContext.CreateFromProperty(propertyInfo: rule.Member);
                    }

                    IValidator? validator = null;
                    if (validationRules is IValidator)
                    {
                        validator = validationRules as IValidator;
                    }

                    return new ValidationRuleInfo(rule, isCollectionRule, reflectionContext, validator);
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
        public static IEnumerable<IPropertyValidator> GetValidators(
            this IValidationRule validationRule,
            ValidationRuleInfo context)
        {
            return validationRule
                .Components
                .NotNull()
                .Where(component =>
                {
                    if (component.HasNoCondition()) return true;

                    IValidator? validator = context.Validator;
                    if (validator is null) return false;

                    if (_allowedConditionalValidators.TryGetValue(
                        validator,
                        out IEnumerable<Type> allowedConditionalValidatorTypes))
                    {
                        bool isExcluded = _excludedConditionalRules.TryGetValue(validationRule, out object _);
                        if (isExcluded) return false;

                        return component.Validator.IsAllowedAsConditionalValidator(allowedConditionalValidatorTypes);
                    }

                    if (_allowedConditionalRules.TryGetValue(
                        validationRule,
                        out IEnumerable<Type> allowedConditionalValidatorTypesForRule))
                    {
                        return component.Validator.IsAllowedAsConditionalValidator(allowedConditionalValidatorTypesForRule);
                    }

                    return false;
                })
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
                var validator = (IValidator)getValidatorGeneric.Invoke(null, new[] { childValidatorAdapter });
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

        private static bool IsAllowedAsConditionalValidator(
              this IPropertyValidator validator,
              IEnumerable<Type> allowedConditionalValidatorTypes)
        {
            Type validatorType = validator.GetType();
            return allowedConditionalValidatorTypes.Any(x => x.IsAssignableFrom(validatorType))
                || typeof(IChildValidatorAdaptor).IsAssignableFrom(validatorType);
        }
    }
}