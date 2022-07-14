// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using FluentValidation;

namespace MicroElements.Swashbuckle.FluentValidation
{
    /// <summary>
    /// Gets validators for a particular type.
    /// </summary>
    public interface IValidatorRegistry
    {
        /// <summary>
        /// Gets the validator for the specified type.
        /// </summary>
        /// <param name="type">Type to validate.</param>
        /// <returns>Validator or <see langword="null"/>.</returns>
        IValidator? GetValidator(Type type);
    }

    /// <summary>
    /// <see cref="IValidatorRegistry"/> that works with registered validators.
    /// </summary>
    public class ValidatorRegistry : IValidatorRegistry
    {
        private readonly List<IValidator> _validators;

        public ValidatorRegistry(IEnumerable<IValidator> validators)
        {
            _validators = validators.ToList();
        }

        /// <inheritdoc />
        public IValidator? GetValidator(Type type)
        {
            return _validators.FirstOrDefault(validator => validator.CanValidateInstancesOfType(type));
        }
    }

    /// <summary>
    /// Validator registry that gets validators from <see cref="IServiceProvider"/>.
    /// </summary>
    public class ServiceProviderValidatorRegistry : IValidatorRegistry
    {
        private readonly IServiceProvider _serviceProvider;

        public ServiceProviderValidatorRegistry(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        /// <inheritdoc />
        public IValidator? GetValidator(Type type)
        {
            Type genericType = typeof(IValidator<>).MakeGenericType(type);
            return _serviceProvider.GetService(genericType) as IValidator;
        }
    }
}