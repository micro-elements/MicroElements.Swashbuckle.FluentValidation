// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using MicroElements.OpenApi.FluentValidation;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
#if !OPENAPI_V2
using Microsoft.OpenApi.Models;
#endif

namespace MicroElements.AspNetCore.OpenApi.FluentValidation
{
    /// <summary>
    /// Schema provider for Microsoft.AspNetCore.OpenApi.
    /// .NET 9: returns empty schema (limited nested validator support).
    /// .NET 10+: uses <c>GetOrCreateSchemaAsync</c> for full sub-schema resolution.
    /// </summary>
    internal class AspNetCoreSchemaProvider : ISchemaProvider<OpenApiSchema>
    {
        private readonly OpenApiSchemaTransformerContext? _context;
        private readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="AspNetCoreSchemaProvider"/> class.
        /// </summary>
        /// <param name="context">Optional transformer context (used for GetOrCreateSchemaAsync on .NET 10+).</param>
        /// <param name="logger">Optional logger.</param>
        public AspNetCoreSchemaProvider(OpenApiSchemaTransformerContext? context = null, ILogger? logger = null)
        {
            _context = context;
            _logger = logger ?? NullLogger.Instance;
        }

        /// <inheritdoc />
        public OpenApiSchema GetSchemaForType(Type type)
        {
#if NET10_0_OR_GREATER
            if (_context != null)
            {
                // NOTE: Sync-over-async via GetAwaiter().GetResult().
                // This is safe in ASP.NET Core because it does not use a SynchronizationContext
                // (removed since ASP.NET Core 1.0), so there is no deadlock risk.
                // The ISchemaProvider<T> interface is synchronous by contract (defined in core package).
                // A future async variant of ISchemaProvider may be introduced to avoid this pattern.
                try
                {
                    return _context.GetOrCreateSchemaAsync(type, null, CancellationToken.None)
                        .GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "GetOrCreateSchemaAsync failed for type '{SchemaType}'. Falling back to empty schema.", type);
                    return new OpenApiSchema();
                }
            }
#endif
            // .NET 9 fallback: return empty schema.
            // Nested validator support is limited in .NET 9.
            return new OpenApiSchema();
        }
    }
}
