// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using MicroElements.OpenApi.FluentValidation;
#if !OPENAPI_V2
using Microsoft.OpenApi.Models;
#endif
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MicroElements.Swashbuckle.FluentValidation
{
    /// <summary>
    /// SchemaProvider implementation for Swashbuckle.
    /// </summary>
    public class SwashbuckleSchemaProvider : ISchemaProvider<OpenApiSchema>
    {
        private readonly SchemaRepository _schemaRepository;
        private readonly ISchemaGenerator _schemaGenerator;
        private readonly Func<Type, string> _schemaIdSelector;

        /// <summary>
        /// Initializes a new instance of the <see cref="SwashbuckleSchemaProvider"/> class.
        /// </summary>
        /// <param name="schemaRepository">Swashbuckle schema repository.</param>
        /// <param name="schemaGenerator">Swashbuckle schema generator.</param>
        /// <param name="schemaIdSelector">Swashbuckle schemaId selector.</param>
        public SwashbuckleSchemaProvider(
            SchemaRepository schemaRepository,
            ISchemaGenerator schemaGenerator,
            Func<Type, string>? schemaIdSelector = null)
        {
            _schemaRepository = schemaRepository;
            _schemaGenerator = schemaGenerator;
            _schemaIdSelector = schemaIdSelector ?? DefaultSchemaIdSelector;
        }

        /// <inheritdoc />
        public OpenApiSchema GetSchemaForType(Type type)
        {
            var schemaId = _schemaIdSelector(type);

#if OPENAPI_V2
            if (!_schemaRepository.Schemas.TryGetValue(schemaId, out var schemaInterface))
            {
                schemaInterface = _schemaGenerator.GenerateSchema(type, _schemaRepository);
            }

            var schema = schemaInterface as OpenApiSchema ?? new OpenApiSchema();

            if ((schema.Properties == null || schema.Properties.Count == 0) &&
                _schemaRepository.Schemas.ContainsKey(schemaId))
            {
                schema = _schemaRepository.Schemas[schemaId] as OpenApiSchema ?? schema;
            }
#else
            if (!_schemaRepository.Schemas.TryGetValue(schemaId, out OpenApiSchema schema))
            {
                schema = _schemaGenerator.GenerateSchema(type, _schemaRepository);
            }

            if ((schema.Properties == null || schema.Properties.Count == 0) &&
                _schemaRepository.Schemas.ContainsKey(schemaId))
            {
                schema = _schemaRepository.Schemas[schemaId];
            }
#endif

            return schema;
        }

        private string DefaultSchemaIdSelector(Type type)
        {
            return type.Name;
        }
    }
}