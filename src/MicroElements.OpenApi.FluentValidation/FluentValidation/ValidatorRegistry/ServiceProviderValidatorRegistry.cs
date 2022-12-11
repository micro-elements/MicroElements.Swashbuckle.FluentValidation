// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using FluentValidation;
using MicroElements.CodeContracts;
using MicroElements.Swashbuckle.FluentValidation;
using Microsoft.Extensions.Options;

namespace MicroElements.OpenApi.FluentValidation
{
    /// <summary>
    /// Validator registry that gets validators from <see cref="IServiceProvider"/>.
    /// </summary>
    public class ServiceProviderValidatorRegistry : IValidatorRegistry
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ISchemaGenerationOptions _options;

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceProviderValidatorRegistry"/> class.
        /// </summary>
        /// <param name="serviceProvider">The source service provider.</param>
        /// <param name="options">Schema generation options.</param>
        public ServiceProviderValidatorRegistry(
            IServiceProvider serviceProvider,
            IOptions<SchemaGenerationOptions>? options = null)
        {
            _serviceProvider = serviceProvider.AssertArgumentNotNull(nameof(serviceProvider));
            _options = options?.Value ?? new SchemaGenerationOptions();
        }

        /// <inheritdoc />
        public IValidator? GetValidator(Type type) => _serviceProvider.GetValidator(type);

        /// <inheritdoc />
        public IEnumerable<IValidator> GetValidators(Type type) => _serviceProvider.GetValidators(type, _options);
    }
}