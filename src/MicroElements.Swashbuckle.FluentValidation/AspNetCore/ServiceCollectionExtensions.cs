// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using MicroElements.OpenApi.FluentValidation;
using MicroElements.Swashbuckle.FluentValidation.Generation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
#if !OPENAPI_V2
using Microsoft.OpenApi.Models;
#endif
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MicroElements.Swashbuckle.FluentValidation.AspNetCore
{
    /// <summary>
    /// ServiceCollection extensions.
    /// </summary>
    [SuppressMessage("ReSharper", "RedundantTypeArgumentsOfMethod", Justification = "More obvious")]
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds FluentValidationRules staff to Swagger.
        /// </summary>
        /// <param name="services">Services.</param>
        /// <param name="configure">Optional configure action.</param>
        /// <param name="configureRegistration">Optional configure registration options.</param>
        /// <returns>The same service collection.</returns>
        public static IServiceCollection AddFluentValidationRulesToSwagger(
            this IServiceCollection services,
            Action<SchemaGenerationOptions>? configure = null,
            Action<RegistrationOptions>? configureRegistration = null)
        {
            var registrationOptions = new RegistrationOptions();
            configureRegistration?.Invoke(registrationOptions);

            // Adds fluent validation rules to swagger
            if (registrationOptions.RegisterFluentValidationRules)
            {
                if (registrationOptions.ExperimentalUseDocumentFilter)
                {
                    services.TryAdd(new ServiceDescriptor(typeof(FluentValidationDocumentFilter), typeof(FluentValidationDocumentFilter), registrationOptions.ServiceLifetime));
                }
                else
                {
                    services.TryAdd(new ServiceDescriptor(typeof(FluentValidationRules), typeof(FluentValidationRules), registrationOptions.ServiceLifetime));
                    services.TryAdd(new ServiceDescriptor(typeof(FluentValidationOperationFilter), typeof(FluentValidationOperationFilter), registrationOptions.ServiceLifetime));
                }

                services.Configure<SwaggerGenOptions>(options =>
                {
                    // Registers Swashbuckle filters
                    if (registrationOptions.ExperimentalUseDocumentFilter)
                    {
                        options.DocumentFilter<DocumentFilterScopeAdapter<FluentValidationDocumentFilter>>(registrationOptions.ServiceLifetime);
                    }
                    else
                    {
                        options.SchemaFilter<FluentValidationRulesScopeAdapter>(registrationOptions.ServiceLifetime);
                        options.OperationFilter<FluentValidationOperationFilterScopeAdapter>(registrationOptions.ServiceLifetime);
                    }
                });
            }

            // Register JsonSerializerOptions (reference to Microsoft.AspNetCore.Mvc.JsonOptions.Value)
            if (registrationOptions.RegisterJsonSerializerOptions)
            {
                services.TryAddTransient<AspNetJsonSerializerOptions>(provider => new AspNetJsonSerializerOptions(provider.GetJsonSerializerOptionsOrDefault()));
                services.TryAddTransient<JsonSerializerOptions>(provider => provider.GetService<AspNetJsonSerializerOptions>()?.Value!);
            }

            // Adds name resolver. For example when property name in schema differs from property name in dotnet class.
            if (registrationOptions.RegisterSystemTextJsonNameResolver)
            {
                services.TryAddSingleton<INameResolver, SystemTextJsonNameResolver>();
            }

            // Adds default IValidatorRegistry
            services.TryAdd(new ServiceDescriptor(typeof(IValidatorRegistry), typeof(ServiceProviderValidatorRegistry), registrationOptions.ServiceLifetime));

            // Adds IFluentValidationRuleProvider
            services.TryAddSingleton<IFluentValidationRuleProvider<OpenApiSchema>, DefaultFluentValidationRuleProvider>();

            // DI injected services
            services.TryAddTransient<IServicesContext, ServicesContext>();

            // Schema generation configuration
            if (configure != null)
                services.Configure<SchemaGenerationOptions>(configure);

            // PostConfigure SchemaGenerationOptions
            services.TryAddEnumerable(
                ServiceDescriptor.Transient<IPostConfigureOptions<SchemaGenerationOptions>, FillSchemaGenerationOptions>());

            return services;
        }
    }
}
