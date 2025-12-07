// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using MicroElements.Swashbuckle.FluentValidation.AspNetCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
#if !OPENAPI_V2
using Microsoft.OpenApi.Models;
#endif
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
            // Hack with the scope mismatch.
            if (serviceLifetime == ServiceLifetime.Scoped || serviceLifetime == ServiceLifetime.Transient)
                serviceProvider = serviceProvider.CreateScope().ServiceProvider;

            _fluentValidationRules = serviceProvider.GetService<FluentValidationRules>();

            if (_fluentValidationRules == null)
            {
                var logger = serviceProvider.GetService<ILoggerFactory>()?.CreateLogger(typeof(FluentValidationRulesScopeAdapter));
                logger?.LogWarning($"{nameof(FluentValidationRules)} should be registered in services. Hint: Use registration method '{nameof(ServiceCollectionExtensions.AddFluentValidationRulesToSwagger)}'");
            }

            // Last chance to create filter
            _fluentValidationRules ??= ActivatorUtilities.CreateInstance<FluentValidationRules>(serviceProvider);
        }

        /// <inheritdoc />
#if OPENAPI_V2
        public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
        {
            _fluentValidationRules.Apply(schema, context);
        }
#else
        public void Apply(OpenApiSchema schema, SchemaFilterContext context)
        {
            _fluentValidationRules.Apply(schema, context);
        }
#endif
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
            // Hack with the scope mismatch.
            if (serviceLifetime == ServiceLifetime.Scoped || serviceLifetime == ServiceLifetime.Transient)
                serviceProvider = serviceProvider.CreateScope().ServiceProvider;

            _fluentValidationRules = serviceProvider.GetService<FluentValidationOperationFilter>();

            if (_fluentValidationRules == null)
            {
                var logger = serviceProvider.GetService<ILoggerFactory>()?.CreateLogger(typeof(FluentValidationRulesScopeAdapter));
                logger?.LogWarning($"{nameof(FluentValidationOperationFilter)} should be registered in services. Hint: Use registration method '{nameof(ServiceCollectionExtensions.AddFluentValidationRulesToSwagger)}'");
            }

            // Last chance to create filter
            _fluentValidationRules ??= ActivatorUtilities.CreateInstance<FluentValidationOperationFilter>(serviceProvider);
        }

        /// <inheritdoc />
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            _fluentValidationRules.Apply(operation, context);
        }
    }

    /// <summary>
    /// Creates service from service provider with desired lifestyle.
    /// </summary>
    public class DocumentFilterScopeAdapter<TDocumentFilter> : IDocumentFilter
        where TDocumentFilter : IDocumentFilter
    {
        private readonly IDocumentFilter _documentFilter;

        /// <summary>
        /// Initializes a new instance of the <see cref="DocumentFilterScopeAdapter{TDocumentFilter}"/> class.
        /// </summary>
        /// <param name="serviceProvider"><see cref="IServiceProvider"/>.</param>
        /// <param name="serviceLifetime"><see cref="ServiceLifetime"/> to use.</param>
        public DocumentFilterScopeAdapter(IServiceProvider serviceProvider, ServiceLifetime serviceLifetime)
        {
            // Hack with the scope mismatch.
            if (serviceLifetime == ServiceLifetime.Scoped || serviceLifetime == ServiceLifetime.Transient)
                serviceProvider = serviceProvider.CreateScope().ServiceProvider;

            _documentFilter = serviceProvider.GetService<TDocumentFilter>();

            if (_documentFilter == null)
            {
                var logger = serviceProvider.GetService<ILoggerFactory>()?.CreateLogger(GetType());
                logger?.LogWarning($"{nameof(TDocumentFilter)} should be registered in services. Hint: Use registration method '{nameof(ServiceCollectionExtensions.AddFluentValidationRulesToSwagger)}'");
            }

            // Last chance to create filter
            _documentFilter ??= ActivatorUtilities.CreateInstance<TDocumentFilter>(serviceProvider);
        }

        /// <inheritdoc />
        public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
        {
            _documentFilter.Apply(swaggerDoc, context);
        }
    }
}