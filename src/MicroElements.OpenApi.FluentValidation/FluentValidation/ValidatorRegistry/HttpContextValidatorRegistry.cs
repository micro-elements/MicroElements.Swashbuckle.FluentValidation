// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using FluentValidation;
using MicroElements.Swashbuckle.FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace MicroElements.OpenApi.FluentValidation
{
    /// <summary>
    /// Allows for the creation of validators that have dependencies on scoped services.
    /// Add <c>services.AddHttpContextAccessor();</c> to use <see cref="IHttpContextAccessor"/>.
    /// </summary>
    public class HttpContextValidatorRegistry : IValidatorRegistry
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ISchemaGenerationOptions _options;

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpContextValidatorRegistry"/> class.
        /// </summary>
        /// <param name="httpContextAccessor">Access to the current HttpContext.</param>
        /// <param name="options">Schema generation options.</param>
        public HttpContextValidatorRegistry(
            IHttpContextAccessor httpContextAccessor,
            IOptions<SchemaGenerationOptions> options)
        {
            _httpContextAccessor = httpContextAccessor;
            _options = options.Value;
        }

        private IServiceProvider ServiceProvider => _httpContextAccessor.HttpContext.RequestServices;

        /// <inheritdoc />
        public IValidator? GetValidator(Type type) => ServiceProvider.GetValidator(type);

        /// <inheritdoc />
        public IEnumerable<IValidator> GetValidators(Type type) => ServiceProvider.GetValidators(type, _options);
    }
}
