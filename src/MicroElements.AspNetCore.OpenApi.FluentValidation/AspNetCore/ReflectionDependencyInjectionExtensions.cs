// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MicroElements.AspNetCore.OpenApi.FluentValidation.AspNetCore
{
    /// <summary>
    /// DependencyInjection through Reflection.
    /// </summary>
    internal static class ReflectionDependencyInjectionExtensions
    {
        private static readonly ConcurrentDictionary<string, Type?> _typeCache = new();

        private static Type? GetByFullName(string typeName)
        {
            return _typeCache.GetOrAdd(typeName, static name =>
            {
                return AppDomain
                    .CurrentDomain
                    .GetAssemblies()
                    .Where(assembly => assembly.FullName?.Contains("Microsoft") == true)
                    .SelectMany(GetLoadableTypes)
                    .FirstOrDefault(type => type.FullName == name);
            });
        }

        /// <summary>
        /// Gets loadable Types from an Assembly, not throwing when some Types can't be loaded.
        /// </summary>
        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                return e.Types.Where(t => t != null);
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
