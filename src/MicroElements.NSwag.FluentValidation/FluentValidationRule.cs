// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using FluentValidation.Validators;
using MicroElements.OpenApi.FluentValidation;
using NJsonSchema.Generation;

namespace MicroElements.NSwag.FluentValidation
{
    /// <summary>
    /// FluentValidationRule.
    /// </summary>
    public class FluentValidationRule : IFluentValidationRule<SchemaProcessorContext>
    {
        /// <summary>
        /// Rule name.
        /// </summary>
        public string Name { get; }

        /// <inheritdoc />
        public IReadOnlyCollection<Func<IPropertyValidator, bool>> Conditions { get; private set; }

        /// <summary>
        /// Predicate to match property validator.
        /// </summary>
        public Func<IPropertyValidator, bool> Matches
        {
            init => Conditions = new[] { value };
        }

        /// <inheritdoc />
        public Action<IRuleContext<SchemaProcessorContext>>? Apply { get; set; }

        /// <summary>
        /// Creates new instance of <see cref="FluentValidationRule"/>.
        /// </summary>
        /// <param name="name">Rule name.</param>
        public FluentValidationRule(string name)
        {
            Name = name;
        }
    }
}