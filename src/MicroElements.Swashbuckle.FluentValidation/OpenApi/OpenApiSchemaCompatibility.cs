// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
#if OPENAPI_V2
using Microsoft.OpenApi;
#else
using Microsoft.OpenApi.Models;
#endif

namespace MicroElements.OpenApi
{
    /// <summary>
    /// Compatibility layer for Microsoft.OpenApi 1.x and 2.x differences.
    /// </summary>
    internal static class OpenApiSchemaCompatibility
    {
        /// <summary>
        /// Checks if schema type is string.
        /// </summary>
        public static bool IsStringType(OpenApiSchema schema)
        {
#if OPENAPI_V2
            return schema.Type.HasValue && schema.Type.Value.HasFlag(JsonSchemaType.String);
#else
            return schema.Type == "string";
#endif
        }

        /// <summary>
        /// Checks if schema type is array.
        /// </summary>
        public static bool IsArrayType(OpenApiSchema schema)
        {
#if OPENAPI_V2
            return schema.Type.HasValue && schema.Type.Value.HasFlag(JsonSchemaType.Array);
#else
            return schema.Type == "array";
#endif
        }

        /// <summary>
        /// Sets schema as not nullable.
        /// </summary>
        public static void SetNotNullable(OpenApiSchema schema)
        {
#if OPENAPI_V2
            if (schema.Type.HasValue)
            {
                schema.Type &= ~JsonSchemaType.Null;
            }
#else
            schema.Nullable = false;
#endif
        }

        /// <summary>
        /// Gets nullable value from schema.
        /// </summary>
        public static bool GetNullable(OpenApiSchema schema)
        {
#if OPENAPI_V2
            return schema.Type.HasValue && schema.Type.Value.HasFlag(JsonSchemaType.Null);
#else
            return schema.Nullable;
#endif
        }

#if OPENAPI_V2
        /// <summary>
        /// Gets nullable value from IOpenApiSchema interface.
        /// </summary>
        public static bool GetNullable(IOpenApiSchema schema)
        {
            if (schema is OpenApiSchema openApiSchema)
                return GetNullable(openApiSchema);
            return false;
        }
#endif

        /// <summary>
        /// Sets nullable value on schema.
        /// </summary>
        public static void SetNullable(OpenApiSchema schema, bool value)
        {
#if OPENAPI_V2
            if (value)
            {
                if (schema.Type.HasValue)
                    schema.Type |= JsonSchemaType.Null;
            }
            else
            {
                SetNotNullable(schema);
            }
#else
            schema.Nullable = value;
#endif
        }

        /// <summary>
        /// Ensures Required collection is initialized.
        /// </summary>
        public static void EnsureRequiredInitialized(OpenApiSchema schema)
        {
#if OPENAPI_V2
            schema.Required ??= new HashSet<string>();
#endif
            // In v1, Required is always initialized
        }

        /// <summary>
        /// Adds property to Required collection safely.
        /// </summary>
        public static void AddRequired(OpenApiSchema schema, string propertyName)
        {
            EnsureRequiredInitialized(schema);
            if (!schema.Required.Contains(propertyName))
            {
                schema.Required.Add(propertyName);
            }
        }

        /// <summary>
        /// Checks if Required collection contains property.
        /// </summary>
        public static bool RequiredContains(OpenApiSchema schema, string propertyName)
        {
#if OPENAPI_V2
            return schema.Required?.Contains(propertyName) ?? false;
#else
            return schema.Required.Contains(propertyName);
#endif
        }

        /// <summary>
        /// Ensures AllOf collection is initialized.
        /// </summary>
        public static void EnsureAllOfInitialized(OpenApiSchema schema)
        {
#if OPENAPI_V2
            schema.AllOf ??= new List<IOpenApiSchema>();
#endif
        }

        /// <summary>
        /// Gets count of AllOf items matching predicate.
        /// </summary>
        public static int AllOfCountWhere(OpenApiSchema schema, Func<OpenApiSchema, bool> predicate)
        {
#if OPENAPI_V2
            return schema.AllOf?.OfType<OpenApiSchema>().Count(predicate) ?? 0;
#else
            return schema.AllOf.Count(predicate);
#endif
        }

        /// <summary>
        /// Adds schema to AllOf collection safely.
        /// </summary>
        public static void AddAllOf(OpenApiSchema schema, OpenApiSchema child)
        {
            EnsureAllOfInitialized(schema);
            schema.AllOf.Add(child);
        }

        /// <summary>
        /// Checks if Properties dictionary contains key.
        /// </summary>
        public static bool PropertiesContainsKey(OpenApiSchema schema, string key)
        {
            return schema.Properties?.ContainsKey(key) ?? false;
        }

