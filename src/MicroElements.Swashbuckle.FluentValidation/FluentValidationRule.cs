// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using FluentValidation.Validators;
using MicroElements.OpenApi.FluentValidation;
using Microsoft.OpenApi.Models;

namespace MicroElements.Swashbuckle.FluentValidation
{
    /// <summary>
    /// FluentValidationRule.
    /// </summary>
    public class FluentValidationRule : IFluentValidationRule<OpenApiSchema>
    {
        /// <summary>
        /// Gets rule name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets predicates that checks validator is matches rule.
        /// </summary>
        public IReadOnlyCollection<Func<IPropertyValidator, bool>> Conditions { get; }

        /// <summary>
        /// Gets action that modifies swagger schema.
        /// </summary>
        public Action<IRuleContext<OpenApiSchema>>? Apply { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="FluentValidationRule"/> class.
        /// </summary>
        /// <param name="name">Rule name.</param>
        /// <param name="matches">Validator predicates.</param>
        /// <param name="apply">Apply rule to schema action.</param>
        public FluentValidationRule(
            string name,
            IReadOnlyCollection<Func<IPropertyValidator, bool>>? matches = null,
            Action<IRuleContext<OpenApiSchema>>? apply = null)
        {
            Name = name;
            Conditions = matches ?? Array.Empty<Func<IPropertyValidator, bool>>();
            Apply = apply;
        }
    }

    /// <summary>
    /// <see cref="FluentValidationRule"/> extensions.
    /// </summary>
    public static class FluentValidationRuleExtensions
    {
        /// <summary>
        /// Adds match predicate.
        /// </summary>
        /// <param name="rule">Source rule.</param>
        /// <param name="validatorPredicate">Validator selector.</param>
        /// <returns>New rule instance.</returns>
        public static FluentValidationRule WithCondition(this FluentValidationRule rule, Func<IPropertyValidator, bool> validatorPredicate)
        {
            var matches = rule.Conditions.Append(validatorPredicate).ToArray();
            return new FluentValidationRule(rule.Name, matches, rule.Apply);
        }

        /// <summary>
        /// Sets <see cref="Apply"/> action.
        /// </summary>
        /// <param name="rule">Source rule.</param>
        /// <param name="applyAction">New apply action.</param>
        /// <returns>New rule instance.</returns>
        public static FluentValidationRule WithApply(this FluentValidationRule rule, Action<IRuleContext<OpenApiSchema>> applyAction)
        {
            return new FluentValidationRule(rule.Name, rule.Conditions, applyAction);
        }
    }
}