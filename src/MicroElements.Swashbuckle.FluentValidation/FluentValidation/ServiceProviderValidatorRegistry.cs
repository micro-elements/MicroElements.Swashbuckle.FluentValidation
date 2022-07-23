using System;
using FluentValidation;

namespace MicroElements.Swashbuckle.FluentValidation
{
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