// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Numerics;
using FluentAssertions;
using FluentValidation;
using MicroElements.OpenApi;
#if OPENAPI_V2
using Microsoft.OpenApi;
#else
using Microsoft.OpenApi.Models;
#endif
using Swashbuckle.AspNetCore.SwaggerGen;
using Xunit;

namespace MicroElements.Swashbuckle.FluentValidation.Tests
{
    /// <summary>
    /// Tests for OpenApiSchemaReference resolution in GetProperty/TryGetProperty.
    /// Issue #146: BigInteger (and other $ref types) should have validation rules applied on net10.0.
    /// </summary>
    public class SchemaReferenceResolutionTests : UnitTestBase
    {
        /// <summary>
        /// Verifies that OpenApiSchemaCompatibility.GetProperty resolves OpenApiSchemaReference
        /// through SchemaRepository when the property is a $ref.
        /// </summary>
        [Fact]
        public void GetProperty_Should_Resolve_SchemaReference_Via_Repository()
        {
            // Arrange: generate schema for a type that contains BigInteger
            var schemaRepository = new SchemaRepository();
            var validator = new InlineValidator<SchemaGenerationTests.BigIntegerModel>();
            validator.RuleFor(x => x.Value).InclusiveBetween(new BigInteger(0), new BigInteger(100));

            var schemaGenerator = SchemaGenerator(validator);
            var referenceSchema = schemaGenerator.GenerateSchema(typeof(SchemaGenerationTests.BigIntegerModel), schemaRepository);
            var schema = schemaRepository.GetSchema(referenceSchema.GetRefId()!);

            // Act: GetProperty WITH repository should resolve even if property is OpenApiSchemaReference
            var property = OpenApiSchemaCompatibility.GetProperty(schema, "Value", schemaRepository);

            // Assert
            property.Should().NotBeNull("GetProperty with repository should resolve $ref properties");
        }

        /// <summary>
        /// Verifies that OpenApiSchemaCompatibility.TryGetProperty resolves OpenApiSchemaReference
        /// through SchemaRepository when the property is a $ref.
        /// </summary>
        [Fact]
        public void TryGetProperty_Should_Resolve_SchemaReference_Via_Repository()
        {
            // Arrange
            var schemaRepository = new SchemaRepository();
            var validator = new InlineValidator<SchemaGenerationTests.BigIntegerModel>();
            validator.RuleFor(x => x.Value).GreaterThan(new BigInteger(5));

            var schemaGenerator = SchemaGenerator(validator);
            var referenceSchema = schemaGenerator.GenerateSchema(typeof(SchemaGenerationTests.BigIntegerModel), schemaRepository);
            var schema = schemaRepository.GetSchema(referenceSchema.GetRefId()!);

            // Act
            var found = OpenApiSchemaCompatibility.TryGetProperty(schema, "Value", out var property, schemaRepository);

            // Assert
            found.Should().BeTrue("TryGetProperty with repository should resolve $ref properties");
            property.Should().NotBeNull();
        }

        /// <summary>
        /// Verifies that GetProperty works after the schema filter has resolved $ref properties.
        /// On OPENAPI_V2, the schema filter replaces $ref entries with concrete schemas during processing,
        /// so GetProperty works even without a repository after the filter has run.
        /// </summary>
        [Fact]
        public void GetProperty_Without_Repository_After_Filter_Processing()
        {
            // Arrange
            var schemaRepository = new SchemaRepository();
            var validator = new InlineValidator<SchemaGenerationTests.BigIntegerModel>();
            validator.RuleFor(x => x.Value).InclusiveBetween(new BigInteger(0), new BigInteger(100));

            var schemaGenerator = SchemaGenerator(validator);
            var referenceSchema = schemaGenerator.GenerateSchema(typeof(SchemaGenerationTests.BigIntegerModel), schemaRepository);
            var schema = schemaRepository.GetSchema(referenceSchema.GetRefId()!);

            // Act: GetProperty WITHOUT repository — after the schema filter has already processed the schema,
            // the $ref is replaced with a concrete OpenApiSchema, so it works without repository.
            var property = OpenApiSchemaCompatibility.GetProperty(schema, "Value");

            // After filter processing, the property should be available regardless of OPENAPI version
            property.Should().NotBeNull("property should be available after schema filter processing");
        }

        /// <summary>
        /// Two models sharing the same BigInteger $ref type but with different validator ranges
        /// should get independent min/max constraints (no shared schema mutation).
        /// </summary>
        public class ModelA
        {
            public BigInteger Amount { get; set; }
        }

        public class ModelB
        {
            public BigInteger Amount { get; set; }
        }

        public class ModelAValidator : AbstractValidator<ModelA>
        {
            public ModelAValidator()
            {
                RuleFor(x => x.Amount).InclusiveBetween(new BigInteger(10), new BigInteger(100));
            }
        }

        public class ModelBValidator : AbstractValidator<ModelB>
        {
            public ModelBValidator()
            {
                RuleFor(x => x.Amount).InclusiveBetween(new BigInteger(500), new BigInteger(1000));
            }
        }

        [Fact]
        public void SharedRef_Should_Not_Corrupt_Between_Models()
        {
            // Arrange: both models share BigInteger which may be a $ref in the SchemaRepository
            var schemaRepository = new SchemaRepository();
            var schemaGenerator = SchemaGenerator(new ModelAValidator(), new ModelBValidator());

            // Act: generate schemas for both models into the same repository
            var refA = schemaGenerator.GenerateSchema(typeof(ModelA), schemaRepository);
            var schemaA = schemaRepository.GetSchema(refA.GetRefId()!);

            var refB = schemaGenerator.GenerateSchema(typeof(ModelB), schemaRepository);
            var schemaB = schemaRepository.GetSchema(refB.GetRefId()!);

            // Assert: each model should have its own min/max constraints
            // Use OpenApiSchemaCompatibility.GetProperty which handles $ref resolution on OPENAPI_V2
            var propertyA = OpenApiSchemaCompatibility.GetProperty(schemaA, "Amount", schemaRepository);
            var propertyB = OpenApiSchemaCompatibility.GetProperty(schemaB, "Amount", schemaRepository);

            propertyA.Should().NotBeNull("ModelA should have Amount property");
            propertyB.Should().NotBeNull("ModelB should have Amount property");

            var minA = OpenApiSchemaCompatibility.GetMinimum(propertyA!);
            var maxA = OpenApiSchemaCompatibility.GetMaximum(propertyA!);
            var minB = OpenApiSchemaCompatibility.GetMinimum(propertyB!);
            var maxB = OpenApiSchemaCompatibility.GetMaximum(propertyB!);

            minA.Should().Be(10, "ModelA minimum should be 10");
            maxA.Should().Be(100, "ModelA maximum should be 100");
            minB.Should().Be(500, "ModelB minimum should be 500");
            maxB.Should().Be(1000, "ModelB maximum should be 1000");
        }
    }
}
