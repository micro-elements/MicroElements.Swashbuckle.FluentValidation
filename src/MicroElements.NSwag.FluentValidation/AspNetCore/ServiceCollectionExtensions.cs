// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using MicroElements.OpenApi.AspNetCore;
using MicroElements.OpenApi.FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace MicroElements.NSwag.FluentValidation.AspNetCore
{
    /// <summary>
    /// ServiceCollection extensions.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds FluentValidationRules staff to Swagger.
        /// </summary>
        /// <param name="services">Services.</param>
        /// <param name="configure">Optional configure action.</param>
        /// <returns>The same service collection.</returns>
        public static IServiceCollection AddFluentValidationRulesToSwagger(
            this IServiceCollection services,
            Action<SchemaGenerationOptions>? configure = null,
            Action<RegistrationOptions>? configureRegistration = null)
        {
            var registrationOptions = new RegistrationOptions();
            configureRegistration?.Invoke(registrationOptions);

            // Add the FluentValidationSchemaProcessor as a scoped service
            services.AddScoped<FluentValidationSchemaProcessor>();

            // Adds default IValidatorRegistry
            services.TryAdd(new ServiceDescriptor(typeof(IValidatorRegistry), typeof(ServiceProviderValidatorRegistry), registrationOptions.ServiceLifetime));

            // DI injected services
            services.AddTransient<IServicesContext, ServicesContext>();

            // Schema generation configuration
            if (configure != null)
                services.Configure<SchemaGenerationOptions>(configure);

            services.AddTransient<IPostConfigureOptions<SchemaGenerationOptions>, PostConfigureSchemaGenerationOptions>();

            return services;
        }
    }

    /// <summary>
    /// Registration customization.
    /// </summary>
    public class RegistrationOptions
    {
        /// <summary>
        /// ServiceLifetime to use for service registration.
        /// </summary>
        public ServiceLifetime ServiceLifetime { get; set; } = ServiceLifetime.Scoped;
    }
}
