// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
#if OPENAPI_V2
using System.Collections.Generic;
#endif
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

#if OPENAPI_V2
        private readonly Dictionary<Type, string> _requestedSchemaIds = new Dictionary<Type, string>();

        /// <summary>
        /// Gets the types passed to <see cref="GetSchemaForType"/> mapped to their schema ids.
        /// The Issue #180 cleanup uses this map to heal Swashbuckle's internal reserved-id state via
        /// <c>SchemaRepository.ReplaceSchemaId</c> (Swashbuckle 10.1.0+) before removing a schema (Issue #226).
        /// Covers every call site: the parameters loop and the Issue #209 ancestor-requiredness walk.
        /// </summary>
        internal IReadOnlyDictionary<Type, string> RequestedSchemaIds => _requestedSchemaIds;
#endif

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
            _requestedSchemaIds[type] = schemaId;

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

            // Issue #223: when the same [FromQuery] DTO is shared by several endpoints, the per-operation
            // Issue #180 cleanup removes the container schema from SchemaRepository.Schemas, but Swashbuckle
            // still keeps the type in its internal reserved-ids map. For the 2nd+ endpoint, GenerateSchema then
            // returns a bare $ref (no Properties) and the schema is no longer in Schemas, so the rules would be
            // skipped. Recover the concrete schema by generating it into a throwaway repository — this is fully
            // isolated: it never touches the shared repository or its reserved-id state.
            if ((schema.Properties == null || schema.Properties.Count == 0) &&
                !_schemaRepository.Schemas.ContainsKey(schemaId))
            {
                var throwawayRepository = new SchemaRepository();
                _schemaGenerator.GenerateSchema(type, throwawayRepository);
                if (throwawayRepository.Schemas.TryGetValue(schemaId, out var concrete)
                    && concrete is OpenApiSchema concreteSchema
                    && concreteSchema.Properties is { Count: > 0 })
                {
                    schema = concreteSchema;
                }
            }

            return schema;
        }

        private string DefaultSchemaIdSelector(Type type)
        {
            return type.Name;
        }
    }
}