        /// <summary>
        /// Gets Properties count safely.
        /// </summary>
        public static int PropertiesCount(OpenApiSchema schema)
        {
            return schema.Properties?.Count ?? 0;
        }

        /// <summary>
        /// Gets property from schema by key.
        /// </summary>
        public static OpenApiSchema? GetProperty(OpenApiSchema schema, string key)
        {
#if OPENAPI_V2
            if (schema.Properties?.TryGetValue(key, out var property) == true)
                return property as OpenApiSchema;
            return null;
#else
            if (schema.Properties?.TryGetValue(key, out var property) == true)
                return property;
            return null;
#endif
        }

        /// <summary>
        /// Tries to get property from schema by key.
        /// </summary>
        public static bool TryGetProperty(OpenApiSchema schema, string key, out OpenApiSchema? property)
        {
#if OPENAPI_V2
            if (schema.Properties?.TryGetValue(key, out var prop) == true)
            {
                property = prop as OpenApiSchema;
                return property != null;
            }
            property = null;
            return false;
#else
            if (schema.Properties?.TryGetValue(key, out var prop) == true)
            {
                property = prop;
                return true;
            }
            property = null;
            return false;
#endif
        }

        /// <summary>
        /// Sets ExclusiveMinimum on schema.
        /// </summary>
        public static void SetExclusiveMinimum(OpenApiSchema schema, bool value)
        {
#if OPENAPI_V2
            // In OpenAPI 3.1 / OpenApi 2.x, exclusiveMinimum is a number (string), not a boolean
            // When exclusive, the minimum value is stored in exclusiveMinimum instead
            if (value && !string.IsNullOrEmpty(schema.Minimum))
            {
                schema.ExclusiveMinimum = schema.Minimum;
                schema.Minimum = null;
            }
#else
            schema.ExclusiveMinimum = value;
#endif
        }

        /// <summary>
        /// Sets ExclusiveMaximum on schema.
        /// </summary>
        public static void SetExclusiveMaximum(OpenApiSchema schema, bool value)
        {
#if OPENAPI_V2
            // In OpenAPI 3.1 / OpenApi 2.x, exclusiveMaximum is a number (string), not a boolean
            if (value && !string.IsNullOrEmpty(schema.Maximum))
            {
                schema.ExclusiveMaximum = schema.Maximum;
                schema.Maximum = null;
            }
#else
            schema.ExclusiveMaximum = value;
#endif
        }

        /// <summary>
        /// Sets Minimum on schema.
        /// </summary>
        public static void SetMinimum(OpenApiSchema schema, decimal value)
        {
#if OPENAPI_V2
            schema.Minimum = value.ToString(CultureInfo.InvariantCulture);
#else
            schema.Minimum = value;
#endif
        }

        /// <summary>
        /// Sets Maximum on schema.
        /// </summary>
        public static void SetMaximum(OpenApiSchema schema, decimal value)
        {
#if OPENAPI_V2
            schema.Maximum = value.ToString(CultureInfo.InvariantCulture);
#else
            schema.Maximum = value;
#endif
        }

