using System.Collections.Generic;

namespace MicroElements.Swashbuckle.FluentValidation
{
    /// <summary>
    /// <see cref="FluentValidationRule"/> provider.
    /// </summary>
    public interface IFluentValidationRulesProvider
    {
        /// <summary>
        /// Gets fluent validation  rules.
        /// Can be overriden by name.
        /// </summary>
        /// <returns><see cref="FluentValidationRule"/> enumeration.</returns>
        IEnumerable<FluentValidationRule> GetRules();
    }
}