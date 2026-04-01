// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using MicroElements.OpenApi.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MicroElements.OpenApi.FluentValidation
{
    /// <summary>
    /// Options extensions.
    /// </summary>
    public static class SchemaGenerationOptionsExtensions
    {
        /// <summary>
        /// Sets default values for the <see cref="SchemaGenerationOptions"/>.
        /// </summary>
        /// <param name="options">Options to fill.</param>
        /// <param name="serviceProvider">The service provider for getting some services.</param>
        /// <returns>The same instance.</returns>
        public static SchemaGenerationOptions FillDefaultValues(this SchemaGenerationOptions options, IServiceProvider? serviceProvider)
        {
            options.NameResolver ??= serviceProvider?.GetService<IServicesContext>()?.NameResolver;

            // ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
            options.ValidatorSearch ??= ValidatorSearchSettings.Default;
            options.ValidatorFilter ??= new Condition<ValidatorContext>(context => context.Validator.CanValidateInstancesOfType(context.TypeContext.TypeToValidate));
            options.RuleFilter ??= options.ConditionalRules switch
            {
                ConditionalRulesMode.Exclude => new Condition<ValidationRuleContext>(context => context.ValidationRule.HasNoCondition()),
                ConditionalRulesMode.IncludeWithWarning => CreateLoggingRuleFilter(serviceProvider),
                _ => Condition.Empty<ValidationRuleContext>(),
            };

            options.RuleComponentFilter ??= options.ConditionalRules switch
            {
                ConditionalRulesMode.Exclude => new Condition<RuleComponentContext>(context => context.RuleComponent.HasNoCondition()),
                ConditionalRulesMode.IncludeWithWarning => CreateLoggingComponentFilter(serviceProvider),
                _ => Condition.Empty<RuleComponentContext>(),
            };

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
            options.SetNotNullableIfMinimumGreaterThenZero = other.SetNotNullableIfMinimumGreaterThenZero;
            options.UseAllOfForMultipleRules = other.UseAllOfForMultipleRules;
            options.ValidatorSearch = other.ValidatorSearch;
            options.NameResolver = other.NameResolver;
            options.SchemaIdSelector = other.SchemaIdSelector;
            options.ValidatorFilter = other.ValidatorFilter;
            options.RuleFilter = other.RuleFilter;
            options.RuleComponentFilter = other.RuleComponentFilter;
            options.RemoveUnusedQuerySchemas = other.RemoveUnusedQuerySchemas;
            options.ConditionalRules = other.ConditionalRules;
            return options;
        }

        private static ICondition<ValidationRuleContext> CreateLoggingRuleFilter(IServiceProvider? serviceProvider)
        {
            var logger = serviceProvider?.GetService<ILoggerFactory>()?.CreateLogger("MicroElements.OpenApi.FluentValidation.ConditionalRules");
            return new Condition<ValidationRuleContext>(context =>
            {
                if (!context.ValidationRule.HasNoCondition())
                {
                    logger?.LogWarning(
                        "Conditional validation rule for '{PropertyName}' included in schema. The .When()/.Unless() condition cannot be represented in OpenAPI schema.",
                        context.ValidationRule.PropertyName);
                }

                return true;
            });
        }

        private static ICondition<RuleComponentContext> CreateLoggingComponentFilter(IServiceProvider? serviceProvider)
        {
            var logger = serviceProvider?.GetService<ILoggerFactory>()?.CreateLogger("MicroElements.OpenApi.FluentValidation.ConditionalRules");
            return new Condition<RuleComponentContext>(context =>
            {
                if (!context.RuleComponent.HasNoCondition())
                {
                    logger?.LogWarning("Conditional validation rule component included in schema. The .When()/.Unless() condition cannot be represented in OpenAPI schema.");
                }

                return true;
            });
        }
    }
}
