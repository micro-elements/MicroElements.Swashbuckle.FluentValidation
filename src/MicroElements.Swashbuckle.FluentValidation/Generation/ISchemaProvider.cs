// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace MicroElements.Swashbuckle.FluentValidation.Generation
{
    /// <summary>
    /// Schema provider.
    /// </summary>
    /// <typeparam name="TSchema">Schema type.</typeparam>
    public interface ISchemaProvider<TSchema>
    {
        /// <summary>
        /// Gets or creates schema for type.
        /// </summary>
        /// <param name="type">Type.</param>
        /// <returns>Schema.</returns>
        TSchema GetSchemaForType(Type type);
    }
}