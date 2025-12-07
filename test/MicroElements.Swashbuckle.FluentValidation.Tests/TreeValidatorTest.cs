using System.Collections.Generic;
using FluentAssertions;
using FluentValidation;
#if OPENAPI_V2
using Microsoft.OpenApi;
#else
using Microsoft.OpenApi.Models;
#endif
using Swashbuckle.AspNetCore.SwaggerGen;
using Xunit;

namespace MicroElements.Swashbuckle.FluentValidation.Tests
{
    public class TreeValidatorTest : UnitTestBase
    {
        /// <summary>
        /// A simple recursive data structure
        /// </summary>
        public class Node
        {
            public string Id { get; set; }
            public List<Node> Nodes = new();
        }

        public class NodeValidator : AbstractValidator<Node>
        {
            public NodeValidator()
            {
                // Recursive validation example I found on this closed "issue".  Used it as the base for this test.
                // https://github.com/FluentValidation/FluentValidation/issues/1568
                RuleForEach(r => r.Nodes).SetValidator(this);
                RuleFor(r => r.Id).NotEmpty();
            }
        }

        [Fact]
        public void recursive_validator_works()
        {
            // *********************************
            // FluentValidation swagger behavior
            // *********************************

            var schemaRepository = new SchemaRepository();

            var referenceSchema = SchemaGenerator(new NodeValidator())
                .GenerateSchema(typeof(Node), schemaRepository);

            // if we have gotten this far, huzza, no stack overflow.

            // MicroElements schema validation should work normally on other non-recursive properties
            var schema = schemaRepository.GetSchema(referenceSchema.GetRefId()!);
            var idProperty = schema.GetProperty(nameof(Node.Id))!;
            idProperty.GetTypeString().Should().Be("string");
            idProperty.MinLength.Should().Be(1);
        }
    }
}