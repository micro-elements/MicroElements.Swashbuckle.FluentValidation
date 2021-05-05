// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using FluentValidation;
using FluentValidation.Validators;
using MicroElements.Swashbuckle.FluentValidation.Generation;
using Microsoft.OpenApi.Models;

namespace MicroElements.Swashbuckle.FluentValidation
{
    public record SchemaGenerationContext
    {
        /// <summary>
        /// Gets OpenApi schema.
        /// </summary>
        public OpenApiSchema Schema { get; init; }

        /// <summary>
        /// Schema type.
        /// </summary>
        public Type SchemaType { get; init; }

        public ReflectionContext ReflectionContext { get; init; }

        public IReadOnlyList<FluentValidationRule> Rules { get; init; }

        public ISchemaGenerationOptions SchemaGenerationOptions { get; init; }

        public ISchemaProvider<OpenApiSchema> SchemaProvider { get; init; }
}

    /// <summary>
    /// Contains <see cref="PropertyRule"/> and additional info.
    /// </summary>
    public record ValidationRuleContext
    {
        /// <summary>
        /// PropertyRule.
        /// </summary>
        public IValidationRule PropertyRule { get; init; }

        /// <summary>
        /// Flag indication whether the <see cref="PropertyRule"/> is the CollectionRule.
        /// </summary>
        public bool IsCollectionRule { get; init; }

        /// <summary>
        /// Reflection context.
        /// </summary>
        public ReflectionContext ReflectionContext { get; init; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ValidationRuleContext"/> class.
        /// </summary>
        /// <param name="propertyRule">PropertyRule.</param>
        /// <param name="isCollectionRule">Is a CollectionPropertyRule.</param>
        /// <param name="reflectionContext">Reflection context.</param>
        public ValidationRuleContext(
            IValidationRule propertyRule,
            bool isCollectionRule,
            ReflectionContext reflectionContext)
        {
            PropertyRule = propertyRule;
            IsCollectionRule = isCollectionRule;
            ReflectionContext = reflectionContext;
        }
    }

    /// <summary>
    /// RuleContext.
    /// </summary>
    public class RuleContext
    {
        /// <summary>
        /// Swagger schema.
        /// </summary>
        public OpenApiSchema Schema { get; }

        /// <summary>
        /// Property name.
        /// </summary>
        public string PropertyKey { get; }

        /// <summary>
        /// Property validator.
        /// </summary>
        public IPropertyValidator PropertyValidator { get; }

        /// <summary>
        /// Gets value indicating that <see cref="PropertyValidator"/> should be applied to collection item instead of property.
        /// </summary>
        public bool IsCollectionValidator { get; }

        /// <summary>
        /// Gets target property schema.
        /// </summary>
        public OpenApiSchema Property => !IsCollectionValidator ? Schema.Properties[PropertyKey] : Schema.Properties[PropertyKey].Items;

        /// <summary>
        /// Reflection context.
        /// </summary>
        public ReflectionContext ReflectionContext { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="RuleContext"/> class.
        /// </summary>
        /// <param name="schema">Swagger schema.</param>
        /// <param name="propertyKey">Property name.</param>
        /// <param name="propertyValidator">Property validator.</param>
        /// <param name="reflectionContext">Reflection context.</param>
        /// <param name="isCollectionValidator">Should be applied to collection items.</param>
        public RuleContext(
            OpenApiSchema schema,
            string propertyKey,
            IPropertyValidator propertyValidator,
            ReflectionContext? reflectionContext = null,
            bool isCollectionValidator = false)
        {
            Schema = schema;
            PropertyKey = propertyKey;
            PropertyValidator = propertyValidator;
            ReflectionContext = reflectionContext;
            IsCollectionValidator = isCollectionValidator;
        }
    }

    public class ReflectionContext
    {
        public Type? Type { get; }

        public MemberInfo? PropertyInfo { get; }

        public ParameterInfo? ParameterInfo { get; }

        public ReflectionContext(
            Type? type = null,
            MemberInfo? propertyInfo = null,
            ParameterInfo? parameterInfo = null)
        {
            Type = type;
            PropertyInfo = propertyInfo;
            ParameterInfo = parameterInfo;
        }
    }
}