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

        /// <summary>
        /// Issue #198: SetValidator with nested object type should preserve $ref in parent schema.
        /// https://github.com/micro-elements/MicroElements.Swashbuckle.FluentValidation/issues/198
        /// </summary>
        public class PersonModel
        {
            public AddressModel Address { get; set; }
        }

        public class AddressModel
        {
            public string Street { get; set; }
        }

        public class AddressModelValidator : AbstractValidator<AddressModel>
        {
            public AddressModelValidator()
            {
                RuleFor(x => x.Street).NotEmpty();
            }
        }

        public class PersonModelValidator : AbstractValidator<PersonModel>
        {
            public PersonModelValidator()
            {
                RuleFor(x => x.Address)
                    .NotEmpty()
                    .SetValidator(new AddressModelValidator());
            }
        }

        [Fact]
        public void SetValidator_Should_Preserve_Ref_For_Nested_Object()
        {
            // Arrange
            var schemaRepository = new SchemaRepository();
            var schemaGenerator = SchemaGenerator(new PersonModelValidator(), new AddressModelValidator());

            // Act
            var referenceSchema = schemaGenerator.GenerateSchema(typeof(PersonModel), schemaRepository);
            var personSchema = schemaRepository.GetSchema(referenceSchema.GetRefId()!);

            // Assert: Person schema should have "Address" in required
            personSchema.Required.Should().Contain("Address");

            // Assert: Address component schema should exist and have street constraints
            schemaRepository.Schemas.Should().ContainKey("AddressModel");

            // Assert: Person.properties["Address"] should remain a $ref, not an inline copy
            var addressProp = personSchema.Properties["Address"];
#if OPENAPI_V2
            addressProp.Should().BeOfType<OpenApiSchemaReference>(
                "Person.properties['address'] should be a $ref, not an inline copy of the Address schema");
#else
            addressProp.Reference.Should().NotBeNull(
                "Person.properties['address'] should be a $ref, not an inline copy of the Address schema");
#endif
        }

        /// <summary>
        /// Issue #198 follow-up: when a $ref property IS modified by a validation rule
        /// (e.g., Email sets Format), the $ref should NOT be restored — the inline copy
        /// with constraints must be kept.
        /// </summary>
        public class ModelWithEmail
        {
            public string ContactEmail { get; set; }
        }

        public class ModelWithEmailValidator : AbstractValidator<ModelWithEmail>
        {
            public ModelWithEmailValidator()
            {
                RuleFor(x => x.ContactEmail).NotEmpty().EmailAddress();
            }
        }

        [Fact]
        public void Ref_Property_With_Constraint_Should_Not_Be_Restored()
        {
            // Arrange
            var schemaRepository = new SchemaRepository();
            var schemaGenerator = SchemaGenerator(new ModelWithEmailValidator());

            // Act
            var referenceSchema = schemaGenerator.GenerateSchema(typeof(ModelWithEmail), schemaRepository);
            var schema = schemaRepository.GetSchema(referenceSchema.GetRefId()!);

            // Assert: ContactEmail should have format=email applied (not discarded by ref restore)
            var emailProp = schema.GetProperty("ContactEmail")!;
            emailProp.Format.Should().Be("email",
                "Email rule should set Format on the property, and it must not be discarded by $ref restoration");
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

        /// <summary>
        /// Issue #198 (comment 4601720562 by jrgcubano): nested object with a child validator
        /// that adds MULTIPLE constraints (Email format + MaximumLength) should still keep the
        /// parent property as a $ref, and the child component schema must NOT become an orphan.
        /// https://github.com/micro-elements/MicroElements.Swashbuckle.FluentValidation/issues/198#issuecomment-4601720562
        /// </summary>
        public class CreateUserRequest
        {
            public CreateUserParams User { get; set; }
        }

        public class CreateUserParams
        {
            public string Email { get; set; }

            public string Name { get; set; }
        }

        public class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
        {
            public CreateUserRequestValidator()
            {
                RuleFor(a => a.User).NotEmpty().SetValidator(new CreateUserParamsValidator());
            }
        }

        public class CreateUserParamsValidator : AbstractValidator<CreateUserParams>
        {
            public CreateUserParamsValidator()
            {
                RuleFor(a => a.Email).NotEmpty().EmailAddress();
                RuleFor(a => a.Name).NotEmpty().MaximumLength(510);
            }
        }

        [Fact]
        public void SetValidator_With_MultiConstraint_Child_Should_Preserve_Ref()
        {
            // Arrange
            var schemaRepository = new SchemaRepository();
            var schemaGenerator = SchemaGenerator(new CreateUserRequestValidator(), new CreateUserParamsValidator());

            // Act
            var referenceSchema = schemaGenerator.GenerateSchema(typeof(CreateUserRequest), schemaRepository);
            var requestSchema = schemaRepository.GetSchema(referenceSchema.GetRefId()!);

            // Assert: CreateUserRequest schema should have "User" in required
            requestSchema.Required.Should().Contain("User");

            // Assert: CreateUserParams component schema should exist
            schemaRepository.Schemas.Should().ContainKey("CreateUserParams");

            // Assert: CreateUserRequest.properties["User"] should remain a $ref, not an inline copy
            var userProp = requestSchema.Properties["User"];
#if OPENAPI_V2
            userProp.Should().BeOfType<OpenApiSchemaReference>(
                "CreateUserRequest.properties['user'] should be a $ref to CreateUserParams, not an inline copy (Issue #198 comment)");
#else
            userProp.Reference.Should().NotBeNull(
                "CreateUserRequest.properties['user'] should be a $ref to CreateUserParams, not an inline copy (Issue #198 comment)");
#endif

            // Assert: the child component schema keeps ALL its constraints
            var paramsSchema = schemaRepository.Schemas["CreateUserParams"] as OpenApiSchema;
            paramsSchema.Should().NotBeNull();
            OpenApiSchemaCompatibility.GetProperty(paramsSchema!, "Email", schemaRepository)!.Format.Should().Be("email");
            OpenApiSchemaCompatibility.GetProperty(paramsSchema!, "Name", schemaRepository)!.MaxLength.Should().Be(510);

            // Assert: the required set lives on the child component (this is exactly what the orphan bug dropped)
            paramsSchema!.Required.Should().Contain("Email").And.Contain("Name");
        }

        /// <summary>
        /// Issue #198 (comment 4601720562): a nested object whose constraints are defined inline via
        /// ChildRules (so the child type has NO standalone validator) must still keep the parent property
        /// as a $ref, and the child component schema must NOT become an orphan.
        /// Before the fix, the inline child component gained its Required only after the parent's inline
        /// snapshot was taken, so the stale Required diverged and defeated ref restoration.
        /// https://github.com/micro-elements/MicroElements.Swashbuckle.FluentValidation/issues/198#issuecomment-4601720562
        /// </summary>
        public class ChildRulesRequest
        {
            public ChildRulesParams User { get; set; }
        }

        public class ChildRulesParams
        {
            public string Email { get; set; }

            public string Name { get; set; }
        }

        // NOTE: there is intentionally NO ChildRulesParamsValidator — the child constraints are inline.
        public class ChildRulesRequestValidator : AbstractValidator<ChildRulesRequest>
        {
            public ChildRulesRequestValidator()
            {
                RuleFor(a => a.User)
                    .NotEmpty()
                    .ChildRules(u =>
                    {
                        u.RuleFor(c => c.Email).NotEmpty().EmailAddress();
                        u.RuleFor(c => c.Name).NotEmpty().MaximumLength(510);
                    });
            }
        }

        [Fact]
        public void ChildRules_Inline_Should_Preserve_Ref_For_Nested_Object()
        {
            // Arrange: only the request validator exists; the child type has no standalone validator.
            var schemaRepository = new SchemaRepository();
            var schemaGenerator = SchemaGenerator(new ChildRulesRequestValidator());

            // Act
            var referenceSchema = schemaGenerator.GenerateSchema(typeof(ChildRulesRequest), schemaRepository);
            var requestSchema = schemaRepository.GetSchema(referenceSchema.GetRefId()!);

            // Assert: request schema should have "User" in required
            requestSchema.Required.Should().Contain("User");

            // Assert: the child component schema should exist
            schemaRepository.Schemas.Should().ContainKey("ChildRulesParams");

            // Assert: User should remain a $ref, not an inline copy (and so ChildRulesParams is not orphaned)
            var userProp = requestSchema.Properties["User"];
#if OPENAPI_V2
            userProp.Should().BeOfType<OpenApiSchemaReference>(
                "ChildRulesRequest.properties['user'] should be a $ref to ChildRulesParams, not an inline copy (Issue #198 comment)");
#else
            userProp.Reference.Should().NotBeNull(
                "ChildRulesRequest.properties['user'] should be a $ref to ChildRulesParams, not an inline copy (Issue #198 comment)");
#endif

            // Assert: the child component keeps ALL the inline ChildRules constraints
            var paramsSchema = schemaRepository.Schemas["ChildRulesParams"] as OpenApiSchema;
            paramsSchema.Should().NotBeNull();
            OpenApiSchemaCompatibility.GetProperty(paramsSchema!, "Email", schemaRepository)!.Format.Should().Be("email");
            OpenApiSchemaCompatibility.GetProperty(paramsSchema!, "Name", schemaRepository)!.MaxLength.Should().Be(510);

            // Assert: the required set lives on the child component (this is exactly what the orphan bug dropped)
            paramsSchema!.Required.Should().Contain("Email").And.Contain("Name");
        }
    }
}
