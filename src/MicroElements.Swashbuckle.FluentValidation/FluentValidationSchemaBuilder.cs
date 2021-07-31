// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FluentValidation;
using FluentValidation.Validators;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;

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
            Type schemaType,
            IEnumerable<string>? schemaPropertyNames,
            IValidator validator,
            ILogger logger,
            SchemaGenerationContext schemaGenerationContext)
        {
            OpenApiSchema schema = schemaGenerationContext.Schema;
            var schemaTypeName = schemaType.Name;

            var lazyLog = new LazyLog(logger, l => l.LogDebug($"Applying FluentValidation rules to swagger schema '{schemaTypeName}'."));

            var validationRules = validator
                .GetValidationRules()
                .Where(context => context.ReflectionContext != null)
                .ToArray();

            schemaPropertyNames ??= schema.Properties?.Keys ?? Array.Empty<string>();
            foreach (var schemaPropertyName in schemaPropertyNames)
            {
                var validationRuleInfoList = validationRules
                    .Where(propertyRule => IsMatchesRule(propertyRule, schemaPropertyName, schemaGenerationContext.SchemaGenerationSettings));

                foreach (var validationRuleInfo in validationRuleInfoList)
                {
                    var propertyValidators = validationRuleInfo.PropertyRule.GetValidators();

                    foreach (var propertyValidator in propertyValidators)
                    {
                        foreach (var rule in schemaGenerationContext.Rules)
                        {
                            if (rule.IsMatches(propertyValidator))
                            {
                                try
                                {
                                    var ruleHistoryItem = new RuleHistoryCache.RuleHistoryItem(schemaTypeName, schemaPropertyName, propertyValidator, rule.Name);
                                    if (!schema.ContainsRuleHistoryItem(ruleHistoryItem))
                                    {
                                        lazyLog.LogOnce();

                                        var ruleContext = new RuleContext(
                                            schema: schema,
                                            propertyKey: schemaPropertyName,
                                            validationRuleInfo: validationRuleInfo,
                                            propertyValidator: propertyValidator,
                                            reflectionContext: validationRuleInfo.ReflectionContext);
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

        internal static bool IsMatchesRule(ValidationRuleInfo validationRuleInfo, string schemaPropertyName, ISchemaGenerationSettings schemaGenerationSettings)
        {
            if (schemaGenerationSettings.NameResolver != null && validationRuleInfo.ReflectionContext?.PropertyInfo is PropertyInfo propertyInfo)
            {
                var propertyName = schemaGenerationSettings.NameResolver.GetPropertyName(propertyInfo);
                if (propertyName.EqualsIgnoreAll(schemaPropertyName))
                    return true;
            }

            return validationRuleInfo.PropertyRule.PropertyName.EqualsIgnoreAll(schemaPropertyName);
        }

        internal static void AddRulesFromIncludedValidators(
            IValidator validator,
            ILogger logger,
            SchemaGenerationContext schemaGenerationContext)
        {
            // Note: IValidatorDescriptor doesn't return IncludeRules so we need to get validators manually.
            var validationRules = validator
                .GetValidationRules()
                .ToArrayDebug();

            var propertiesWithChildAdapters = validationRules
                .Select(context => (context.PropertyRule, context.PropertyRule.GetValidators().OfType<IChildValidatorAdaptor>().ToArray()))
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
                                schemaPropertyNames: null,
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

                            var childSchemaContext = schemaGenerationContext with
                            {
                                Schema = schemaForChildValidator
                            };

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

        internal static IValidator? GetValidatorFromChildValidatorAdapter(this IChildValidatorAdaptor childValidatorAdapter)
        {
            // Try to validator with reflection.
            var childValidatorAdapterType = childValidatorAdapter.GetType();
            var genericTypeArguments = childValidatorAdapterType.GenericTypeArguments;
            if (genericTypeArguments.Length != 2)
                return null;

            var getValidatorGeneric = typeof(FluentValidationSchemaBuilder)
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