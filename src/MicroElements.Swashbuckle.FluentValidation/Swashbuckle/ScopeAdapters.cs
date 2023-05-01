// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using MicroElements.Swashbuckle.FluentValidation.AspNetCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

#pragma warning disable SA1402
#pragma warning disable SA1600
#pragma warning disable SA1649
#pragma warning disable CS1591

namespace MicroElements.Swashbuckle.FluentValidation
{
    /// <summary>
    /// Base scope adapter.
    /// </summary>
    /// <typeparam name="TService">Filter type.</typeparam>
    public abstract class ScopeAdapter<TService>
    {
        private readonly Lazy<TService> _filter;

        protected TService Filter => _filter.Value;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScopeAdapter{TService}"/> class.
        /// </summary>
        /// <param name="serviceProvider"><see cref="IServiceProvider"/>.</param>
        /// <param name="serviceLifetime"><see cref="ServiceLifetime"/> to use.</param>
        protected ScopeAdapter(IServiceProvider serviceProvider, ServiceLifetime serviceLifetime)
        {
            _filter = new Lazy<TService>(() => Create(serviceProvider, serviceLifetime));
        }

        private TService Create(IServiceProvider serviceProvider, ServiceLifetime serviceLifetime)
        {
            // Hack with the scope mismatch.
            if (serviceLifetime is ServiceLifetime.Scoped or ServiceLifetime.Transient)
                serviceProvider = serviceProvider.CreateScope().ServiceProvider;

            var service = serviceProvider.GetService<TService>();

            // Last chance to create filter
            service ??= ActivatorUtilities.CreateInstance<TService>(serviceProvider);

            if (service == null)
            {
                var logger = serviceProvider.GetService<ILoggerFactory>()?.CreateLogger(GetType());
                logger?.LogWarning("{ServiceType} should be registered in services. Hint: Use registration method '{Method}'", nameof(TService), nameof(ServiceCollectionExtensions.AddFluentValidationRulesToSwagger));
            }

            return service;
        }
    }

    /// <summary>
    /// Creates filter from service provider with desired lifestyle.
    /// </summary>
    /// <typeparam name="TFilter">Filter type.</typeparam>
    public class SchemaFilterScopeAdapter<TFilter> : ScopeAdapter<TFilter>, ISchemaFilter
        where TFilter : ISchemaFilter
    {
        public SchemaFilterScopeAdapter(IServiceProvider serviceProvider, ServiceLifetime serviceLifetime)
            : base(serviceProvider, serviceLifetime)
        {
        }

        /// <inheritdoc />
        public void Apply(OpenApiSchema schema, SchemaFilterContext context)
        {
            Filter.Apply(schema, context);
        }
    }

    /// <summary>
    /// Creates filter from service provider with desired lifestyle.
    /// </summary>
    /// <typeparam name="TFilter">Filter type.</typeparam>
    public class OperationFilterScopeAdapter<TFilter> : ScopeAdapter<TFilter>, IOperationFilter
        where TFilter : IOperationFilter
    {
        public OperationFilterScopeAdapter(IServiceProvider serviceProvider, ServiceLifetime serviceLifetime)
            : base(serviceProvider, serviceLifetime)
        {
        }

        /// <inheritdoc />
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            Filter.Apply(operation, context);
        }
    }

    /// <summary>
    /// Creates filter from service provider with desired lifestyle.
    /// </summary>
    /// <typeparam name="TFilter">Filter type.</typeparam>
    public class DocumentFilterScopeAdapter<TFilter> : ScopeAdapter<TFilter>, IDocumentFilter
        where TFilter : IDocumentFilter
    {
        public DocumentFilterScopeAdapter(IServiceProvider serviceProvider, ServiceLifetime serviceLifetime)
            : base(serviceProvider, serviceLifetime)
        {
        }

        /// <inheritdoc />
        public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
        {
            Filter.Apply(swaggerDoc, context);
        }
    }

    /// <summary>
    /// Creates filter from service provider with desired lifestyle.
    /// </summary>
    /// <typeparam name="TFilter">Filter type.</typeparam>
    public class RequestBodyFilterScopeAdapter<TFilter> : ScopeAdapter<TFilter>, IRequestBodyFilter
        where TFilter : IRequestBodyFilter
    {
        public RequestBodyFilterScopeAdapter(IServiceProvider serviceProvider, ServiceLifetime serviceLifetime)
            : base(serviceProvider, serviceLifetime)
        {
        }

        public void Apply(OpenApiRequestBody requestBody, RequestBodyFilterContext context)
        {
            Filter.Apply(requestBody, context);
        }
    }
}