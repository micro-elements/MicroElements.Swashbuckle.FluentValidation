// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
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
        //TODO: remove FluentValidationRulesRegistrator
        /// <summary>
        /// Adds fluent validation rules to swagger.
        /// </summary>
        /// <param name="options">Swagger options.</param>
        [Obsolete("Use ServiceCollectionExtensions.AddFluentValidationRulesToSwagger instead of this to allow all features.")]
        public static void AddFluentValidationRules(this SwaggerGenOptions options)
        {
            options.SchemaFilter<FluentValidationRules>();
            options.OperationFilter<FluentValidationOperationFilter>();
        }

        /// <summary>
        /// Adds fluent validation rules to swagger.
        /// NOTE: Use ServiceCollectionExtensions.AddFluentValidationRulesToSwagger instead of this to allow all features.
        /// </summary>
        /// <param name="options">Swagger options.</param>
        /// <param name="serviceLifetime">Use scoped registration adapter.</param>
        public static void AddFluentValidationRulesScoped(this SwaggerGenOptions options, ServiceLifetime serviceLifetime = ServiceLifetime.Scoped)
        {
            options.SchemaFilter<FluentValidationRulesScopeAdapter>(serviceLifetime);
            options.OperationFilter<FluentValidationOperationFilterScopeAdapter>(serviceLifetime);
        }
    }
}