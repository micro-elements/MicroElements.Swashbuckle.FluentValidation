using System;
using MicroElements.OpenApi.FluentValidation;
using Microsoft.Extensions.Options;

namespace MicroElements.OpenApi.AspNetCore
{
    /// <summary>
    /// Fills <see cref="SchemaGenerationOptions"/> default values on PostConfigure action.
    /// </summary>
    public class FillDefaultValuesPostConfigureOptions : IPostConfigureOptions<SchemaGenerationOptions>
    {
        private readonly IServiceProvider _serviceProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="FillDefaultValuesPostConfigureOptions"/> class.
        /// </summary>
        /// <param name="serviceProvider">The source service provider.</param>
        public FillDefaultValuesPostConfigureOptions(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        /// <inheritdoc />
        public void PostConfigure(string name, SchemaGenerationOptions options)
        {
            options.FillDefaultValues(_serviceProvider);
        }
    }
}