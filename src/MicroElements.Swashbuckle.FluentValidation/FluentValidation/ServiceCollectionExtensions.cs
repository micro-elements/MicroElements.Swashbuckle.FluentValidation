// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace MicroElements.FluentValidation
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers all registered validators as base <see cref="IValidator"/>.
        /// </summary>
        /// <param name="services">The source services.</param>
        /// <returns>Modified services.</returns>
        public static IServiceCollection RegisterValidatorsAsIValidator(this IServiceCollection services)
        {
            // Register all validators as IValidator?
            var serviceDescriptors = services.Where(descriptor => descriptor.ServiceType.GetInterfaces().Contains(typeof(IValidator))).ToList();
            serviceDescriptors.ForEach(descriptor => services.Add(ServiceDescriptor.Describe(typeof(IValidator), descriptor.ImplementationType, descriptor.Lifetime)));
            return services;
        }
    }
}