using System;
using System.Reflection;

namespace MicroElements.OpenApi.FluentValidation
{
    /// <summary>
    /// Reflection context for <see cref="RuleContext"/>.
    /// </summary>
    public class ReflectionContext
    {
        /// <summary>
        /// Gets the type (schema type).
        /// </summary>
        public Type? Type { get; }

        /// <summary>
        /// Gets optional PropertyInfo.
        /// </summary>
        public MemberInfo? PropertyInfo { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ReflectionContext"/> class.
        /// </summary>
        /// <param name="type">Schema type.</param>
        /// <param name="propertyInfo">Optional PropertyInfo.</param>
        public ReflectionContext(
            Type? type = null,
            MemberInfo? propertyInfo = null)
        {
            Type = type;
            PropertyInfo = propertyInfo;
        }
    }
}