using MicroElements.Swashbuckle.FluentValidation;

namespace Swashbuckle.AspNetCore.SwaggerGen
{
    public static class FluentValidationRulesRegistrator
    {
        public static void AddFluentValidationRules(this SwaggerGenOptions options)
        {
            options.SchemaFilter<FluentValidationRules>();
        }
    }
}