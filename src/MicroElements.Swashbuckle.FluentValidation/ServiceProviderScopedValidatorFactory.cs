using System;
using FluentValidation;
using Microsoft.AspNetCore.Http;

namespace MicroElements.Swashbuckle.FluentValidation
{
    /// <summary>
    /// Allows for the creation of validators that have dependencies
    /// on scoped services
    /// </summary>
    public class ServiceProviderScopedValidatorFactory : ValidatorFactoryBase
    {
        private readonly HttpContext HttpContext;

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceProviderScopedValidatorFactory"/> class.
        /// </summary>
        /// <param name="httpContextAccessor">Access to the current HttpContext</param>
        public ServiceProviderScopedValidatorFactory(IHttpContextAccessor httpContextAccessor)
        {
            HttpContext = httpContextAccessor.HttpContext;
        }

        /// <inheritdoc/>
        public override IValidator CreateInstance(Type validatorType)
        {
            return HttpContext.RequestServices.GetService(validatorType) as IValidator;
        }
    }
}
