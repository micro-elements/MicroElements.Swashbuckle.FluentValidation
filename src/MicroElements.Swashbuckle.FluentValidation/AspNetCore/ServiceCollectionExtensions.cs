// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Text.Json;
using MicroElements.Swashbuckle.FluentValidation.Generation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MicroElements.Swashbuckle.FluentValidation.AspNetCore
{
    /// <summary>
    /// ServiceCollection extensions.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds FluentValidationRules stuff to Swagger.
        /// </summary>
        /// <param name="services">Services.</param>
        /// <param name="configure">Optional configure action.</param>
        /// <param name="configureRegistration">Optional configure registration options.</param>
        /// <param name="configureServices">Optional configure services action.</param>
        /// <returns>The same service collection.</returns>
        public static IServiceCollection AddFluentValidationRulesToSwagger(
            this IServiceCollection services,
            Action<SchemaGenerationOptions>? configure = null,
            Action<RegistrationOptions>? configureRegistration = null,
            Action<IServiceCollection>? configureServices = null)
        {
            ValidatorServiceProvider validationServiceProvider = new ValidatorServiceProvider(services);
            configureServices?.Invoke(validationServiceProvider);
            services.AddSingleton(validationServiceProvider);

            var registrationOptions = new RegistrationOptions();
            configureRegistration?.Invoke(registrationOptions);

            // Adds fluent validation rules to swagger
            if (registrationOptions.RegisterFluentValidationRules)
            {
                services.Add(new ServiceDescriptor(typeof(FluentValidationRules), typeof(FluentValidationRules), registrationOptions.ServiceLifetime));
                services.Add(new ServiceDescriptor(typeof(FluentValidationOperationFilter), typeof(FluentValidationOperationFilter), registrationOptions.ServiceLifetime));

                services.Configure<SwaggerGenOptions>(options =>
                {
                    // Registers Swashbuckle filters
                    options.SchemaFilter<FluentValidationRulesScopeAdapter>(registrationOptions.ServiceLifetime);
                    options.OperationFilter<FluentValidationOperationFilterScopeAdapter>(registrationOptions.ServiceLifetime);
                });
            }

            // Register JsonSerializerOptions (reference to Microsoft.AspNetCore.Mvc.JsonOptions.Value)
            if (registrationOptions.RegisterJsonSerializerOptions)
            {
                services.AddTransient<AspNetJsonSerializerOptions>(provider => new AspNetJsonSerializerOptions(provider.GetJsonSerializerOptionsOrDefault()));
                services.AddTransient<JsonSerializerOptions>(provider => provider.GetService<AspNetJsonSerializerOptions>().Value);
            }

            // Adds name resolver. For example when property name in schema differs from property name in dotnet class.
            if (registrationOptions.RegisterSystemTextJsonNameResolver)
            {
                services.TryAddSingleton<INameResolver, SystemTextJsonNameResolver>();
            }

            // Schema generation configuration.
            if (configure != null)
                services.Configure<SchemaGenerationOptions>(configure);

            return services;
        }
    }
}
