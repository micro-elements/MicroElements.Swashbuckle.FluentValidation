// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
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
        /// Gets or sets a ServiceLifetime to use for services registration.
        /// </summary>
        public ServiceLifetime ServiceLifetime { get; set; } = ServiceLifetime.Scoped;

        /// <summary>
        /// Use the document-filter pipeline (<see cref="FluentValidationDocumentFilter"/>) instead of the default
        /// schema + operation filters. The document filter processes the whole document at once and performs the
        /// schema cleanup once at the end, so per-operation shared-DTO state issues (Issue #223/#226) cannot occur.
        /// Default: false (the schema + operation filter pipeline remains the default).
        /// </summary>
        public bool UseDocumentFilter { get; set; } = false;

        /// <summary>
        /// Obsolete alias for <see cref="UseDocumentFilter"/>.
        /// </summary>
        [Obsolete("Use " + nameof(UseDocumentFilter) + ". This property forwards to it and will be removed in a future major version.")]
        public bool ExperimentalUseDocumentFilter
        {
            get => UseDocumentFilter;
            set => UseDocumentFilter = value;
        }
    }
}