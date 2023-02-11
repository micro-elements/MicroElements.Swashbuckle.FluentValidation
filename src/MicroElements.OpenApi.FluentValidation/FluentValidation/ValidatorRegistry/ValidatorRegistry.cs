// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace MicroElements.OpenApi.FluentValidation
{
    /// <summary>
    /// <see cref="IValidatorRegistry"/> that works with registered validators.
    /// </summary>
    public class ValidatorRegistry : IValidatorRegistry
    {
        private readonly ISchemaGenerationOptions _options;
        private readonly List<IValidator> _validators;

        public ValidatorRegistry(
            IEnumerable<IValidator> validators,
            IOptions<SchemaGenerationOptions>? options = null)
        {
            _validators = validators.ToList();
            _options = options?.Value ?? new SchemaGenerationOptions();
        }

        /// <inheritdoc />
        public IValidator? GetValidator(Type type)
        {
            return GetValidators(type).FirstOrDefault();
        }

        /// <inheritdoc />
        public IEnumerable<IValidator> GetValidators(Type type)
        {
            return _validators.GetValidators(type, _options);
        }
    }
}