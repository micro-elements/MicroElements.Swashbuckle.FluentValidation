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
        /// <param name="validatorContext">Validator.</param>
        /// <returns>enumeration.</returns>
        public static IEnumerable<ValidationRuleContext> GetValidationRules(this ValidatorContext validatorContext)
        {
            var ruleFilter = validatorContext.TypeContext.SchemaGenerationOptions.RuleFilter.NotNull();

            var validationRules = validatorContext
                .Validator
                .CreateDescriptor()
                .Rules
                .Select(rule => new ValidationRuleContext(validatorContext, rule))
                .Where(ruleContext => ruleFilter.Matches(ruleContext));

            return validationRules;
        }

        /// <summary>
        /// Gets validators for <see cref="IValidationRule"/>.
        /// </summary>
        /// <param name="validationRuleContext">Validator.</param>
        /// <returns>enumeration.</returns>
        public static IEnumerable<IPropertyValidator> GetValidators(this ValidationRuleContext validationRuleContext)
        {
            return validationRuleContext
                .ValidationRule
                .Components
                .NotNull()
                .Where(component => validationRuleContext.SchemaGenerationOptions.RuleComponentFilter.NotNull().Matches(new RuleComponentContext(validationRuleContext.ValidatorContext, component)))
                .Select(component => component.Validator);
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

        internal static bool IsMatchesRule(this ValidationRuleContext validationRuleInfo, string schemaPropertyName, ISchemaGenerationOptions schemaGenerationOptions)
        {
            if (schemaGenerationOptions.NameResolver is { } nameResolver
                && validationRuleInfo.GetReflectionContext()?.PropertyInfo is PropertyInfo propertyInfo)
            {
                var rulePropertyName = nameResolver.GetPropertyName(propertyInfo);
                return rulePropertyName.EqualsIgnoreAll(schemaPropertyName);
            }
            else
            {
                var rulePropertyName = validationRuleInfo.ValidationRule.PropertyName;
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
                var validator = (IValidator)getValidatorGeneric.Invoke(null, new [] { childValidatorAdapter });
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