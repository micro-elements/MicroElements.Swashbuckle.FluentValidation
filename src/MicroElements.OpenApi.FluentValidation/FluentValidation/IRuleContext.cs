using FluentValidation.Validators;

namespace MicroElements.OpenApi.FluentValidation
{
    /// <summary>
    /// RuleContext.
    /// </summary>
    public interface IRuleContext
    {
        /// <summary>
        /// Gets property name in schema.
        /// </summary>
        string PropertyKey { get; }

        /// <summary>
        /// Gets property validator for property in schema.
        /// </summary>
        IPropertyValidator PropertyValidator { get; }
    }

    /// <summary>
    /// Rule context with knowledge of schema type.
    /// </summary>
    /// <typeparam name="TSchema">Schema type.</typeparam>
    public interface IRuleContext<out TSchema> : IRuleContext
    {
        /// <summary>
        /// Gets OpenApi schema for model.
        /// </summary>
        public TSchema Schema { get; }

        /// <summary>
        /// Gets the property in schema.
        /// </summary>
        public TSchema Property { get; }
    }
}