        /// <summary>
        /// Gets Minimum from schema.
        /// </summary>
        public static decimal? GetMinimum(OpenApiSchema schema)
        {
#if OPENAPI_V2
            if (decimal.TryParse(schema.Minimum, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
                return result;
            return null;
#else
            return schema.Minimum;
#endif
        }

        /// <summary>
        /// Gets Maximum from schema.
        /// </summary>
        public static decimal? GetMaximum(OpenApiSchema schema)
        {
#if OPENAPI_V2
            if (decimal.TryParse(schema.Maximum, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
                return result;
            return null;
#else
            return schema.Maximum;
#endif
        }

        /// <summary>
        /// Sets Minimum with comparison logic (takes the larger of existing and new value).
        /// </summary>
        public static void SetNewMinimum(OpenApiSchema schema, decimal newValue)
        {
            var current = GetMinimum(schema);
            var finalValue = current.HasValue ? Math.Max(current.Value, newValue) : newValue;
            SetMinimum(schema, finalValue);
        }

        /// <summary>
        /// Sets Maximum with comparison logic (takes the smaller of existing and new value).
        /// </summary>
        public static void SetNewMaximum(OpenApiSchema schema, decimal newValue)
        {
            var current = GetMaximum(schema);
            var finalValue = current.HasValue ? Math.Min(current.Value, newValue) : newValue;
            SetMaximum(schema, finalValue);
        }

        /// <summary>
        /// Copies ExclusiveMinimum from source to target schema.
        /// </summary>
        public static void CopyExclusiveMinimum(OpenApiSchema target, OpenApiSchema source)
        {
            target.ExclusiveMinimum = source.ExclusiveMinimum;
        }

        /// <summary>
        /// Copies ExclusiveMaximum from source to target schema.
        /// </summary>
        public static void CopyExclusiveMaximum(OpenApiSchema target, OpenApiSchema source)
        {
            target.ExclusiveMaximum = source.ExclusiveMaximum;
        }

        /// <summary>
        /// Copies Minimum from source to target schema.
        /// </summary>
        public static void CopyMinimum(OpenApiSchema target, OpenApiSchema source)
        {
            target.Minimum = source.Minimum;
        }

        /// <summary>
        /// Copies Maximum from source to target schema.
        /// </summary>
        public static void CopyMaximum(OpenApiSchema target, OpenApiSchema source)
        {
            target.Maximum = source.Maximum;
        }

#if OPENAPI_V2
        /// <summary>
        /// Copies Minimum from IOpenApiSchema source to target schema.
        /// </summary>
        public static void CopyMinimum(OpenApiSchema target, IOpenApiSchema source)
        {
            target.Minimum = source.Minimum;
        }

        /// <summary>
        /// Copies Maximum from IOpenApiSchema source to target schema.
        /// </summary>
        public static void CopyMaximum(OpenApiSchema target, IOpenApiSchema source)
        {
            target.Maximum = source.Maximum;
        }

        /// <summary>
        /// Copies ExclusiveMinimum from IOpenApiSchema source to target schema.
        /// </summary>
        public static void CopyExclusiveMinimum(OpenApiSchema target, IOpenApiSchema source)
        {
            target.ExclusiveMinimum = source.ExclusiveMinimum;
        }

        /// <summary>
        /// Copies ExclusiveMaximum from IOpenApiSchema source to target schema.
        /// </summary>
        public static void CopyExclusiveMaximum(OpenApiSchema target, IOpenApiSchema source)
        {
            target.ExclusiveMaximum = source.ExclusiveMaximum;
        }
#endif

        /// <summary>
        /// Gets Items property from schema.
        /// </summary>
        public static OpenApiSchema? GetItems(OpenApiSchema schema)
        {
#if OPENAPI_V2
            return schema.Items as OpenApiSchema;
#else
            return schema.Items;
#endif
        }

        /// <summary>
        /// Gets AllOf collection as OpenApiSchema enumerable.
        /// </summary>
        public static IEnumerable<OpenApiSchema> GetAllOf(OpenApiSchema schema)
        {
#if OPENAPI_V2
            return schema.AllOf?.OfType<OpenApiSchema>() ?? Enumerable.Empty<OpenApiSchema>();
#else
            return schema.AllOf ?? Enumerable.Empty<OpenApiSchema>();
#endif
        }

        /// <summary>
        /// Gets OneOf collection as OpenApiSchema enumerable.
        /// </summary>
        public static IEnumerable<OpenApiSchema> GetOneOf(OpenApiSchema schema)
        {
#if OPENAPI_V2
            return schema.OneOf?.OfType<OpenApiSchema>() ?? Enumerable.Empty<OpenApiSchema>();
#else
            return schema.OneOf ?? Enumerable.Empty<OpenApiSchema>();
#endif
        }

        /// <summary>
        /// Gets AnyOf collection as OpenApiSchema enumerable.
        /// </summary>
        public static IEnumerable<OpenApiSchema> GetAnyOf(OpenApiSchema schema)
        {
#if OPENAPI_V2
            return schema.AnyOf?.OfType<OpenApiSchema>() ?? Enumerable.Empty<OpenApiSchema>();
#else
            return schema.AnyOf ?? Enumerable.Empty<OpenApiSchema>();
#endif
        }

        /// <summary>
        /// Gets Properties dictionary values as OpenApiSchema enumerable.
        /// </summary>
        public static IEnumerable<KeyValuePair<string, OpenApiSchema>> GetProperties(OpenApiSchema schema)
        {
#if OPENAPI_V2
            if (schema.Properties == null)
                return Enumerable.Empty<KeyValuePair<string, OpenApiSchema>>();
            return schema.Properties
                .Select(kvp => new KeyValuePair<string, OpenApiSchema>(kvp.Key, (OpenApiSchema)kvp.Value));
#else
            return schema.Properties ?? Enumerable.Empty<KeyValuePair<string, OpenApiSchema>>();
#endif
        }

#if OPENAPI_V2
        /// <summary>
        /// Copies all validation-related properties from source to target.
        /// </summary>
        public static void CopyValidationProperties(OpenApiSchema target, IOpenApiSchema source)
        {
            target.MinLength = source.MinLength;
            target.MaxLength = source.MaxLength;
            target.Pattern = source.Pattern;
            target.Minimum = source.Minimum;
            target.Maximum = source.Maximum;
            target.ExclusiveMinimum = source.ExclusiveMinimum;
            target.ExclusiveMaximum = source.ExclusiveMaximum;
            // Can't copy Enum and AllOf directly due to interface limitations
            // These need special handling if needed
        }
#endif
    }

}
