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
using Swashbuckle.AspNetCore.SwaggerGen;

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

#if OPENAPI_V2
                var schemaProperty = OpenApiSchemaCompatibility.ResolveRefProperty(Schema, PropertyKey, _schemaRepository);
#else
                var schemaProperty = OpenApiSchemaCompatibility.GetProperty(Schema, PropertyKey);
#endif

                // Property is a schema reference (enum, nested class) - return empty schema to skip validation
                // Issue #176: https://github.com/micro-elements/MicroElements.Swashbuckle.FluentValidation/issues/176
                if (schemaProperty == null)
                {
                    return new OpenApiSchema();
                }

                var items = OpenApiSchemaCompatibility.GetItems(schemaProperty);
                return !ValidationRuleInfo.IsCollectionRule() ? schemaProperty : (items ?? schemaProperty);
            }
        }

        /// <summary>
        /// Gets <see cref="IValidationRule"/> with extended information.
        /// </summary>
        private ValidationRuleContext ValidationRuleInfo { get; }

        private readonly SchemaRepository? _schemaRepository;

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenApiRuleContext"/> class.
        /// </summary>
        /// <param name="schema">Swagger schema.</param>
        /// <param name="propertyKey">Property name.</param>
        /// <param name="validationRuleInfo">ValidationRuleInfo.</param>
        /// <param name="propertyValidator">Property validator.</param>
        /// <param name="schemaRepository">Optional schema repository for resolving references.</param>
        public OpenApiRuleContext(
            OpenApiSchema schema,
            string propertyKey,
            ValidationRuleContext validationRuleInfo,
            IPropertyValidator propertyValidator,
            SchemaRepository? schemaRepository = null)
        {
            Schema = schema;
            PropertyKey = propertyKey;
            ValidationRuleInfo = validationRuleInfo;
            PropertyValidator = propertyValidator;
            _schemaRepository = schemaRepository;
        }
    }
}
