// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MicroElements.Swashbuckle.FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Swashbuckle.AspNetCore.SwaggerGen;

// ReSharper disable once CheckNamespace
namespace Swashbuckle.AspNetCore.Swagger
{
    /// <summary>
    /// Registration extensions.
    /// </summary>
    public static class FluentValidationRulesRegistrator
    {
        /// <summary>
        /// Adds fluent validation rules to swagger.
        /// </summary>
        /// <param name="options">Swagger options.</param>
        /// <param name="serviceLifetime">Use scoped registration adapter.</param>
        public static void AddFluentValidationRules(this SwaggerGenOptions options, ServiceLifetime serviceLifetime = ServiceLifetime.Scoped)
        {
            options.SchemaFilter<FluentValidationRulesScopeAdapter>(serviceLifetime);
            options.OperationFilter<FluentValidationOperationFilterScopeAdapter>(serviceLifetime);
        }
    }
}