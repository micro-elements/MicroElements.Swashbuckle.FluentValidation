// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Reflection;
using FluentValidation;
using FluentValidation.Validators;
using Microsoft.OpenApi.Models;

namespace MicroElements.Swashbuckle.FluentValidation
{
    /// <summary>
    /// RuleContext.
    /// </summary>
    public class RuleContext
    {
        /// <summary>
        /// Gets OpenApi (swagger) schema.
        /// </summary>
        public OpenApiSchema Schema { get; }

        /// <summary>
        /// Gets property name in schema.
        /// </summary>
        public string PropertyKey { get; }

        /// <summary>
        /// Gets <see cref="IValidationRule"/> with extended information.
        /// </summary>
        public ValidationRuleInfo ValidationRuleInfo { get; }

        /// <summary>
        /// Gets property validator for property in schema.
        /// </summary>
        public IPropertyValidator PropertyValidator { get; }

        /// <summary>
        /// Gets a value indicating whether the <see cref="PropertyValidator"/> should be applied to collection item instead of property.
        /// </summary>
        public bool IsCollectionValidator => ValidationRuleInfo.IsCollectionRule;

        /// <summary>
        /// Gets target property schema.
        /// </summary>
        public OpenApiSchema Property => !IsCollectionValidator ? Schema.Properties[PropertyKey] : Schema.Properties[PropertyKey].Items;

        /// <summary>
        /// Gets reflection context.
        /// </summary>
        public ReflectionContext ReflectionContext { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="RuleContext"/> class.
        /// </summary>
        /// <param name="schema">Swagger schema.</param>
        /// <param name="propertyKey">Property name.</param>
        /// <param name="validationRuleInfo">ValidationRuleInfo.</param>
        /// <param name="propertyValidator">Property validator.</param>
        /// <param name="reflectionContext">Reflection context.</param>
        public RuleContext(
            OpenApiSchema schema,
            string propertyKey,
            ValidationRuleInfo validationRuleInfo,
            IPropertyValidator propertyValidator,
            ReflectionContext? reflectionContext = null)
        {
            Schema = schema;
            PropertyKey = propertyKey;
            ValidationRuleInfo = validationRuleInfo;
            PropertyValidator = propertyValidator;
            ReflectionContext = reflectionContext;
        }
    }

    /// <summary>
    /// Reflection context for <see cref="RuleContext"/>.
    /// </summary>
    public class ReflectionContext
    {
        /// <summary>
        /// Gets the type (schema type).
        /// </summary>
        public Type? Type { get; }

        /// <summary>
        /// Gets optional PropertyInfo.
        /// </summary>
        public MemberInfo? PropertyInfo { get; }

        /// <summary>
        /// Gets optional ParameterInfo.
        /// </summary>
        public ParameterInfo? ParameterInfo { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ReflectionContext"/> class.
        /// </summary>
        /// <param name="type">Schema type.</param>
        /// <param name="propertyInfo">Optional PropertyInfo.</param>
        /// <param name="parameterInfo">Optional ParameterInfo.</param>
        public ReflectionContext(
            Type? type = null,
            MemberInfo? propertyInfo = null,
            ParameterInfo? parameterInfo = null)
        {
            Type = type;
            PropertyInfo = propertyInfo;
            ParameterInfo = parameterInfo;
        }

        public static ReflectionContext CreateFromProperty(MemberInfo propertyInfo)
        {
            return new ReflectionContext(
                type: propertyInfo.ReflectedType,
                propertyInfo: propertyInfo);
        }
    }
}