using System;
using System.Collections.Generic;
using FluentValidation.Validators;

namespace MicroElements.OpenApi.FluentValidation
{
    public interface ISchemaGenerationContext
    {
        /// <summary>
        /// Gets the schema .net type.
        /// </summary>
        Type SchemaType { get; }

        /// <summary>
        /// Gets the schema properties.
        /// </summary>
        IEnumerable<string> Properties { get; }

        /// <summary>
        /// Gets the validation rules to apply.
        /// </summary>
        IReadOnlyList<IFluentValidationRule> Rules { get; }

        /// <summary>
        /// Gets <see cref="ISchemaGenerationOptions"/>.
        /// </summary>
        ISchemaGenerationOptions SchemaGenerationOptions { get; }
    }

    public interface ISchemaGenerationContext<TSchema> : ISchemaGenerationContext
    {
        /// <summary>
        /// Gets OpenApi schema.
        /// </summary>
        TSchema Schema { get; }

        /// <summary>
        /// Gets schema provider.
        /// </summary>
        ISchemaProvider<TSchema> SchemaProvider { get; }

        /// <summary>
        /// Gets the validation rules to apply.
        /// </summary>
        IReadOnlyList<IFluentValidationRule<TSchema>> Rules { get; }

        ISchemaGenerationContext<TSchema> With(TSchema schema);

        IRuleContext<TSchema> Create(string schemaPropertyName, ValidationRuleContext validationRuleContext, IPropertyValidator propertyValidator);
    }
}