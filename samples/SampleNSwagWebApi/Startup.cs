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
                var scopedProvider = serviceProvider.CreateScope().ServiceProvider;

                // Add the fluent validations schema processor
                settings.SchemaProcessors.Add(scopedProvider.GetService<FluentValidationSchemaProcessor>());

                settings.OperationProcessors.Add(scopedProvider.GetService<NSwagOperationProcessor>());
            });

            // Register FV validators
            services.AddValidatorsFromAssemblyContaining<Startup>(lifetime: ServiceLifetime.Scoped);

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
            app.UseSwaggerUi3(); // serve Swagger UI
        }
    }
}
