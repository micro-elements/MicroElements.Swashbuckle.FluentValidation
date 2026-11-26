// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentValidation.Validators;
using MicroElements.OpenApi.FluentValidation;
using NJsonSchema.Generation;

namespace MicroElements.NSwag.FluentValidation
{
    /// <summary>
    /// RuleContext.
    /// </summary>
    public class NSwagRuleContext : IRuleContext<SchemaProcessorContext>
    {
        internal bool IsValueTypeProperty { get; init; }

        /// <inheritdoc/>
        public string PropertyKey { get; }

        /// <inheritdoc/>
        public IPropertyValidator PropertyValidator { get; }

        /// <inheritdoc/>
        public SchemaProcessorContext Schema { get; }

        /// <inheritdoc />
        public SchemaProcessorContext Property
        {
            get
            {
                // Use TryGetValue to safely handle missing properties (e.g., enum or nested class references)
                // Issue #176: https://github.com/micro-elements/MicroElements.Swashbuckle.FluentValidation/issues/176
                if (!Schema.Schema.Properties.TryGetValue(PropertyKey, out var property))
                {
                    // Property is a reference - return schema with empty properties to skip validation
                    return new SchemaProcessorContext(
                        Schema.ContextualType,
                        new NJsonSchema.JsonSchema(),
                        Schema.Resolver,
                        Schema.Generator,
                        Schema.Settings);
                }

                return new SchemaProcessorContext(
                    Schema.ContextualType,
                    property,
                    Schema.Resolver,
                    Schema.Generator,
                    Schema.Settings);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NSwagRuleContext"/> class.
        /// </summary>
        /// <param name="schema">SchemaProcessorContext.</param>
        /// <param name="propertyKey">Property name.</param>
        /// <param name="propertyValidator">Property validator.</param>
        public NSwagRuleContext(
            SchemaProcessorContext schema,
            string propertyKey,
            IPropertyValidator propertyValidator)
        {
            Schema = schema;
            PropertyKey = propertyKey;
            PropertyValidator = propertyValidator;
        }
    }
}