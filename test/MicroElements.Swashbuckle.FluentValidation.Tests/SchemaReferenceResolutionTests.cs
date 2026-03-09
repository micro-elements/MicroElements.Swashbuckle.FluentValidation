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
        /// Verifies that GetProperty without repository returns null for $ref properties on OPENAPI_V2,
        /// but still works on non-OPENAPI_V2 where properties are always OpenApiSchema.
        /// </summary>
        [Fact]
        public void GetProperty_Without_Repository_Behavior()
        {
            // Arrange
            var schemaRepository = new SchemaRepository();
            var validator = new InlineValidator<SchemaGenerationTests.BigIntegerModel>();
            validator.RuleFor(x => x.Value).InclusiveBetween(new BigInteger(0), new BigInteger(100));

            var schemaGenerator = SchemaGenerator(validator);
            var referenceSchema = schemaGenerator.GenerateSchema(typeof(SchemaGenerationTests.BigIntegerModel), schemaRepository);
            var schema = schemaRepository.GetSchema(referenceSchema.GetRefId()!);

            // Act: GetProperty WITHOUT repository
            var property = OpenApiSchemaCompatibility.GetProperty(schema, "Value");

#if OPENAPI_V2
            // On OPENAPI_V2, BigInteger is rendered as $ref → GetProperty returns null without repository
            property.Should().BeNull("BigInteger property is OpenApiSchemaReference on OPENAPI_V2");
#else
            // On older versions, BigInteger is rendered inline → GetProperty works without repository
            property.Should().NotBeNull("BigInteger property is inline OpenApiSchema on non-OPENAPI_V2");
#endif
        }

    }
}
