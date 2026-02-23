// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using FluentValidation.Validators;
using MicroElements.OpenApi.FluentValidation;
using Microsoft.AspNetCore.OpenApi;
#if !OPENAPI_V2
using Microsoft.OpenApi.Models;
#endif

namespace MicroElements.AspNetCore.OpenApi.FluentValidation
{
    /// <summary>
    /// Schema generation context for Microsoft.AspNetCore.OpenApi.
    /// </summary>
    public record AspNetCoreSchemaGenerationContext : ISchemaGenerationContext<OpenApiSchema>
    {
        /// <inheritdoc/>
        public Type SchemaType { get; }

        /// <inheritdoc />
        IReadOnlyList<IFluentValidationRule> ISchemaGenerationContext.Rules => Rules;

        /// <inheritdoc/>
        public IReadOnlyList<IFluentValidationRule<OpenApiSchema>> Rules { get; }

        /// <inheritdoc/>
        public ISchemaGenerationOptions SchemaGenerationOptions { get; }

        /// <inheritdoc/>
        public OpenApiSchema Schema { get; }

        /// <inheritdoc/>
        public ISchemaProvider<OpenApiSchema> SchemaProvider { get; }

        /// <inheritdoc/>
        public IEnumerable<string> Properties => Schema.Properties?.Keys ?? Array.Empty<string>();

        /// <inheritdoc />
        public ISchemaGenerationContext<OpenApiSchema> With(OpenApiSchema schema)
        {
            return new AspNetCoreSchemaGenerationContext(
                schema: schema,
                schemaType: SchemaType,
                rules: Rules,
                schemaGenerationOptions: SchemaGenerationOptions,
                schemaProvider: SchemaProvider);
        }

        /// <inheritdoc />
        public IRuleContext<OpenApiSchema> Create(
            string schemaPropertyName,
            ValidationRuleContext validationRuleContext,
            IPropertyValidator propertyValidator)
        {
            return new OpenApiRuleContext(Schema, schemaPropertyName, validationRuleContext, propertyValidator);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AspNetCoreSchemaGenerationContext"/> class.
        /// </summary>
        /// <param name="schema">OpenApi schema.</param>
        /// <param name="schemaType">Schema .NET type.</param>
        /// <param name="rules">Validation rules.</param>
        /// <param name="schemaGenerationOptions">Schema generation options.</param>
        /// <param name="schemaProvider">Schema provider.</param>
        public AspNetCoreSchemaGenerationContext(
            OpenApiSchema schema,
            Type schemaType,
            IReadOnlyList<IFluentValidationRule<OpenApiSchema>> rules,
            ISchemaGenerationOptions schemaGenerationOptions,
            ISchemaProvider<OpenApiSchema>? schemaProvider = null)
        {
            Schema = schema;
            SchemaType = schemaType;
            Rules = rules;
            SchemaGenerationOptions = schemaGenerationOptions;
            SchemaProvider = schemaProvider ?? new AspNetCoreSchemaProvider();
        }
    }
}
