// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using FluentValidation.Validators;
using MicroElements.OpenApi.FluentValidation;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MicroElements.Swashbuckle.FluentValidation
{
    /// <summary>
    /// Schema generation context.
    /// </summary>
    public record SchemaGenerationContext : ISchemaGenerationContext<OpenApiSchema>
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
        public OpenApiSchema Schema { get; init; }

        /// <inheritdoc/>
        public ISchemaProvider<OpenApiSchema> SchemaProvider { get; }

        public IEnumerable<string> Properties => Schema.Properties?.Keys ?? Array.Empty<string>();

        public ISchemaGenerator SchemaGenerator { get; }

        public SchemaRepository SchemaRepository { get; }

        /// <inheritdoc />
        public ISchemaGenerationContext<OpenApiSchema> With(OpenApiSchema schema)
        {
            return new SchemaGenerationContext(
                schemaRepository: SchemaRepository,
                schemaGenerator: SchemaGenerator,
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
        /// Initializes a new instance of the <see cref="SchemaGenerationContext"/> class.
        /// </summary>
        public SchemaGenerationContext(
            SchemaRepository schemaRepository,
            ISchemaGenerator schemaGenerator,

            OpenApiSchema schema,
            Type schemaType,

            IReadOnlyList<IFluentValidationRule<OpenApiSchema>> rules,
            ISchemaGenerationOptions schemaGenerationOptions,
            ISchemaProvider<OpenApiSchema>? schemaProvider = null)
        {
            SchemaRepository = schemaRepository;
            SchemaGenerator = schemaGenerator;

            Schema = schema;
            SchemaType = schemaType;
            Rules = rules;
            SchemaGenerationOptions = schemaGenerationOptions;

            schemaProvider ??= new SwashbuckleSchemaProvider(
                schemaRepository,
                schemaGenerator,
                schemaGenerationOptions.SchemaIdSelector);
            SchemaProvider = schemaProvider;
        }
    }
}