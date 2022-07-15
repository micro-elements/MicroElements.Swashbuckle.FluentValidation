// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using FluentValidation;

namespace MicroElements.Swashbuckle.FluentValidation
{
    /// <summary>
    /// Gets validators for a particular type.
    /// </summary>
    public interface IValidatorRegistry
    {
        /// <summary>
        /// Gets the validator for the specified type.
        /// </summary>
        /// <param name="type">Type to validate.</param>
        /// <returns>Validator or <see langword="null"/>.</returns>
        IValidator? GetValidator(Type type);
    }
}