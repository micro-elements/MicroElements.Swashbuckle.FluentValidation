using System.Linq;
using FluentValidation;
using FluentValidation.AspNetCore;
using MicroElements.Swashbuckle.FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using SampleWebApi.DbModels;
using Swashbuckle.AspNetCore.Swagger;

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
            services
                .AddControllers()
                // Adds fluent validators to Asp.net
                .AddFluentValidation(c =>
                {
                    c.RegisterValidatorsFromAssemblyContaining<Startup>();
                    // Optionally set validator factory if you have problems with scope resolve inside validators.
                    // c.ValidatorFactoryType = typeof(CustomValidatorFactory);
                });

            // Register all validators as IValidator?
            var serviceDescriptors = services.Where(descriptor => descriptor.ServiceType.GetInterfaces().Contains(typeof(IValidator))).ToList();
            serviceDescriptors.ForEach(descriptor => services.Add(ServiceDescriptor.Transient(typeof(IValidator), descriptor.ImplementationType)));

            // One more way to set custom factory.
            //services = services.Replace(ServiceDescriptor.Scoped<IValidatorFactory, ScopedServiceProviderValidatorFactory>());

            //IOptions<SwaggerGeneratorOptions>
            //services.AddO
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo(){ Title = "My API", Version = "v1" });
                // Adds fluent validation rules to swagger
                c.AddFluentValidationRules();
            });

            // Adds logging
            services.AddLogging(builder => builder.AddConsole().AddFilter(level => true));

            // Register database
            services.AddDbContext<BloggingDbContext>(
                options => options.UseInMemoryDatabase("validationDB"),
                ServiceLifetime.Scoped);

            //services.AddTransient<Func<BloggingDbContext>>(provider => provider.GetService<BloggingDbContext>);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            app
                //.UseMvc()
                // Adds swagger
                //.UseSwagger()

                // Use scoped swagger if you have problems with scoped services in validators
                .UseScopedSwagger();
            ;

            // Adds swagger UI
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1");
            });

            // Seed database (NOT PRODUCTION CODE)
            TestDatabaseSeed(app);
        }

        private static void TestDatabaseSeed(IApplicationBuilder app)
        {
            var bloggingContext = app.ApplicationServices.CreateScope().ServiceProvider.GetService<BloggingDbContext>();
            if (!EnumerableExtensions.Any(bloggingContext.Metadata))
            {
                // Example of defining rules dynamically from database.
                bloggingContext.Metadata.Add(new ValidationMetadata
                {
                    ValidationMetadataId = 1,
                    TypeName = typeof(Blog).Name,
                    PropertyName = nameof(Blog.Author),
                    IsRequired = true,
                    MaxLength = 7
                });
                bloggingContext.SaveChanges();
            }
        }
    }
}
