using FluentValidation;
using FluentValidation.AspNetCore;
using MicroElements.NSwag.FluentValidation;
using MicroElements.NSwag.FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace SampleNSwagWebApi
{
    public class Startup
    {
        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // HttpContextValidatorRegistry requires access to HttpContext
            services.AddHttpContextAccessor();

            services
                .AddControllers()
                // Adds fluent validators to Asp.net
                .AddFluentValidation(c =>
                {
                    c.RegisterValidatorsFromAssemblyContaining<Startup>(includeInternalTypes: true);
                });

            services.AddOpenApiDocument((settings, serviceProvider) =>
            {
                var fluentValidationSchemaProcessor = serviceProvider.CreateScope().ServiceProvider.GetService<FluentValidationSchemaProcessor>();

                // Add the fluent validations schema processor
                settings.SchemaSettings.SchemaProcessors.Add(fluentValidationSchemaProcessor);
            });

            // Register FV validators
            services.AddValidatorsFromAssemblyContaining<Startup>();

            // Adds FV rules to NSwag
            services.AddFluentValidationRulesToSwagger();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app)
        {
            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });

            app.UseOpenApi(); // serve OpenAPI/Swagger documents
            app.UseSwaggerUi(); // serve Swagger UI
        }
    }
}
