// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace MicroElements.OpenApi.FluentValidation
{
    /// <summary>
    /// SchemaGenerationSettings provides runtime options and services.
    /// </summary>
    public interface ISchemaGenerationSettings
    {
        /// <summary>
        /// Gets <see cref="INameResolver"/>.
        /// </summary>
        INameResolver? NameResolver { get; }

        /// <summary>
        /// Gets schemaId by type.
        /// </summary>
        Func<Type, string>? SchemaIdSelector { get; }
    }

    /// <summary>
    /// SchemaGenerationSettings provides runtime options and services.
    /// </summary>
    public record SchemaGenerationSettings : ISchemaGenerationSettings
    {
        /// <inheritdoc />
        public INameResolver? NameResolver { get; init; }

        /// <summary>
        /// Gets schemaId by type.
        /// </summary>
        public Func<Type, string>? SchemaIdSelector { get; init; }
    }
}