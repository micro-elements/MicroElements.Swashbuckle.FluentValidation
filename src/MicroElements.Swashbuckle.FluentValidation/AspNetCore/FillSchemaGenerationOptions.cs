// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using MicroElements.OpenApi.FluentValidation;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MicroElements.Swashbuckle.FluentValidation.AspNetCore
{
    /// <summary>
    /// Fills <see cref="SchemaGenerationOptions"/> default values on PostConfigure action.
    /// </summary>
    public class FillSchemaGenerationOptions : IPostConfigureOptions<SchemaGenerationOptions>
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IOptions<SwaggerGenOptions>? _swaggerGenOptions;

        /// <summary>
        /// Initializes a new instance of the <see cref="FillSchemaGenerationOptions"/> class.
        /// </summary>
        /// <param name="serviceProvider">The source service provider.</param>
        /// <param name="swaggerGenOptions">Swashbuckle options.</param>
        public FillSchemaGenerationOptions(
            IServiceProvider serviceProvider,
            IOptions<SwaggerGenOptions>? swaggerGenOptions = null)
        {
            _serviceProvider = serviceProvider;
            _swaggerGenOptions = swaggerGenOptions;
        }

        /// <inheritdoc />
        public void PostConfigure(string name, SchemaGenerationOptions options)
        {
            // Fills options from SwashbuckleOptions
            options.FillFromSwashbuckleOptions(_swaggerGenOptions);

            // Assume that all needed is filled
            options.FillDefaultValues(_serviceProvider);
        }
    }
}