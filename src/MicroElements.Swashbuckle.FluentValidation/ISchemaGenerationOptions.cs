// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentValidation.Validators;
using System;
using System.Collections.Generic;

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
        /// Gets a value indicating whether schema generator should use AllOf for multiple rules (for example for multiple patterns).
        /// </summary>
        bool UseAllOffForMultipleRules { get; }

        /// <summary>
        /// Gets a value indicating whether conditional rules are allowed to effect schema generation.
        /// </summary>
        bool AllowConditionalRules { get; }

        /// <summary>
        /// Gets a value indicating whether conditional validators are allowed to effect schema generation.
        /// </summary>
        bool AllowConditionalValidators { get; }

        /// <summary>
        /// Gets a list that contains the allowed conditional validator types.
        /// </summary>
        List<Type> AllowedConditionalValidatorTypes { get; }
    }

    /// <summary>
    /// Schema generation options.
    /// </summary>
    public class SchemaGenerationOptions : ISchemaGenerationOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether property should be set to not nullable if MinLength is greater then zero.
        /// Default: false.
        /// </summary>
        public bool SetNotNullableIfMinLengthGreaterThenZero { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether schema generator should use AllOf for multiple rules (for example for multiple patterns).
        /// Default: true.
        /// </summary>
        public bool UseAllOffForMultipleRules { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether conditional rules are allowed to effect schema generation.
        /// </summary>
        public bool AllowConditionalRules { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether conditional validators are allowed to effect schema generation.
        /// </summary>
        public bool AllowConditionalValidators { get; set; } = false;

        /// <summary>
        /// Gets a list that contains the allowed conditional validator types.
        /// </summary>
        public List<Type> AllowedConditionalValidatorTypes { get; } = new List<Type>()
        {
            typeof(ILengthValidator),
            typeof(IRegularExpressionValidator),
            typeof(IComparisonValidator),
            typeof(IEmailValidator),
            typeof(IBetweenValidator),
        };

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