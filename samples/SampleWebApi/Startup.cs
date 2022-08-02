﻿using System;
using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using FluentValidation.AspNetCore;
using MicroElements.Swashbuckle.FluentValidation;
using MicroElements.Swashbuckle.FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using SampleWebApi.DbModels;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;

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
            // HttpContextValidatorRegistry requires access to HttpContext
            services.AddHttpContextAccessor();

            services
                .AddControllers()
                .AddJsonOptions(options =>
                {
                    // Workaround for snake_case
                    // options.JsonSerializerOptions.PropertyNamingPolicy = new NewtonsoftJsonNamingPolicy(new SnakeCaseNamingStrategy());
                    // options.JsonSerializerOptions.DictionaryKeyPolicy = new NewtonsoftJsonNamingPolicy(new SnakeCaseNamingStrategy());
                })
                .AddNewtonsoftJson();

            // Register FV validators
            services.AddValidatorsFromAssemblyContaining<Startup>(lifetime: ServiceLifetime.Scoped);

            // Add FV to Asp.net
            services.AddFluentValidationAutoValidation();
            
            // Register all validators as IValidator?
            //var serviceDescriptors = services.Where(descriptor => descriptor.ServiceType.GetInterfaces().Contains(typeof(IValidator))).ToList();
            //serviceDescriptors.ForEach(descriptor => services.Add(ServiceDescriptor.Describe(typeof(IValidator), descriptor.ImplementationType, descriptor.Lifetime)));

            // One more way to set custom factory.
            //services = services.Replace(ServiceDescriptor.Scoped<IValidatorFactory, ScopedServiceProviderValidatorFactory>());

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo(){ Title = "My API", Version = "v1" });
                c.EnableAnnotations(enableAnnotationsForInheritance: true, enableAnnotationsForPolymorphism: true);

                // [Optional] Use native C#'s reference type nullable feature
                //c.SupportNonNullableReferenceTypes();
            });

            // [Optional] Add INameResolver (SystemTextJsonNameResolver will be registered by default)
            // services.AddSingleton<INameResolver, CustomNameResolver>();

            // Adds FluentValidationRules staff to Swagger
            services.AddFluentValidationRulesToSwagger();

            // [Optional] Configure generation options for your needs. Also can be done with services.Configure<SchemaGenerationOptions>
            // services.AddFluentValidationRulesToSwagger(configure: options =>
            // {
            //     options.SetNotNullableIfMinLengthGreaterThenZero = true;
            //     options.UseAllOffForMultipleRules = true;
            // });

            // Adds logging
            services.AddLogging(builder => builder.AddConsole().AddFilter(level => true));

            // Register database
            services.AddDbContext<BloggingDbContext>(
                options => options.UseInMemoryDatabase("validationDB"),
                ServiceLifetime.Scoped);

            //services.AddTransient<Func<BloggingDbContext>>(provider => provider.GetService<BloggingDbContext>);

            // Example: override or add ValidationRules
            //services.AddSingleton(new FluentValidationRule("Pattern")
            //{
            //    Matches = propertyValidator => propertyValidator is IRegularExpressionValidator,
            //    Apply = context =>
            //    {
            //        // your own implementation here!
            //        var regularExpressionValidator = (IRegularExpressionValidator)context.PropertyValidator;
            //        context.Schema.Properties[context.PropertyKey].Pattern = regularExpressionValidator.Expression;
            //    }
            //});
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app)
        {
            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });

            // Adds swagger
            app.UseSwagger();

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
            if (!bloggingContext.Metadata.Any())
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
