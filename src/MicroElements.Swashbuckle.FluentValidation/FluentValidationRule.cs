// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using FluentValidation.Validators;

namespace MicroElements.Swashbuckle.FluentValidation
{
    /// <summary>
    /// FluentValidationRule.
    /// </summary>
    public class FluentValidationRule
    {
        /// <summary>
        /// Gets rule name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets predicates that checks validator is matches rule.
        /// </summary>
        public IReadOnlyCollection<Func<IPropertyValidator, bool>> Matches { get; }

        /// <summary>
        /// Gets action that modifies swagger schema.
        /// </summary>
        public Action<RuleContext> Apply { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="FluentValidationRule"/> class.
        /// </summary>
        /// <param name="name">Rule name.</param>
        /// <param name="matches">Validator predicates.</param>
        /// <param name="apply">Apply rule to schema action.</param>
        public FluentValidationRule(string name, IReadOnlyCollection<Func<IPropertyValidator, bool>>? matches = null, Action<RuleContext>? apply = null)
        {
            Name = name;
            Matches = matches ?? Array.Empty<Func<IPropertyValidator, bool>>();
            Apply = apply ?? (context => { });
        }

        /// <summary>
        /// Checks that validator is matches rule.
        /// </summary>
        /// <param name="validator">Validator.</param>
        /// <returns>True if validator matches rule.</returns>
        public bool IsMatches(IPropertyValidator validator)
        {
            foreach (var match in Matches)
            {
                if (!match(validator))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Adds condition that validator has no conditions.
        /// Conditional validators will not change schema because conditions are processes in runtime.
        /// </summary>
        /// <returns>New rule instance.</returns>
        public FluentValidationRule MatchesValidatorWithNoCondition()
        {
            var matches = Matches.Prepend(validator => validator.HasNoCondition()).ToArray();
            return new FluentValidationRule(Name, matches, Apply);
        }

        /// <summary>
        /// Adds match predicate.
        /// </summary>
        /// <param name="validatorSelector">Validator selector.</param>
        /// <returns>New rule instance.</returns>
        public FluentValidationRule MatchesValidator(Func<IPropertyValidator, bool> validatorSelector)
        {
            var matches = Matches.Append(validatorSelector).ToArray();
            return new FluentValidationRule(Name, matches, Apply);
        }

        /// <summary>
        /// Sets <see cref="Apply"/> action.
        /// </summary>
        /// <param name="applyAction">New apply action.</param>
        /// <returns>New rule instance.</returns>
        public FluentValidationRule WithApply(Action<RuleContext> applyAction)
        {
            return new FluentValidationRule(Name, Matches, applyAction);
        }
    }
}