// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using MicroElements.Swashbuckle.FluentValidation.AspNetCore;

namespace MicroElements.Swashbuckle.FluentValidation.Generation
{
    /// <summary>
    /// Resolves name according System.Text.Json <see cref="JsonPropertyNameAttribute"/> or <see cref="JsonSerializerOptions.PropertyNamingPolicy"/>.
    /// </summary>
    public class SystemTextJsonNameResolver : INameResolver
    {
        private readonly JsonSerializerOptions? _serializerOptions;

        /// <summary>
        /// Initializes a new instance of the <see cref="SystemTextJsonNameResolver"/> class.
        /// </summary>
        /// <param name="serializerOptions"><see cref="JsonSerializerOptions"/>.</param>
        public SystemTextJsonNameResolver(AspNetJsonSerializerOptions? serializerOptions = null)
        {
            _serializerOptions = serializerOptions?.Value ?? new JsonSerializerOptions();
        }

        /// <inheritdoc />
        public string GetPropertyName(PropertyInfo propertyInfo)
        {
            if (propertyInfo.GetCustomAttribute<JsonPropertyNameAttribute>() is { Name: { } jsonPropertyName })
            {
                return jsonPropertyName;
            }

            if (_serializerOptions?.PropertyNamingPolicy is { } jsonNamingPolicy)
            {
                return jsonNamingPolicy.ConvertName(propertyInfo.Name);
            }

            return propertyInfo.Name;
        }
    }
}