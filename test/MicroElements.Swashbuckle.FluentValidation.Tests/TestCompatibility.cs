// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
#if OPENAPI_V2
using Microsoft.OpenApi;
#else
using Microsoft.OpenApi.Models;
#endif
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MicroElements.Swashbuckle.FluentValidation.Tests;

/// <summary>
/// Compatibility helpers for OpenApi v1/v2 differences in tests.
/// </summary>
internal static class TestCompatibility
{
    /// <summary>
    /// Gets schema from SchemaRepository by key.
    /// </summary>
    public static OpenApiSchema GetSchema(this SchemaRepository repository, string key)
    {
#if OPENAPI_V2
        return repository.Schemas[key] as OpenApiSchema ?? throw new System.Exception($"Schema '{key}' not found or not OpenApiSchema");
#else
        return repository.Schemas[key];
#endif
    }

#if OPENAPI_V2
    /// <summary>
    /// Gets schema from SchemaRepository by reference.
    /// </summary>
    public static OpenApiSchema GetSchemaByRef(this SchemaRepository repository, IOpenApiSchema schema)
    {
        if (schema is OpenApiSchemaReference schemaRef)
        {
            var refId = schemaRef.Reference?.Id;
            if (refId != null && repository.Schemas.TryGetValue(refId, out var result))
                return result as OpenApiSchema ?? new OpenApiSchema();
        }
        return schema as OpenApiSchema ?? new OpenApiSchema();
    }
#endif

    /// <summary>
    /// Gets schema from SchemaRepository by reference.
    /// </summary>
    public static OpenApiSchema GetSchemaByRef(this SchemaRepository repository, OpenApiSchema schema)
    {
#if OPENAPI_V2
        // In OpenApi 2.x, we need to check if it's a reference
        return schema;
#else
        if (schema.Reference?.Id != null)
            return repository.Schemas[schema.Reference.Id];
        return schema;
#endif
    }

    /// <summary>
    /// Checks if schema type matches expected type.
    /// </summary>
    public static bool IsType(this OpenApiSchema schema, string expectedType)
    {
#if OPENAPI_V2
        if (!schema.Type.HasValue) return false;
        return expectedType switch
        {
            "string" => schema.Type.Value.HasFlag(JsonSchemaType.String),
            "integer" => schema.Type.Value.HasFlag(JsonSchemaType.Integer),
            "number" => schema.Type.Value.HasFlag(JsonSchemaType.Number),
            "boolean" => schema.Type.Value.HasFlag(JsonSchemaType.Boolean),
            "array" => schema.Type.Value.HasFlag(JsonSchemaType.Array),
            "object" => schema.Type.Value.HasFlag(JsonSchemaType.Object),
            _ => false
        };
#else
        return schema.Type == expectedType;
#endif
    }

    /// <summary>
    /// Gets the type as string for assertions.
    /// </summary>
    public static string? GetTypeString(this OpenApiSchema schema)
    {
#if OPENAPI_V2
        if (!schema.Type.HasValue) return null;
        var type = schema.Type.Value;
        if (type.HasFlag(JsonSchemaType.String)) return "string";
        if (type.HasFlag(JsonSchemaType.Integer)) return "integer";
        if (type.HasFlag(JsonSchemaType.Number)) return "number";
        if (type.HasFlag(JsonSchemaType.Boolean)) return "boolean";
        if (type.HasFlag(JsonSchemaType.Array)) return "array";
        if (type.HasFlag(JsonSchemaType.Object)) return "object";
        return null;
#else
        return schema.Type;
#endif
    }

    /// <summary>
    /// Checks if schema is nullable.
    /// </summary>
    public static bool IsNullable(this OpenApiSchema schema)
    {
#if OPENAPI_V2
        return schema.Type.HasValue && schema.Type.Value.HasFlag(JsonSchemaType.Null);
#else
        return schema.Nullable;
#endif
    }

    /// <summary>
    /// Gets property from schema.
    /// </summary>
    public static OpenApiSchema? GetProperty(this OpenApiSchema schema, string key)
    {
#if OPENAPI_V2
        if (schema.Properties?.TryGetValue(key, out var prop) == true)
            return prop as OpenApiSchema;
        return null;
#else
        if (schema.Properties?.TryGetValue(key, out var prop) == true)
            return prop;
        return null;
#endif
    }

