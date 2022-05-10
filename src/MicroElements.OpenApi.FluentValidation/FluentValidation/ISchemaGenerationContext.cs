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
        
        IEnumerable<string> Properties { get; }

        /// <summary>
        /// Gets the validation rules to apply.
        /// </summary>
        IReadOnlyList<IFluentValidationRule> Rules { get; }

        /// <summary>
        /// Gets <see cref="ISchemaGenerationOptions"/> (constant user options).
        /// </summary>
        ISchemaGenerationOptions SchemaGenerationOptions { get; }

        /// <summary>
        /// Gets <see cref="ISchemaGenerationSettings"/> (runtime options and services).
        /// </summary>
        ISchemaGenerationSettings SchemaGenerationSettings { get; }
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

        IRuleContext<TSchema> Create(string schemaPropertyName, ValidationRuleInfo validationRuleInfo, IPropertyValidator propertyValidator);
    }
}