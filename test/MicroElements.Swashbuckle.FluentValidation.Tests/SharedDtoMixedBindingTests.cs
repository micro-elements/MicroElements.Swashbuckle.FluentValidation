// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// Issue #226 requires SchemaRepository.ReplaceSchemaId (Swashbuckle 10.1.0+, net10.0/OPENAPI_V2 target only).
#if OPENAPI_V2
using System.Collections.Generic;
using FluentAssertions;
using FluentValidation;
using MicroElements.OpenApi.FluentValidation;
using MicroElements.Swashbuckle.FluentValidation.Generation;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using Xunit;

namespace MicroElements.Swashbuckle.FluentValidation.Tests
{
    /// <summary>
    /// Issue #226 (ADR-006): when the same DTO is bound as a flattened [FromQuery] container by one endpoint
    /// and as a request body ([FromBody]/[FromForm]) by another, the Issue #180 cleanup after the query
    /// operation left the type "reserved-but-removed" in Swashbuckle's <see cref="SchemaRepository"/>.
    /// Generating the body operation then produced a $ref to a component that no longer exists (a dangling
    /// reference) and the FluentValidation rules never reached the emitted document.
    ///
    /// The fix (ADR-006, net10.0/OPENAPI_V2 only) heals the repository state during the cleanup via
    /// SchemaRepository.ReplaceSchemaId (Swashbuckle 10.1.0+): the reservation is cleared together with the
    /// component removal, so the body operation regenerates a full component and the FluentValidationRules
    /// schema filter re-applies the rules to the real document object.
    /// https://github.com/micro-elements/MicroElements.Swashbuckle.FluentValidation/issues/226
    /// </summary>
    public class SharedDtoMixedBindingTests : UnitTestBase
    {
        public class HelloRequest
        {
            public string? Name { get; set; }
        }

        public class HelloRequestValidator : AbstractValidator<HelloRequest>
        {
            public HelloRequestValidator()
            {
                RuleFor(x => x.Name).NotEmpty().MaximumLength(10);
            }
        }

        [Fact]
        public void JsonBody_Component_Should_Exist_And_Carry_Rules_After_Query_Operation_Cleanup()
        {
            var (operationFilter, schemaGenerator, schemaRepository) = CreateSharedPipeline();

            // Operation 1: [FromQuery] HelloRequest — the Issue #180 cleanup removes the side-effect component.
            ApplyToQueryEndpoint(operationFilter, schemaGenerator, schemaRepository);

            // Operation 2: Swashbuckle generates the [FromBody] (JSON) request body for the SAME type.
            var bodySchema = schemaGenerator.GenerateSchema(typeof(HelloRequest), schemaRepository);

            // The emitted $ref must resolve to an existing component (no dangling reference).
            bodySchema.GetRefId().Should().Be("HelloRequest");
            schemaRepository.Schemas.Should().ContainKey(
                "HelloRequest",
                because: "a $ref emitted into the request body must resolve to an existing component (Issue #226)");

            // ...and the component must carry the FluentValidation constraints.
            var component = schemaRepository.GetSchema("HelloRequest");
            var nameProperty = component.GetProperty("Name", schemaRepository);
            nameProperty.Should().NotBeNull();
            nameProperty!.MinLength.Should().Be(1, because: "NotEmpty must reach the body-bound shared DTO (Issue #226)");
            nameProperty.MaxLength.Should().Be(10, because: "MaximumLength(10) must reach the body-bound shared DTO (Issue #226)");
        }

