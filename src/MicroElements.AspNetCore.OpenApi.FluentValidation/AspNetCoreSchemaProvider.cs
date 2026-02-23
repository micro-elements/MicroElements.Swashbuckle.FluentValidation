// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using MicroElements.OpenApi.FluentValidation;
using Microsoft.AspNetCore.OpenApi;
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

        /// <summary>
        /// Initializes a new instance of the <see cref="AspNetCoreSchemaProvider"/> class.
        /// </summary>
        /// <param name="context">Optional transformer context (used for GetOrCreateSchemaAsync on .NET 10+).</param>
        public AspNetCoreSchemaProvider(OpenApiSchemaTransformerContext? context = null)
        {
            _context = context;
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
                catch (Exception)
                {
                    // Fallback to empty schema if sub-schema resolution fails.
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
