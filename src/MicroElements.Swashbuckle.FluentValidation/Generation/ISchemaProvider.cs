using System;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MicroElements.Swashbuckle.FluentValidation.Generation
{
    public interface ISchemaProvider<TSchema>
    {
        TSchema GetSchemaForType(Type type);
    }

    public class SwashbuckleSchemaProvider : ISchemaProvider<OpenApiSchema>
    {
        private readonly SchemaRepository _schemaRepository;
        private readonly ISchemaGenerator _schemaGenerator;
        private readonly Func<Type, string> _schemaIdSelector;

        public SwashbuckleSchemaProvider(
            SchemaRepository schemaRepository,
            ISchemaGenerator schemaGenerator,
            Func<Type, string>? schemaIdSelector = null)
        {
            _schemaRepository = schemaRepository;
            _schemaGenerator = schemaGenerator;
            _schemaIdSelector = schemaIdSelector ?? DefaultSchemaIdSelector;
        }

        private string DefaultSchemaIdSelector(Type type)
        {
            return type.Name;
        }

        /// <inheritdoc />
        public OpenApiSchema GetSchemaForType(Type type)
        {
            var schemaId = _schemaIdSelector(type);

            if (!_schemaRepository.Schemas.TryGetValue(schemaId, out OpenApiSchema schema))
            {
                schema = _schemaGenerator.GenerateSchema(type, _schemaRepository);
            }

            if ((schema.Properties == null || schema.Properties.Count == 0) &&
                _schemaRepository.Schemas.ContainsKey(schemaId))
            {
                schema = _schemaRepository.Schemas[schemaId];
            }

            return schema;
        }
    }
}