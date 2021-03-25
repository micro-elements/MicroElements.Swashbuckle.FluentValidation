// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using FluentValidation;
using Microsoft.AspNetCore.Http;

namespace MicroElements.Swashbuckle.FluentValidation
{
    /// <summary>
    /// Allows for the creation of validators that have dependencies on scoped services.
    /// </summary>
    public class HttpContextServiceProviderValidatorFactory : ValidatorFactoryBase
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpContextServiceProviderValidatorFactory"/> class.
        /// </summary>
        /// <param name="httpContextAccessor">Access to the current HttpContext</param>
        public HttpContextServiceProviderValidatorFactory(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        /// <inheritdoc/>
        public override IValidator CreateInstance(Type validatorType)
        {
            var serviceProvider = _httpContextAccessor.HttpContext.RequestServices;
            return serviceProvider.GetService(validatorType) as IValidator;
        }
    }
}
