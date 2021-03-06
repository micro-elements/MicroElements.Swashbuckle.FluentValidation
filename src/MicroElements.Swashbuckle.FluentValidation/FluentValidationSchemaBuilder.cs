// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using FluentValidation.Internal;
using FluentValidation.Validators;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MicroElements.Swashbuckle.FluentValidation
{
    /// <summary>
    /// OpenApi schema builder.
    /// </summary>
    public static class FluentValidationSchemaBuilder
    {
        /// <summary>
        /// Applies rules from validator.
        /// </summary>
        internal static void ApplyRulesToSchema(
            OpenApiSchema schema,
            Type schemaType,
            IEnumerable<string>? schemaPropertyNames,
            SchemaFilterContext? schemaFilterContext,
            IValidator validator,
            IReadOnlyCollection<FluentValidationRule> rules,
            ILogger logger)
        {
            var schemaTypeName = schemaType.Name;

            var lazyLog = new LazyLog(logger,
                l => l.LogDebug($"Applying FluentValidation rules to swagger schema '{schemaTypeName}'."));

            schemaPropertyNames ??= schema.Properties?.Keys ?? Array.Empty<string>();
            foreach (var schemaPropertyName in schemaPropertyNames)
            {
                var validationRules = validator.GetValidationRulesForMemberIgnoreCase(schemaPropertyName).ToArrayDebug();

                foreach (var ruleContext in validationRules)
                {
                    var propertyValidators = ruleContext.PropertyRule.Validators;
                    foreach (var propertyValidator in propertyValidators)
                    {
                        foreach (var rule in rules)
                        {
                            if (rule.Matches(propertyValidator))
                            {
                                try
                                {
                                    var ruleHistoryItem = new RuleHistoryCache.RuleHistoryItem(schemaTypeName, schemaPropertyName, propertyValidator, rule.Name);
                                    if (!schema.ContainsRuleHistoryItem(ruleHistoryItem))
                                    {
                                        lazyLog.LogOnce();

                                        rule.Apply(new RuleContext(schema, schemaPropertyName, propertyValidator, schemaFilterContext, isCollectionValidator: ruleContext.IsCollectionRule));

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

        internal static void AddRulesFromIncludedValidators(
            OpenApiSchema schema,
            SchemaFilterContext schemaFilterContext,
            IValidator validator,
            IReadOnlyCollection<FluentValidationRule> rules,
            ILogger logger)
        {
            // Note: IValidatorDescriptor doesn't return IncludeRules so we need to get validators manually.
            var validationRules = (validator as IEnumerable<IValidationRule>)
                .NotNull()
                .OfType<PropertyRule>()
                .Where(includeRule => includeRule.HasNoCondition())
                .ToArray();

            var childAdapters = validationRules
                .Where(rule => rule is IIncludeRule)
                .SelectMany(includeRule => includeRule.Validators)
                .OfType<IChildValidatorAdaptor>();

            var childAdapters2 = validationRules
                .SelectMany(rule => rule.Validators)
                .OfType<IChildValidatorAdaptor>()
                .ToArrayDebug();

            childAdapters = childAdapters.Concat(childAdapters2);

            foreach (var childAdapter in childAdapters)
            {
                IValidator? includedValidator = childAdapter.GetValidatorFromChildValidatorAdapter();
                if (includedValidator != null)
                {
                    var canValidateInstancesOfType = includedValidator.CanValidateInstancesOfType(schemaFilterContext.Type);

                    if (canValidateInstancesOfType)
                    {
                        ApplyRulesToSchema(
                            schema: schema,
                            schemaType: schemaFilterContext.Type,
                            schemaPropertyNames: null,
                            schemaFilterContext: schemaFilterContext,
                            validator: includedValidator,
                            rules: rules,
                            logger: logger);

                        AddRulesFromIncludedValidators(
                            schema: schema,
                            schemaFilterContext: schemaFilterContext,
                            validator: includedValidator,
                            rules: rules,
                            logger: logger);
                    }
                    else
                    {
                        // GetSchemaForType, ApplyRulesToSchema
                    }
                }
            }
        }

        internal static IValidator? GetValidatorFromChildValidatorAdapter(this IChildValidatorAdaptor childValidatorAdapter)
        {
            // Fake context. We have not got real context because no validation yet.
            var fakeContext = new PropertyValidatorContext(new ValidationContext<object>(null), null, string.Empty);

            // Try to validator with reflection.
            var childValidatorAdapterType = childValidatorAdapter.GetType();
            var getValidatorMethod = childValidatorAdapterType.GetMethod(nameof(ChildValidatorAdaptor<object, object>.GetValidator));
            if (getValidatorMethod != null)
            {
                var validator = (IValidator)getValidatorMethod.Invoke(childValidatorAdapter, new[] {fakeContext});
                return validator;
            }

            return null;
        }

        public static OpenApiSchema GetSchemaForType(
            SchemaRepository schemaRepository,
            ISchemaGenerator schemaGenerator,
            Func<Type, string> schemaIdSelector,
            Type parameterType)
        {
            var schemaId = schemaIdSelector(parameterType);

            if (!schemaRepository.Schemas.TryGetValue(schemaId, out OpenApiSchema schema))
            {
                schema = schemaGenerator.GenerateSchema(parameterType, schemaRepository);
            }

            if ((schema.Properties == null || schema.Properties.Count == 0) &&
                schemaRepository.Schemas.ContainsKey(schemaId))
            {
                schema = schemaRepository.Schemas[schemaId];
            }

            return schema;
        }
    }
}