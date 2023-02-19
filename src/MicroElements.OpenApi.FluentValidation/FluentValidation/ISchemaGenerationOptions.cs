// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using FluentValidation;
using FluentValidation.Internal;
using MicroElements.OpenApi.Core;

namespace MicroElements.OpenApi.FluentValidation
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
        bool UseAllOfForMultipleRules { get; }

        /// <summary>
        /// Gets <see cref="INameResolver"/>.
        /// </summary>
        INameResolver? NameResolver { get; }

        /// <summary>
        /// Gets schemaId by type.
        /// </summary>
        Func<Type, string>? SchemaIdSelector { get; }

        /// <summary>
        /// Gets the validator search strategy.
        /// Default: OneForType. (One model -> One validator).
        /// </summary>
        ValidatorSearchSettings ValidatorSearch { get; }

        /// <summary>
        /// Gets validator filter.
        /// </summary>
        ICondition<ValidatorContext>? ValidatorFilter { get; }

        /// <summary>
        /// Gets <see cref="IValidationRule"/> filter.
        /// </summary>
        ICondition<ValidationRuleContext>? RuleFilter { get; }

        /// <summary>
        /// Gets <see cref="IRuleComponent"/> filter.
        /// </summary>
        ICondition<RuleComponentContext>? RuleComponentFilter { get; }
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
        public bool UseAllOfForMultipleRules { get; set; } = true;

        /// <summary>
        /// Gets or sets the validator search strategy.
        /// Default: <see cref="ValidatorSearchSettings.Default"/>.
        /// </summary>
        public ValidatorSearchSettings ValidatorSearch { get; set; } = ValidatorSearchSettings.Default;

        /// <inheritdoc />
        public INameResolver? NameResolver { get; set; }

        /// <inheritdoc />
        public Func<Type, string>? SchemaIdSelector { get; set; }

        /// <inheritdoc />
        public ICondition<ValidatorContext>? ValidatorFilter { get; set; }

        /// <inheritdoc />
        public ICondition<ValidationRuleContext>? RuleFilter { get; set; }

        /// <inheritdoc />
        public ICondition<RuleComponentContext>? RuleComponentFilter { get; set; }

        /// <summary>
        /// Sets values that compatible with FluentValidation.
        /// </summary>
        /// <returns>The same options.</returns>
        public SchemaGenerationOptions SetFluentValidationCompatibility()
        {
            SetNotNullableIfMinLengthGreaterThenZero = false;
            ValidatorSearch = ValidatorSearch with { IsOneValidatorForType = true };

            return this;
        }
    }

    /// <summary>
    /// Search settings.
    /// </summary>
    public record ValidatorSearchSettings
    {
        /// <summary>
        /// Gets default validator search settings.
        /// </summary>
        public static ValidatorSearchSettings Default { get; } = new() { IsOneValidatorForType = true, SearchBaseTypeValidators = true };

        /// <summary>
        /// Gets a value indicating whether the model type can have only one validator (default in FluentValidation).
        /// </summary>
        public bool IsOneValidatorForType { get; init; }

        /// <summary>
        /// Gets a value indicating whether the search should use base type validators.
        /// </summary>
        public bool SearchBaseTypeValidators { get; init; }
    }

    /// <summary>
    /// Type context for filters.
    /// </summary>
    /// <param name="TypeToValidate">Type to validate.</param>
    /// <param name="SchemaGenerationOptions">Schema generation options.</param>
    public record TypeContext(Type TypeToValidate, ISchemaGenerationOptions SchemaGenerationOptions);

    /// <summary>
    /// Context for validation filter.
    /// </summary>
    /// <param name="TypeContext">Type context.</param>
    /// <param name="Validator">Validator.</param>
    public record ValidatorContext(TypeContext TypeContext, IValidator Validator) :
        TypeContext(TypeContext.TypeToValidate, TypeContext.SchemaGenerationOptions);

    /// <summary>
    /// Context for <see cref="IValidationRule"/> filters.
    /// </summary>
    /// <param name="ValidatorContext">Validator context.</param>
    /// <param name="ValidationRule">Validation rule.</param>
    public record ValidationRuleContext(ValidatorContext ValidatorContext, IValidationRule ValidationRule)
        : ValidatorContext(ValidatorContext.TypeContext, ValidatorContext.Validator);

    /// <summary>
    /// Context for <see cref="IRuleComponent"/> filters.
    /// </summary>
    /// <param name="ValidatorContext">Validator context.</param>
    /// <param name="RuleComponent">Rule component.</param>
    public record RuleComponentContext(ValidatorContext ValidatorContext, IRuleComponent RuleComponent)
        : ValidatorContext(ValidatorContext.TypeContext, ValidatorContext.Validator);
}