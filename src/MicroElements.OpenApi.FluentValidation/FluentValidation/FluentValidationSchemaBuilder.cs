// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using FluentValidation.Validators;
using MicroElements.OpenApi.Core;
using Microsoft.Extensions.Logging;

namespace MicroElements.OpenApi.FluentValidation
{
    public static class FluentValidationSchemaBuilder
    {
        /// <summary>
        /// Applies rules from validator.
        /// </summary>
        public static void ApplyRulesToSchema<TSchema>(
            Type schemaType,
            IEnumerable<string>? schemaPropertyNames,
            IValidator validator,
            ILogger logger,
            ISchemaGenerationContext<TSchema> schemaGenerationContext)
        {
            var schemaTypeName = schemaType.Name;
            TSchema schema = schemaGenerationContext.Schema;
            var schemaGenerationOptions = schemaGenerationContext.SchemaGenerationOptions;
            IReadOnlyList<IFluentValidationRule<TSchema>> fluentValidationRules = schemaGenerationContext.Rules;
            schemaPropertyNames ??= schemaGenerationContext.Properties;

            var lazyLog = new LazyLog(logger, l => l.LogDebug($"Applying FluentValidation rules to swagger schema '{schemaTypeName}'."));

            var validationRules = validator
                .GetValidationRules(schemaGenerationOptions)
                .Where(context => context.ReflectionContext != null)
                .ToArray();

            foreach (var schemaPropertyName in schemaPropertyNames)
            {
                var validationRuleInfoList = validationRules
                    .Where(propertyRule => propertyRule.IsMatchesRule(schemaPropertyName, schemaGenerationOptions))
                    .ToArrayDebug();

                foreach (var validationRuleInfo in validationRuleInfoList)
                {
                    var propertyValidators = validationRuleInfo
                        .PropertyRule
                        .GetValidators(schemaGenerationOptions)
                        .ToArrayDebug();

                    foreach (var propertyValidator in propertyValidators)
                    {
                        foreach (var rule in fluentValidationRules)
                        {
                            if (rule.IsMatches(propertyValidator) && rule.Apply is not null)
                            {
                                try
                                {
                                    var ruleHistoryItem = new RuleHistoryCache.RuleCacheItem(schemaTypeName, schemaPropertyName, propertyValidator, rule.Name);
                                    if (!schema.ContainsRuleHistoryItem(ruleHistoryItem))
                                    {
                                        lazyLog.LogOnce();

                                        IRuleContext<TSchema> ruleContext = schemaGenerationContext.Create(schemaPropertyName, validationRuleInfo, propertyValidator);

                                        rule.Apply(ruleContext);

                                        logger.LogDebug($"Rule '{rule.Name}' applied for property '{schemaTypeName}.{schemaPropertyName}'.");
                                        schema.AddRuleHistoryItem(ruleHistoryItem);
                                    }
                                    else
                                    {
                                        logger.LogDebug($"Rule '{rule.Name}' already applied for property '{schemaTypeName}.{schemaPropertyName}'.");
                                    }
                                }
                                catch (Exception e)
                                {
                                    logger.LogWarning(0, e, $"Error on apply rule '{rule.Name}' for property '{schemaTypeName}.{schemaPropertyName}'.");
                                }
                            }
                        }
                    }
                }
            }
        }

        public static void AddRulesFromIncludedValidators<TSchema>(
            IValidator validator,
            ILogger logger,
            ISchemaGenerationContext<TSchema> schemaGenerationContext)
        {
            // Note: IValidatorDescriptor doesn't return IncludeRules so we need to get validators manually.
            var validationRules = validator
                .GetValidationRules(schemaGenerationContext.SchemaGenerationOptions)
                .ToArrayDebug();

            var propertiesWithChildAdapters = validationRules
                .Select(context => (
                    context.PropertyRule,
                    context
                        .PropertyRule
                        .GetValidators(schemaGenerationContext.SchemaGenerationOptions)
                        .OfType<IChildValidatorAdaptor>()
                        .ToArray()))
                .ToArrayDebug();

            foreach ((IValidationRule propertyRule, IChildValidatorAdaptor[] childAdapters) in propertiesWithChildAdapters)
            {
                foreach (var childAdapter in childAdapters)
                {
                    IValidator? childValidator = childAdapter.GetValidatorFromChildValidatorAdapter();
                    if (childValidator != null)
                    {
                        var canValidateInstancesOfType = childValidator.CanValidateInstancesOfType(schemaGenerationContext.SchemaType);

                        if (canValidateInstancesOfType)
                        {
                            // It's a validator for current type (Include for example) so apply changes to current schema.
                            ApplyRulesToSchema(
                                schemaType: schemaGenerationContext.SchemaType,
                                schemaPropertyNames: schemaGenerationContext.Properties,
                                validator: childValidator,
                                logger: logger,
                                schemaGenerationContext: schemaGenerationContext);

                            AddRulesFromIncludedValidators(
                                validator: childValidator,
                                logger: logger,
                                schemaGenerationContext: schemaGenerationContext);
                        }
                        else
                        {
                            // It's a validator for sub schema so get schema and apply changes to it.
                            var schemaForChildValidator = schemaGenerationContext.SchemaProvider.GetSchemaForType(propertyRule.TypeToValidate);

                            var childSchemaContext = schemaGenerationContext.With(schemaForChildValidator);

                            ApplyRulesToSchema(
                                schemaType: propertyRule.TypeToValidate,
                                schemaPropertyNames: null,
                                validator: childValidator,
                                logger: logger,
                                schemaGenerationContext: childSchemaContext);

                            AddRulesFromIncludedValidators(
                                validator: childValidator,
                                logger: logger,
                                schemaGenerationContext: childSchemaContext);
                        }
                    }
                }
            }
        }
    }
}