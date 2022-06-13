using FluentValidation.AspNetCore;
using MicroElements.NSwag.FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSwag.Generation.Processors;

namespace SampleWebApi
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
// HttpContextServiceProviderValidatorFactory requires access to HttpContext
            services.AddHttpContextAccessor();

            services
                .AddControllers()

                // Adds fluent validators to Asp.net
                .AddFluentValidation(c =>
                {
                    c.RegisterValidatorsFromAssemblyContaining<Startup>();

                    // Optionally set validator factory if you have problems with scope resolve inside validators.
                    //c.ValidatorFactoryType = typeof(HttpContextServiceProviderValidatorFactory);
                });

            services.AddOpenApiDocument((settings, serviceProvider) =>
            {
                var fluentValidationSchemaProcessor = serviceProvider.CreateScope().ServiceProvider.GetService<FluentValidationSchemaProcessor>();

                // Add the fluent validations schema processor
                settings.SchemaProcessors.Add(fluentValidationSchemaProcessor);
                
                //settings.DocumentProcessors.Add(new FluentValidationDocumentProcessor());
            });

            // Add the FluentValidationSchemaProcessor as a scoped service
            services.AddScoped<FluentValidationSchemaProcessor>();
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
