// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;
using MicroElements.OpenApi.FluentValidation;
using MicroElements.Swashbuckle.FluentValidation.Generation;
using Microsoft.Extensions.DependencyInjection;

namespace MicroElements.Swashbuckle.FluentValidation.AspNetCore
{
    /// <summary>
    /// Registration customization.
    /// </summary>
    public class RegistrationOptions
    {
        /// <summary>
        /// Register fluent validation rules generators to swagger.
        /// Default: true.
        /// </summary>
        public bool RegisterFluentValidationRules { get; set; } = true;

        /// <summary>
        /// Register <see cref="AspNetJsonSerializerOptions"/> and <see cref="JsonSerializerOptions"/> as reference to Microsoft.AspNetCore.Mvc.JsonOptions.Value.
        /// Default: true.
        /// </summary>
        public bool RegisterJsonSerializerOptions { get; set; } = true;

        /// <summary>
        /// Register <see cref="SystemTextJsonNameResolver"/> as default <see cref="INameResolver"/>.
        /// Default: true.
        /// </summary>
        public bool RegisterSystemTextJsonNameResolver { get; set; } = true;

        /// <summary>
        /// ServiceLifetime to use for service registration.
        /// </summary>
        public ServiceLifetime ServiceLifetime { get; set; } = ServiceLifetime.Scoped;

        /// <summary>
        /// Use DocumentFilter instead of SchemaFilter.
        /// </summary>
        public bool ExperimentalUseDocumentFilter { get; set; } = false;
    }
}