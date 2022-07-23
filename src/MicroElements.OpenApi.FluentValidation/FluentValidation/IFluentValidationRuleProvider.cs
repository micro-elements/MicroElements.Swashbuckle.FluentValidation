// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace MicroElements.OpenApi.FluentValidation
{
    /// <summary>
    /// Provides rules for schema generation.
    /// </summary>
    public interface IFluentValidationRuleProvider<TSchema>
    {
        /// <summary>
        /// Gets rules for schema generation.
        /// </summary>
        /// <returns>Enumeration of <see cref="FluentValidationRule"/>.</returns>
        IEnumerable<IFluentValidationRule<TSchema>> GetRules();
    }
}