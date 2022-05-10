using System;
using System.Collections.Generic;
using FluentValidation.Validators;

namespace MicroElements.OpenApi.FluentValidation
{
    /// <summary>
    /// FluentValidationRule.
    /// </summary>
    public interface IFluentValidationRule
    {
        /// <summary>
        /// Gets the rule name.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets predicates that checks the validator is matches the rule.
        /// </summary>
        IReadOnlyCollection<Func<IPropertyValidator, bool>> Conditions { get; }
    }

    public interface IFluentValidationRule<in TSchema> : IFluentValidationRule
    {
        /// <summary>
        /// Gets the action that modifies OpenApi schema.
        /// </summary>
        public Action<IRuleContext<TSchema>>? Apply { get; }
    }
}