        [Fact]
        public void FormBody_Referenced_Component_Should_Exist_And_Carry_Rules_After_Query_Operation_Cleanup()
        {
            var (operationFilter, schemaGenerator, schemaRepository) = CreateSharedPipeline();

            // Operation 1: [FromQuery] HelloRequest — the Issue #180 cleanup removes the side-effect component.
            ApplyToQueryEndpoint(operationFilter, schemaGenerator, schemaRepository);

            // Operation 2: [FromForm] — Swashbuckle generates the multipart body schema for the same type.
            var bodySchema = schemaGenerator.GenerateSchema(typeof(HelloRequest), schemaRepository);

            var metadataProvider = new EmptyModelMetadataProvider();
            var apiDescription = new ApiDescription();
            apiDescription.ParameterDescriptions.Add(new ApiParameterDescription
            {
                Name = "form",
                ModelMetadata = metadataProvider.GetMetadataForType(typeof(HelloRequest)),
                Source = BindingSource.Form,
            });

            var operation = new OpenApiOperation
            {
                RequestBody = new OpenApiRequestBody
                {
                    Content = new Dictionary<string, OpenApiMediaType>
                    {
                        ["multipart/form-data"] = new OpenApiMediaType { Schema = bodySchema },
                    },
                },
            };

            var context = new OperationFilterContext(
                apiDescription,
                schemaGenerator,
                schemaRepository,
                new OpenApiDocument(),
                typeof(SharedDtoMixedBindingTests).GetMethod(nameof(FormBody_Referenced_Component_Should_Exist_And_Carry_Rules_After_Query_Operation_Cleanup))!);

            operationFilter.Apply(operation, context);

            // The multipart body's $ref must resolve to an existing component carrying the rules.
            schemaRepository.Schemas.Should().ContainKey(
                "HelloRequest",
                because: "the form body's $ref must resolve to an existing component (Issue #226)");

            var component = schemaRepository.GetSchema("HelloRequest");
            var nameProperty = component.GetProperty("Name", schemaRepository);
            nameProperty.Should().NotBeNull();
            nameProperty!.MinLength.Should().Be(1, because: "NotEmpty must reach the form-bound shared DTO (Issue #226)");
            nameProperty.MaxLength.Should().Be(10, because: "MaximumLength(10) must reach the form-bound shared DTO (Issue #226)");
        }

        /// <summary>
        /// One SchemaRepository + SchemaGenerator (with the FluentValidationRules schema filter) shared across
        /// operations, exactly like the real Swagger pipeline where SchemaRepository persists for the whole document.
        /// </summary>
        private (FluentValidationOperationFilter OperationFilter, SchemaGenerator SchemaGenerator, SchemaRepository SchemaRepository) CreateSharedPipeline()
        {
            var schemaGeneratorOptions = new SchemaGeneratorOptions();
            var schemaRepository = new SchemaRepository();
            var schemaGenerator = SchemaGenerator(new HelloRequestValidator());

            var schemaGenerationOptions = new SchemaGenerationOptions
            {
                NameResolver = new SystemTextJsonNameResolver(),
                SchemaIdSelector = schemaGeneratorOptions.SchemaIdSelector,
                RemoveUnusedQuerySchemas = true, // the default; this is the flag that triggers the bug
            };

            var validatorRegistry = new ValidatorRegistry(
                new IValidator[] { new HelloRequestValidator() },
                new OptionsWrapper<SchemaGenerationOptions>(schemaGenerationOptions));

            var operationFilter = new FluentValidationOperationFilter(
                validatorRegistry: validatorRegistry,
                schemaGenerationOptions: new OptionsWrapper<SchemaGenerationOptions>(schemaGenerationOptions));

            return (operationFilter, schemaGenerator, schemaRepository);
        }

        /// <summary>
        /// Runs the <see cref="FluentValidationOperationFilter"/> for one GET endpoint that binds
        /// <see cref="HelloRequest"/> via [FromQuery] (flattened to the "Name" parameter).
        /// </summary>
        private static void ApplyToQueryEndpoint(
            FluentValidationOperationFilter operationFilter,
            SchemaGenerator schemaGenerator,
            SchemaRepository schemaRepository)
        {
            var metadataProvider = new EmptyModelMetadataProvider();
            var nameMetadata = metadataProvider.GetMetadataForProperty(typeof(HelloRequest), nameof(HelloRequest.Name));

            var apiDescription = new ApiDescription();
            apiDescription.ParameterDescriptions.Add(new ApiParameterDescription
            {
                Name = "Name",
                ModelMetadata = nameMetadata,
                Source = BindingSource.Query,
            });

            var nameParamSchema = new OpenApiSchema { Type = JsonSchemaType.String };
            var operation = new OpenApiOperation
            {
                Parameters = new List<IOpenApiParameter>
                {
                    new OpenApiParameter { Name = "Name", In = ParameterLocation.Query, Schema = nameParamSchema },
                },
            };

            var context = new OperationFilterContext(
                apiDescription,
                schemaGenerator,
                schemaRepository,
                new OpenApiDocument(),
                typeof(SharedDtoMixedBindingTests).GetMethod(nameof(JsonBody_Component_Should_Exist_And_Carry_Rules_After_Query_Operation_Cleanup))!);

            operationFilter.Apply(operation, context);
        }
    }
}
#endif
