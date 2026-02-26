// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.OpenApi;

namespace MicroElements.AspNetCore.OpenApi.FluentValidation
{
    /// <summary>
    /// Extensions for <see cref="OpenApiOptions"/>.
    /// </summary>
    public static class OpenApiOptionsExtensions
    {
        /// <summary>
        /// Adds FluentValidation schema transformer to the OpenAPI options.
        /// Call this after <c>AddFluentValidationRulesToOpenApi()</c> on the service collection.
        /// </summary>
        /// <param name="options">OpenApi options.</param>
        /// <returns>The same options instance for chaining.</returns>
        public static OpenApiOptions AddFluentValidationRules(this OpenApiOptions options)
        {
            options.AddSchemaTransformer<FluentValidationSchemaTransformer>();
            return options;
        }
    }
}
