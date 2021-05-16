// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using MicroElements.Swashbuckle.FluentValidation.Generation;
using Microsoft.OpenApi.Models;

namespace MicroElements.Swashbuckle.FluentValidation
{
    /// <summary>
    /// Schema generation context.
    /// </summary>
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

        /// <summary>
        /// Gets all registered <see cref="FluentValidationRule"/>.
        /// </summary>
        public IReadOnlyList<FluentValidationRule> Rules { get; init; }

        /// <summary>
        /// Gets <see cref="ISchemaGenerationOptions"/> (constant user options).
        /// </summary>
        public ISchemaGenerationOptions SchemaGenerationOptions { get; init; }

        /// <summary>
        /// Gets <see cref="ISchemaGenerationSettings"/> (runtime options and services).
        /// </summary>
        public ISchemaGenerationSettings SchemaGenerationSettings { get; init; }

        /// <summary>
        /// Gets schema provider.
        /// </summary>
        public ISchemaProvider<OpenApiSchema> SchemaProvider { get; init; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SchemaGenerationContext"/> class.
        /// </summary>
        public SchemaGenerationContext(
            OpenApiSchema schema,
            Type schemaType,
            IReadOnlyList<FluentValidationRule> rules,
            ISchemaGenerationOptions schemaGenerationOptions,
            ISchemaGenerationSettings schemaGenerationSettings,
            ISchemaProvider<OpenApiSchema> schemaProvider)
        {
            Schema = schema;
            SchemaType = schemaType;
            Rules = rules;
            SchemaGenerationOptions = schemaGenerationOptions;
            SchemaGenerationSettings = schemaGenerationSettings;
            SchemaProvider = schemaProvider;
        }
    }
}