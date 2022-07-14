using System;
using System.Diagnostics;
using FluentValidation;
using MicroElements.Swashbuckle.FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace SampleWebApi.ValidatorFactories
{
    /// <summary>
    /// <see cref="IValidatorRegistry"/> like default <see cref="FluentValidation.AspNetCore.ServiceProviderValidatorFactory"/>.
    /// The main difference that this factory tries to get <see cref="IValidator"/> on new scope if first try fails.
    /// </summary>
    public class ScopedServiceProviderValidatorFactory : ValidatorFactoryBase
    {
        private readonly IServiceProvider _serviceProvider;

        /// <summary>
        /// Creates new instance of <see cref="ScopedServiceProviderValidatorFactory"/>.
        /// </summary>
        /// <param name="serviceProvider"><see cref="IServiceProvider"/>.</param>
        public ScopedServiceProviderValidatorFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        /// <inheritdoc />
        [DebuggerStepThrough]
        public override IValidator CreateInstance(Type validatorType)
        {
            try
            {
                return _serviceProvider.GetService(validatorType) as IValidator;
            }
            catch (InvalidOperationException)
            {
                using (_serviceProvider.CreateScope())
                    return _serviceProvider.CreateScope().ServiceProvider.GetService(validatorType) as IValidator;
            }
        }
    }
}