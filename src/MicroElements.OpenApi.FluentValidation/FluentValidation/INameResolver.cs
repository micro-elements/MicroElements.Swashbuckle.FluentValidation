// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

namespace MicroElements.OpenApi.FluentValidation
{
    /// <summary>
    /// Name resolver.
    /// Gets property name using naming conventions.
    /// </summary>
    public interface INameResolver
    {
        /// <summary>
        /// Gets schema name for property.
        /// </summary>
        /// <param name="propertyInfo">Property info.</param>
        /// <returns>Property schema name.</returns>
        string GetPropertyName(PropertyInfo propertyInfo);
    }
}