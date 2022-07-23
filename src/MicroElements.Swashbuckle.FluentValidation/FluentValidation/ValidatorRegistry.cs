using System;
using System.Collections.Generic;
using System.Linq;
using FluentValidation;

namespace MicroElements.Swashbuckle.FluentValidation
{
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
}