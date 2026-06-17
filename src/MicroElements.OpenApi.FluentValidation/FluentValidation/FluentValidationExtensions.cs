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
        /// Returns a <see cref="bool"/> indicating if the <paramref name="ruleComponent"/> is conditional.
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

        /// <summary>
        /// Determines whether nested validation is actually wired from <paramref name="rootValidator"/>
        /// down to a leaf member, following the <c>SetValidator</c>/<c>ChildRules</c> chain at every
        /// ancestor member. Returns <c>false</c> as soon as an ancestor has no child validator adapter:
        /// FluentValidation never auto-validates a child object from DI, so an unwired nested validator
        /// must not leak its rules (required, length, pattern, ...) into the OpenAPI document.
        /// Issue #211.
        /// </summary>
        /// <param name="rootValidator">Validator of the root (e.g. the <c>[FromQuery]</c>) type.</param>
        /// <param name="ancestorMemberNames">The path members from the root down to (but excluding) the leaf.</param>
        /// <param name="options">Schema generation options — used to honor the <see cref="ConditionalRulesMode"/>
        /// so a conditional <c>When()</c>/<c>Unless()</c> SetValidator is treated consistently with the rest of
        /// the pipeline (excluded by default).</param>
        /// <returns><c>true</c> when every ancestor wires a child validator for the next member.</returns>
        public static bool IsNestedValidationWired(IValidator rootValidator, IReadOnlyList<string> ancestorMemberNames, ISchemaGenerationOptions options)
        {
            IValidator? currentValidator = rootValidator;

            foreach (var memberName in ancestorMemberNames)
            {
                if (currentValidator == null)
                    return false;

                currentValidator = currentValidator.GetChildValidatorForMember(memberName, options);

                if (currentValidator == null)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Returns the child validator wired for <paramref name="memberName"/> via <c>SetValidator</c>/
        /// <c>ChildRules</c>, or <c>null</c> when the member has no child validator adapter. When several
        /// child adapters are attached to the member the first resolvable one wins (enough to answer
        /// "is it wired?"). Conditional rules/components are skipped unless the configured
        /// <see cref="ConditionalRulesMode"/> includes them, matching <c>GetValidationRules</c>/<c>GetValidators</c>.
        /// </summary>
        internal static IValidator? GetChildValidatorForMember(this IValidator validator, string memberName, ISchemaGenerationOptions options)
        {
            // ConditionalRulesMode.Exclude (default) drops .When()/.Unless() rules from the schema; mirror that
            // here so a conditional SetValidator is not reported as "wired" when its constraints would be excluded.
            var includeConditional = options.ConditionalRules != ConditionalRulesMode.Exclude;

            foreach (var rule in validator.CreateDescriptor().Rules)
            {
                if (rule.PropertyName is null || !rule.PropertyName.EqualsIgnoreAll(memberName))
                    continue;

                if (!includeConditional && !rule.HasNoCondition())
                    continue;

                foreach (var component in rule.Components.NotNull())
                {
                    if (!includeConditional && !component.HasNoCondition())
                        continue;

                    if (component.Validator is IChildValidatorAdaptor childAdapter)
                    {
                        var childValidator = childAdapter.GetValidatorFromChildValidatorAdapter();
                        if (childValidator != null)
                            return childValidator;
                    }
                }
            }

            return null;
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
                var validator = (IValidator)getValidatorGeneric.Invoke(null, new object[] { childValidatorAdapter });
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
                var fakeContext = new ValidationContext<T>(default!);
                object? value = null;

                var validator = (IValidator)getValidatorMethod.Invoke(childValidatorAdapter, new[] { fakeContext, value });
                return validator;
            }

            return null;
        }
    }
}