    /// <summary>
    /// Generates schema and resolves reference.
    /// </summary>
    public static OpenApiSchema GenerateAndResolve<T>(this ISchemaGenerator generator, SchemaRepository repository)
    {
        var schema = generator.GenerateSchema(typeof(T), repository);
#if OPENAPI_V2
        if (schema is OpenApiSchemaReference schemaRef && schemaRef.Reference?.Id != null)
        {
            if (repository.Schemas.TryGetValue(schemaRef.Reference.Id, out var resolved))
                return resolved as OpenApiSchema ?? new OpenApiSchema();
        }
        return schema as OpenApiSchema ?? new OpenApiSchema();
#else
        if (schema.Reference?.Id != null)
            return repository.Schemas[schema.Reference.Id];
        return schema;
#endif
    }

#if OPENAPI_V2
    /// <summary>
    /// Resolves schema reference from already generated schema.
    /// </summary>
    public static OpenApiSchema GenerateAndResolve<T>(this SchemaRepository repository, IOpenApiSchema schema)
    {
        if (schema is OpenApiSchemaReference schemaRef && schemaRef.Reference?.Id != null)
        {
            if (repository.Schemas.TryGetValue(schemaRef.Reference.Id, out var resolved))
                return resolved as OpenApiSchema ?? new OpenApiSchema();
        }
        return schema as OpenApiSchema ?? new OpenApiSchema();
    }

    /// <summary>
    /// Gets the reference ID from a schema result.
    /// </summary>
    public static string? GetRefId(this IOpenApiSchema schema)
    {
        if (schema is OpenApiSchemaReference schemaRef)
            return schemaRef.Reference?.Id;
        return null;
    }
#else
    /// <summary>
    /// Resolves schema reference from already generated schema.
    /// </summary>
    public static OpenApiSchema GenerateAndResolve<T>(this SchemaRepository repository, OpenApiSchema schema)
    {
        if (schema.Reference?.Id != null)
            return repository.Schemas[schema.Reference.Id];
        return schema;
    }

    /// <summary>
    /// Gets the reference ID from a schema result.
    /// </summary>
    public static string? GetRefId(this OpenApiSchema schema)
    {
        return schema.Reference?.Id;
    }
#endif

    /// <summary>
    /// Gets the Items schema from an array schema.
    /// </summary>
    public static OpenApiSchema? GetItems(this OpenApiSchema schema)
    {
#if OPENAPI_V2
        return schema.Items as OpenApiSchema;
#else
        return schema.Items;
#endif
    }

    /// <summary>
    /// Gets Minimum as decimal. In OpenApi 2.x, when exclusive, the value is in ExclusiveMinimum.
    /// </summary>
    public static decimal? GetMinimum(this OpenApiSchema schema)
    {
#if OPENAPI_V2
        // In OpenApi 2.x with exclusive, value is in ExclusiveMinimum instead of Minimum
        var minValue = !string.IsNullOrEmpty(schema.Minimum) ? schema.Minimum :
                       !string.IsNullOrEmpty(schema.ExclusiveMinimum) ? schema.ExclusiveMinimum : null;
        return decimal.TryParse(minValue, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var result) ? result : null;
#else
        return schema.Minimum;
#endif
    }

    /// <summary>
    /// Gets Maximum as decimal. In OpenApi 2.x, when exclusive, the value is in ExclusiveMaximum.
    /// </summary>
    public static decimal? GetMaximum(this OpenApiSchema schema)
    {
#if OPENAPI_V2
        // In OpenApi 2.x with exclusive, value is in ExclusiveMaximum instead of Maximum
        var maxValue = !string.IsNullOrEmpty(schema.Maximum) ? schema.Maximum :
                       !string.IsNullOrEmpty(schema.ExclusiveMaximum) ? schema.ExclusiveMaximum : null;
        return decimal.TryParse(maxValue, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var result) ? result : null;
#else
        return schema.Maximum;
#endif
    }

    /// <summary>
    /// Gets ExclusiveMinimum.
    /// </summary>
    public static bool? GetExclusiveMinimum(this OpenApiSchema schema)
    {
#if OPENAPI_V2
        // In OpenApi 2.x, ExclusiveMinimum is a string value representing the exclusive minimum
        // If ExclusiveMinimum has a value, it means exclusive is true
        return string.IsNullOrEmpty(schema.ExclusiveMinimum) ? null : true;
#else
        return schema.ExclusiveMinimum;
#endif
    }

    /// <summary>
    /// Gets ExclusiveMaximum.
    /// </summary>
    public static bool? GetExclusiveMaximum(this OpenApiSchema schema)
    {
#if OPENAPI_V2
        // In OpenApi 2.x, ExclusiveMaximum is a string value representing the exclusive maximum
        // If ExclusiveMaximum has a value, it means exclusive is true
        return string.IsNullOrEmpty(schema.ExclusiveMaximum) ? null : true;
#else
        return schema.ExclusiveMaximum;
#endif
    }

    /// <summary>
    /// Gets AllOf as a list of OpenApiSchema.
    /// </summary>
    public static IList<OpenApiSchema> GetAllOf(this OpenApiSchema schema)
    {
#if OPENAPI_V2
        return schema.AllOf?.OfType<OpenApiSchema>().ToList() ?? new List<OpenApiSchema>();
#else
        return schema.AllOf ?? new List<OpenApiSchema>();
#endif
    }
}
