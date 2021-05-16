// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using MicroElements.Swashbuckle.FluentValidation.Generation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MicroElements.Swashbuckle.FluentValidation.AspNetCore
{
    /// <summary>
    /// ServiceCollection extensions.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registration customization.
        /// </summary>
        public class RegistrationOptions
        {
            /// <summary>
            /// Register fluent validation rules generators to swagger.
            /// Default: true.
            /// </summary>
            public bool RegisterFluentValidationRules { get; set; } = true;

            /// <summary>
            /// Register <see cref="AspNetJsonSerializerOptions"/> and <see cref="JsonSerializerOptions"/> as reference to Microsoft.AspNetCore.Mvc.JsonOptions.Value.
            /// Default: true.
            /// </summary>
            public bool RegisterJsonSerializerOptions { get; set; } = true;

            /// <summary>
            /// Register <see cref="SystemTextJsonNameResolver"/> as default <see cref="INameResolver"/>.
            /// Default: true.
            /// </summary>
            public bool RegisterSystemTextJsonNameResolver { get; set; } = true;
        }

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
                services.Configure<SwaggerGenOptions>(options =>
                    options.AddFluentValidationRules());
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

    /// <summary>
    /// DependencyInjection through Reflection.
    /// </summary>
    internal static class ReflectionDependencyInjectionExtensions
    {
        public static Type? GetByFullName(string typeName)
        {
            Type type = AppDomain
                .CurrentDomain
                .GetAssemblies()
                .Where(assembly => assembly.FullName.Contains("Microsoft"))
                .SelectMany(assembly => assembly.GetTypes())
                .FirstOrDefault(type => type.FullName == typeName);

            return type;
        }

        /// <summary>
        /// Calls through reflection: <c>services.Configure&lt;JsonOptions&gt;(options =&gt; configureJson(options));</c>.
        /// Can be used from netstandard.
        /// </summary>
        /// <param name="services">Services.</param>
        /// <param name="configureJson">Action to configure <see cref="JsonSerializerOptions"/> in JsonOptions.</param>
        public static void ConfigureJsonOptionsForAspNetCore(this IServiceCollection services, Action<JsonSerializerOptions> configureJson)
        {
            Action<object> configureJsonOptionsUntyped = options =>
            {
                PropertyInfo? propertyInfo = options.GetType().GetProperty("JsonSerializerOptions");

                if (propertyInfo?.GetValue(options) is JsonSerializerOptions jsonSerializerOptions)
                {
                    configureJson(jsonSerializerOptions);
                }
            };

            Type? jsonOptionsType = GetByFullName("Microsoft.AspNetCore.Mvc.JsonOptions");
            if (jsonOptionsType != null)
            {
                Type? extensionsType = GetByFullName("Microsoft.Extensions.DependencyInjection.OptionsServiceCollectionExtensions");

                MethodInfo? configureMethodGeneric = extensionsType
                    ?.GetTypeInfo()
                    .DeclaredMethods
                    .FirstOrDefault(info => info.Name == "Configure" && info.GetParameters().Length == 2);

                MethodInfo? configureMethod = configureMethodGeneric?.MakeGenericMethod(jsonOptionsType);

                if (configureMethod != null)
                {
                    // services.Configure<JsonOptions>(options => configureJson(options));
                    configureMethod.Invoke(services, new object?[] { services, configureJsonOptionsUntyped });
                }
            }
        }

        /// <summary>
        /// Gets <see cref="JsonSerializerOptions"/> from JsonOptions registered in AspNetCore.
        /// Uses reflection to call code:
        /// <code>serviceProvider.GetService&lt;IOptions&lt;JsonOptions&gt;&gt;()?.Value?.JsonSerializerOptions;</code>
        /// </summary>
        /// <param name="serviceProvider">Source service provider.</param>
        /// <returns>Optional <see cref="JsonSerializerOptions"/>.</returns>
        public static JsonSerializerOptions? GetJsonSerializerOptions(this IServiceProvider serviceProvider)
        {
            JsonSerializerOptions? jsonSerializerOptions = null;

            Type? jsonOptionsType = GetByFullName("Microsoft.AspNetCore.Mvc.JsonOptions");
            if (jsonOptionsType != null)
            {
                // IOptions<JsonOptions>
                Type jsonOptionsInterfaceType = typeof(IOptions<>).MakeGenericType(jsonOptionsType);
                object? jsonOptionsOption = serviceProvider.GetService(jsonOptionsInterfaceType);

                if (jsonOptionsOption != null)
                {
                    PropertyInfo? valueProperty = jsonOptionsInterfaceType.GetProperty("Value", BindingFlags.Instance | BindingFlags.Public);
                    PropertyInfo? jsonSerializerOptionsProperty = jsonOptionsType.GetProperty("JsonSerializerOptions", BindingFlags.Instance | BindingFlags.Public);

                    if (valueProperty != null && jsonSerializerOptionsProperty != null)
                    {
                        // JsonOptions
                        var jsonOptions = valueProperty.GetValue(jsonOptionsOption);

                        // JsonSerializerOptions
                        if (jsonOptions != null)
                        {
                            jsonSerializerOptions = jsonSerializerOptionsProperty.GetValue(jsonOptions) as JsonSerializerOptions;
                        }
                    }
                }
            }

            return jsonSerializerOptions;
        }

        public static JsonSerializerOptions GetJsonSerializerOptionsOrDefault(this IServiceProvider serviceProvider)
        {
            return serviceProvider.GetJsonSerializerOptions() ?? new JsonSerializerOptions();
        }
    }
}
