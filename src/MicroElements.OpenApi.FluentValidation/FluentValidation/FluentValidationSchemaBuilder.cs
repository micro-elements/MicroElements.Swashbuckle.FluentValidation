// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using FluentValidation.Validators;
using MicroElements.OpenApi.Core;
using Microsoft.Extensions.Logging;
#pragma warning disable SA1611
#pragma warning disable SA1618

namespace MicroElements.OpenApi.FluentValidation
{
    /// <summary>
    /// Schema builder extensions.
    /// </summary>
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
            ISchemaGenerationOptions schemaGenerationOptions = schemaGenerationContext.SchemaGenerationOptions;
            IReadOnlyList<IFluentValidationRule<TSchema>> fluentValidationRules = schemaGenerationContext.Rules;
            schemaPropertyNames ??= schemaGenerationContext.Properties;

            TypeContext typeContext = new TypeContext(schemaType, schemaGenerationOptions);
            ValidatorContext validatorContext = new ValidatorContext(typeContext, validator);

            var lazyLog = new LazyLog(logger, l => l.LogDebug("Applying FluentValidation rules to swagger schema '{SchemaTypeName}'", schemaTypeName));

            var validationRules = validatorContext
                .GetValidationRules()
                .Where(context => context.GetReflectionContext() != null)
                .ToArray();

            foreach (var schemaPropertyName in schemaPropertyNames)
            {
                var validationRuleInfoList = validationRules
                    .Where(propertyRule => propertyRule.IsMatchesRule(schemaPropertyName, schemaGenerationOptions))
                    .ToArrayDebug();

                foreach (var validationRuleContext in validationRuleInfoList)
                {
                    var propertyValidators = validationRuleContext
                        .GetValidators()
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
                                    if (!schema!.ContainsRuleHistoryItem(ruleHistoryItem))
                                    {
                                        lazyLog.LogOnce();

                                        IRuleContext<TSchema> ruleContext = schemaGenerationContext.Create(schemaPropertyName, validationRuleContext, propertyValidator);

                                        rule.Apply(ruleContext);

                                        logger.LogDebug("Rule '{RuleName}' applied for property '{SchemaTypeName}.{SchemaPropertyName}'", rule.Name, schemaTypeName, schemaPropertyName);
                                        schema!.AddRuleHistoryItem(ruleHistoryItem);
                                    }
                                    else
                                    {
                                        logger.LogDebug("Rule '{RuleName}' already applied for property '{SchemaTypeName}.{SchemaPropertyName}'", rule.Name, schemaTypeName, schemaPropertyName);
                                    }
                                }
                                catch (Exception e)
                                {
                                    logger.LogWarning(0, e, "Error on apply rule '{RuleName}' for property '{SchemaTypeName}.{SchemaPropertyName}'", rule.Name, schemaTypeName, schemaPropertyName);
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Adds rules from included validators.
        /// </summary>
        public static void AddRulesFromIncludedValidators<TSchema>(
            ValidatorContext validatorContext,
            ILogger logger,
            ISchemaGenerationContext<TSchema> schemaGenerationContext)
        {
            // Note: IValidatorDescriptor doesn't return IncludeRules so we need to get validators manually.
            var validationRules = validatorContext
                .GetValidationRules()
                .ToArrayDebug();

            var propertiesWithChildAdapters = validationRules
                .Select(context => (
                    context.ValidationRule,
                    context
                        .GetValidators()
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

                        if (childValidator == validator)
                        {
                            // Recursive validation works when using 'this' from SetValidator()
                            // https://github.com/FluentValidation/FluentValidation/issues/1568
                            // using a 'no-op' catch here seems safe and prevents the stack overflow exception
                            // I'm not sure if there is a more "optimal" action that could be taken instead of no-op though.
                        }
                        else if (canValidateInstancesOfType)
                        {
                            // It's a validator for current type (Include for example) so apply changes to current schema.
                            ApplyRulesToSchema(
                                schemaType: schemaGenerationContext.SchemaType,
                                schemaPropertyNames: schemaGenerationContext.Properties,
                                validator: childValidator,
                                logger: logger,
                                schemaGenerationContext: schemaGenerationContext);

                            AddRulesFromIncludedValidators(
                                validatorContext: new ValidatorContext(validatorContext.TypeContext, childValidator),
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
                                validatorContext: new ValidatorContext(validatorContext.TypeContext, childValidator),
                                logger: logger,
                                schemaGenerationContext: childSchemaContext);
                        }
                    }
                }
            }
        }
    }
}