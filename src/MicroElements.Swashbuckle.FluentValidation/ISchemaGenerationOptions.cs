// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MicroElements.Swashbuckle.FluentValidation.Generation;

namespace MicroElements.Swashbuckle.FluentValidation
{
    /// <summary>
    /// Schema generation options.
    /// </summary>
    public interface ISchemaGenerationOptions
    {
        /// <summary>
        /// Gets a value indicating whether property should be set to not nullable if MinLength is greater then zero.
        /// </summary>
        bool SetNotNullableIfMinLengthGreaterThenZero { get; }

        /// <summary>
        /// Gets a value indicating whether schema supports AllOf.
        /// </summary>
        bool IsAllOffSupported { get; }

        /// <summary>
        /// Gets <see cref="INameResolver"/>.
        /// </summary>
        INameResolver? NameResolver { get; }
    }

    /// <summary>
    /// Schema generation options.
    /// </summary>
    public class SchemaGenerationOptions : ISchemaGenerationOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether property should be set to not nullable if MinLength is greater then zero.
        /// </summary>
        public bool SetNotNullableIfMinLengthGreaterThenZero { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether schema supports AllOf.
        /// </summary>
        public bool IsAllOffSupported { get; set; } = true;

        /// <summary>
        /// Gets or sets <see cref="INameResolver"/>.
        /// </summary>
        public INameResolver? NameResolver { get; set; } = new SystemTextJsonNameResolver();

        /// <summary>
        /// Sets values that compatible with FluentValidation.
        /// </summary>
        /// <returns>The same options.</returns>
        public SchemaGenerationOptions SetFluentValidationCompatibility()
        {
            SetNotNullableIfMinLengthGreaterThenZero = false;
            return this;
        }
    }
}