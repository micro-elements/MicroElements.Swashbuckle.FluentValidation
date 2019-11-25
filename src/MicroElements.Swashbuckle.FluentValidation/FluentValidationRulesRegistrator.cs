using MicroElements.Swashbuckle.FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Serialization;
using Swashbuckle.AspNetCore.SwaggerGen;

// ReSharper disable once CheckNamespace
namespace Swashbuckle.AspNetCore.Swagger
{
    /// <summary>
    /// Registration extensions.
    /// </summary>
    public static class FluentValidationRulesRegistrator
    {
        /// <summary>
        /// Adds fluent validation rules to swagger.
        /// </summary>
        /// <param name="options">Swagger options.</param>
        public static void AddFluentValidationRules(this SwaggerGenOptions options)
        {
            options.SchemaFilter<FluentValidationRules>();
            options.OperationFilter<FluentValidationOperationFilter>();
        }

        /// <summary>
        /// Adds fluent validation rules to swagger.
        /// </summary>
        /// <param name="options">Swagger options.</param>
        /// <param name="contractResolver">Contract resolver.</param>
        public static void AddFluentValidationRules(this SwaggerGenOptions options, IContractResolver contractResolver)
        {
            options.SchemaFilter<FluentValidationRules>(contractResolver);
            options.OperationFilter<FluentValidationOperationFilter>();
        }
    }
}