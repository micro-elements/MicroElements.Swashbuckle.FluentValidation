// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using FluentValidation;
using FluentValidation.Validators;
using MicroElements.OpenApi;
using MicroElements.OpenApi.FluentValidation;
#if !OPENAPI_V2
using Microsoft.OpenApi.Models;
#endif

namespace MicroElements.Swashbuckle.FluentValidation
{
    /// <summary>
    /// RuleContext.
    /// </summary>
    public class OpenApiRuleContext : IRuleContext<OpenApiSchema>
    {
        /// <summary>
        /// Gets property name in schema.
        /// </summary>
        public string PropertyKey { get; }

        /// <summary>
        /// Gets property validator for property in schema.
        /// </summary>
        public IPropertyValidator PropertyValidator { get; }

        /// <summary>
        /// Gets OpenApi (swagger) schema.
        /// </summary>
        public OpenApiSchema Schema { get; }

        /// <summary>
        /// Gets target property schema.
        /// </summary>
        public OpenApiSchema Property
        {
            get
            {
                if (!OpenApiSchemaCompatibility.PropertiesContainsKey(Schema, PropertyKey))
                {
                    Type? schemaType = ValidationRuleInfo.GetReflectionContext()?.Type;
                    throw new ApplicationException($"Schema for type '{schemaType}' does not contain property '{PropertyKey}'.\nRegister {typeof(INameResolver)} if name in type differs from name in json.");
                }

                var schemaProperty = OpenApiSchemaCompatibility.GetProperty(Schema, PropertyKey)!;
                var items = OpenApiSchemaCompatibility.GetItems(schemaProperty);
                return !ValidationRuleInfo.IsCollectionRule() ? schemaProperty : items!;
            }
        }

        /// <summary>
        /// Gets <see cref="IValidationRule"/> with extended information.
        /// </summary>
        private ValidationRuleContext ValidationRuleInfo { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenApiRuleContext"/> class.
        /// </summary>
        /// <param name="schema">Swagger schema.</param>
        /// <param name="propertyKey">Property name.</param>
        /// <param name="validationRuleInfo">ValidationRuleInfo.</param>
        /// <param name="propertyValidator">Property validator.</param>
        public OpenApiRuleContext(
            OpenApiSchema schema,
            string propertyKey,
            ValidationRuleContext validationRuleInfo,
            IPropertyValidator propertyValidator)
        {
            Schema = schema;
            PropertyKey = propertyKey;
            ValidationRuleInfo = validationRuleInfo;
            PropertyValidator = propertyValidator;
        }
    }
}