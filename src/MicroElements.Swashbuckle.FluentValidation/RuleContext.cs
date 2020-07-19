using FluentValidation.Validators;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MicroElements.Swashbuckle.FluentValidation
{
    /// <summary>
    /// RuleContext.
    /// </summary>
    public class RuleContext
    {
        /// <summary>
        /// Swagger schema.
        /// </summary>
        public OpenApiSchema Schema { get; }

        /// <summary>
        /// SchemaFilterContext.
        /// </summary>
        public SchemaFilterContext SchemaFilterContext { get; }

        /// <summary>
        /// Property name.
        /// </summary>
        public string PropertyKey { get; }

        /// <summary>
        /// Property validator.
        /// </summary>
        public IPropertyValidator PropertyValidator { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="RuleContext"/> class.
        /// </summary>
        /// <param name="schema">Swagger schema.</param>
        /// <param name="schemaFilterContext">SchemaFilterContext.</param>
        /// <param name="propertyKey">Property name.</param>
        /// <param name="propertyValidator">Property validator.</param>
        public RuleContext(OpenApiSchema schema, SchemaFilterContext schemaFilterContext, string propertyKey, IPropertyValidator propertyValidator)
        {
            Schema = schema;
            SchemaFilterContext = schemaFilterContext;
            PropertyKey = propertyKey;
            PropertyValidator = propertyValidator;
        }
    }
}