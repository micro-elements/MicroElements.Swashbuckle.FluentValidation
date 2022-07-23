// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using FluentValidation;
using Microsoft.AspNetCore.Http;

namespace MicroElements.Swashbuckle.FluentValidation
{
    /// <summary>
    /// Allows for the creation of validators that have dependencies on scoped services.
    /// Add <c>services.AddHttpContextAccessor();</c> to use <see cref="IHttpContextAccessor"/>.
    /// </summary>
    public class HttpContextValidatorRegistry : IValidatorRegistry
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpContextValidatorRegistry"/> class.
        /// </summary>
        /// <param name="httpContextAccessor">Access to the current HttpContext.</param>
        public HttpContextValidatorRegistry(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        /// <inheritdoc />
        public IValidator? GetValidator(Type type)
        {
            IServiceProvider serviceProvider = _httpContextAccessor.HttpContext.RequestServices;
            Type validatorType = typeof(IValidator<>).MakeGenericType(type);
            return serviceProvider.GetService(validatorType) as IValidator;
        }
    }
}
