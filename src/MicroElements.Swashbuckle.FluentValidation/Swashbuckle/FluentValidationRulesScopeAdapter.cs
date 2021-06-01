// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MicroElements.Swashbuckle.FluentValidation
{
    /// <summary>
    /// Creates service from service provider with desired lifestyle.
    /// </summary>
    public class FluentValidationRulesScopeAdapter : ISchemaFilter
    {
        private readonly FluentValidationRules _fluentValidationRules;

        /// <summary>
        /// Initializes a new instance of the <see cref="FluentValidationRulesScopeAdapter"/> class.
        /// </summary>
        /// <param name="serviceProvider"><see cref="IServiceProvider"/>.</param>
        /// <param name="serviceLifetime"><see cref="ServiceLifetime"/> to use.</param>
        public FluentValidationRulesScopeAdapter(IServiceProvider serviceProvider, ServiceLifetime serviceLifetime)
        {
            if (serviceLifetime == ServiceLifetime.Scoped || serviceLifetime == ServiceLifetime.Transient)
                serviceProvider = serviceProvider.CreateScope().ServiceProvider;
            _fluentValidationRules = serviceProvider.GetService<FluentValidationRules>();
        }

        /// <inheritdoc />
        public void Apply(OpenApiSchema schema, SchemaFilterContext context)
        {
            _fluentValidationRules.Apply(schema, context);
        }
    }

    /// <summary>
    /// Creates service from service provider with desired lifestyle.
    /// </summary>
    public class FluentValidationOperationFilterScopeAdapter : IOperationFilter
    {
        private readonly FluentValidationOperationFilter _fluentValidationRules;

        /// <summary>
        /// Initializes a new instance of the <see cref="FluentValidationOperationFilterScopeAdapter"/> class.
        /// </summary>
        /// <param name="serviceProvider"><see cref="IServiceProvider"/>.</param>
        /// <param name="serviceLifetime"><see cref="ServiceLifetime"/> to use.</param>
        public FluentValidationOperationFilterScopeAdapter(IServiceProvider serviceProvider, ServiceLifetime serviceLifetime)
        {
            if (serviceLifetime == ServiceLifetime.Scoped || serviceLifetime == ServiceLifetime.Transient)
                serviceProvider = serviceProvider.CreateScope().ServiceProvider;
            _fluentValidationRules = serviceProvider.GetService<FluentValidationOperationFilter>();
        }

        /// <inheritdoc />
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            _fluentValidationRules.Apply(operation, context);
        }
    }
}