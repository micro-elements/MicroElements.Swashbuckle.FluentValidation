// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using MicroElements.OpenApi.Core;
using Microsoft.Extensions.DependencyInjection;

namespace MicroElements.OpenApi.FluentValidation
{
    public static class ValidationRuleContextExtensions
    {
        public static bool IsCollectionRule(this ValidationRuleContext ruleContext)
        {
            // CollectionPropertyRule<T, TElement> is also a PropertyRule.
            return ruleContext.ValidationRule.GetType().Name.StartsWith("CollectionPropertyRule");
        }

        public static ReflectionContext? GetReflectionContext(this ValidationRuleContext ruleContext)
        {
            var ruleMember = ruleContext.ValidationRule.Member;
            return ruleMember != null ? new ReflectionContext(type: ruleMember.ReflectedType, propertyInfo: ruleMember) : null;
        }
    }

    public static class SchemaGenerationOptionsExtensions
    {
        /// <summary>
        /// Sets default values for the <see cref="SchemaGenerationOptions"/>.
        /// </summary>
        /// <param name="options">Options to fill.</param>
        /// <param name="serviceProvider">The service provider for getting some services.</param>
        /// <returns>The same instance.</returns>
        public static SchemaGenerationOptions FillDefaultValues(this SchemaGenerationOptions options, IServiceProvider serviceProvider)
        {
            options.NameResolver ??= serviceProvider.GetService<IServicesContext>()?.NameResolver;

            options.ValidatorFilter ??= new Condition<ValidatorContext>(context => context.Validator.CanValidateInstancesOfType(context.TypeContext.TypeToValidate));
            options.RuleFilter ??= new Condition<ValidationRuleContext>(context => context.ValidationRule.HasNoCondition());
            options.RuleComponentFilter ??= new Condition<RuleComponentContext>(context => context.RuleComponent.HasNoCondition());

            return options;
        }

        /// <summary>
        /// Sets values for options from other options.
        /// </summary>
        /// <param name="options">Options to fill.</param>
        /// <param name="other">Options that contains values to set to options.</param>
        /// <returns>The same options instance.</returns>
        public static SchemaGenerationOptions SetFrom(this SchemaGenerationOptions options, ISchemaGenerationOptions other)
        {
            options.SetNotNullableIfMinLengthGreaterThenZero = other.SetNotNullableIfMinLengthGreaterThenZero;
            options.UseAllOfForMultipleRules = other.UseAllOfForMultipleRules;
            options.ValidatorSearch = other.ValidatorSearch;
            options.NameResolver = other.NameResolver;
            options.SchemaIdSelector = other.SchemaIdSelector;
            options.ValidatorFilter = other.ValidatorFilter;
            options.RuleFilter = other.RuleFilter;
            options.RuleComponentFilter = other.RuleComponentFilter;
            return options;
        }
    }
}