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

            var lazyLog = new LazyLog(logger, l => l.LogDebug($"Applying FluentValidation rules to swagger schema '{schemaTypeName}'."));

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
                            if (rule.IsMatches(propertyValidator))
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

            var propertiesWithChildAdapters = validationRules
                .Select(rule => (rule, rule.Validators.OfType<IChildValidatorAdaptor>().ToArray()))
                .ToArrayDebug();

            foreach ((PropertyRule propertyRule, IChildValidatorAdaptor[] childAdapters) in propertiesWithChildAdapters)
            {
                foreach (var childAdapter in childAdapters)
                {
                    IValidator? childValidator = childAdapter.GetValidatorFromChildValidatorAdapter();
                    if (childValidator != null)
                    {
                        var canValidateInstancesOfType = childValidator.CanValidateInstancesOfType(schemaFilterContext.Type);

                        if (canValidateInstancesOfType)
                        {
                            // It's a validator for current type (Include for example) so apply changes to current schema.
                            ApplyRulesToSchema(
                                schema: schema,
                                schemaType: schemaFilterContext.Type,
                                schemaPropertyNames: null,
                                schemaFilterContext: schemaFilterContext,
                                validator: childValidator,
                                rules: rules,
                                logger: logger);

                            AddRulesFromIncludedValidators(
                                schema: schema,
                                schemaFilterContext: schemaFilterContext,
                                validator: childValidator,
                                rules: rules,
                                logger: logger);
                        }
                        else
                        {
                            // It's a validator for sub schema so get schema and apply changes to it.
                            var schemaForChildValidator = GetSchemaForType(
                                schemaRepository: schemaFilterContext.SchemaRepository,
                                schemaGenerator: schemaFilterContext.SchemaGenerator,
                                schemaIdSelector: type => type.Name,
                                parameterType: propertyRule.TypeToValidate);

                            ApplyRulesToSchema(
                                schema: schemaForChildValidator,
                                schemaType: propertyRule.TypeToValidate,
                                schemaPropertyNames: null,
                                schemaFilterContext: schemaFilterContext,
                                validator: childValidator,
                                rules: rules,
                                logger: logger);

                            AddRulesFromIncludedValidators(
                                schema: schemaForChildValidator,
                                schemaFilterContext: schemaFilterContext,
                                validator: childValidator,
                                rules: rules,
                                logger: logger);
                        }
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