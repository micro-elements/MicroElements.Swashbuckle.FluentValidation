using System;
using FluentValidation.Validators;

namespace MicroElements.Swashbuckle.FluentValidation
{
    /// <summary>
    /// FluentValidationRule.
    /// </summary>
    public class FluentValidationRule
    {
        /// <summary>
        /// Rule name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Predicate to match property validator.
        /// </summary>
        public Func<IPropertyValidator, bool> Matches { get; set; }

        /// <summary>
        /// Modify Swagger schema action.
        /// </summary>
        public Action<RuleContext> Apply { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="FluentValidationRule"/> class.
        /// </summary>
        /// <param name="name">Rule name.</param>
        public FluentValidationRule(string name)
        {
            Name = name;
        }
    }
}