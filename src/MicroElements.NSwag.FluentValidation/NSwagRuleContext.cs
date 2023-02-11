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
                return new SchemaProcessorContext(
                    Schema.Type,
                    Schema.Schema.Properties[PropertyKey],
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