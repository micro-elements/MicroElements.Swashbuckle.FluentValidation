using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;
using Xunit;

namespace MicroElements.Swashbuckle.FluentValidation.Tests
{
    public class UnitTest1
    {
        [Fact]
        public void Test1()
        {
            FluentValidationRules fluentValidationRules = new FluentValidationRules();
            //fluentValidationRules.Apply(new Schema(), new SchemaFilterContext());
        }

        [Fact]
        public void Apply_DelegatesToSpecifiedFilter_IfTypeDecoratedWithFilterAttribute()
        {
            IEnumerable<OpenApiSchema> schemas;
            var filterContexts = new[]
            {
                FilterContextFor(typeof(SwaggerAnnotatedClass)),
            };

            schemas = filterContexts.Select(c =>
            {
                var schema = new OpenApiSchema();
                Subject().Apply(schema, c);
                return schema;
            });

            IServiceCollection services = new ServiceCollection();
            SwaggerGenServiceCollectionExtensions.AddSwaggerGen(services);

            //Assert.All(schemas, s => Assert.NotEmpty(s.Extensions));
        }

        private SchemaFilterContext FilterContextFor(Type type)
        {
            return new SchemaFilterContext(type, null, null, null);
        }

        private FluentValidationRules Subject()
        {
            return new FluentValidationRules(null);
        }
    }

    public class SwaggerAnnotatedClass
    {
        public string Property { get; set; }
    }
}
