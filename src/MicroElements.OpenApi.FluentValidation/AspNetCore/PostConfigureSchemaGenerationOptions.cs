using System;
using MicroElements.OpenApi.FluentValidation;
using Microsoft.Extensions.Options;

namespace MicroElements.OpenApi.AspNetCore
{
    public class PostConfigureSchemaGenerationOptions : IPostConfigureOptions<SchemaGenerationOptions>
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IServicesContext _servicesContext;

        public PostConfigureSchemaGenerationOptions(IServiceProvider serviceProvider, IServicesContext servicesContext)
        {
            _serviceProvider = serviceProvider;
            _servicesContext = servicesContext;
        }

        /// <inheritdoc />
        public void PostConfigure(string name, SchemaGenerationOptions options)
        {
            options.FillDefaultValues(_serviceProvider);
        }
    }
}