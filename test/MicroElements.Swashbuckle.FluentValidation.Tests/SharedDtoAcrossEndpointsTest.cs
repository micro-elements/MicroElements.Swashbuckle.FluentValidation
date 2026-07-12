// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using FluentAssertions;
using FluentValidation;
using MicroElements.OpenApi.FluentValidation;
using MicroElements.Swashbuckle.FluentValidation.Generation;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Options;
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
    /// Issue #223: when the same [FromQuery] DTO is bound by more than one endpoint, only the FIRST
    /// operation receives the FluentValidation rules; the rest lose them.
    ///
    /// Root cause: the Issue #180 cleanup removes the container schema from <see cref="SchemaRepository.Schemas"/>
    /// but NOT from Swashbuckle's internal reserved-ids map. Because <see cref="FluentValidationOperationFilter"/>
    /// runs once per operation, the second operation's GetSchemaForType hits the reserved-but-removed type and
    /// gets back a bare $ref schema (no Properties), so the rule-application guard is skipped.
    ///
    /// Repro requires RemoveUnusedQuerySchemas = true (the default) AND a SchemaRepository shared across
    /// operations — exactly how the real Swagger pipeline works.
    /// https://github.com/micro-elements/MicroElements.Swashbuckle.FluentValidation/issues/223
    /// </summary>
    public class SharedDtoAcrossEndpointsTest : UnitTestBase
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

// OperationFilter integration tests require framework-specific OpenApi types (excluded on net10.0/OPENAPI_V2).
#if !OPENAPI_V2
        [Fact]
        public void OperationFilter_Should_Apply_Rules_To_Every_Endpoint_Sharing_The_Same_FromQuery_Dto()
        {
            // Arrange — a SINGLE SchemaRepository + SchemaGenerator shared across both operations,
            // exactly like the real Swagger pipeline where SchemaRepository persists for the whole document.
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

            // Act — two endpoints ("/api/hello" and "/api/hello2") binding the same HelloRequest via [FromQuery].
            var firstName = ApplyToEndpoint(operationFilter, schemaGenerator, schemaRepository);
            var secondName = ApplyToEndpoint(operationFilter, schemaGenerator, schemaRepository);

            // Assert — the FIRST endpoint gets the rules today...
            firstName.MinLength.Should().Be(1, because: "the first endpoint has always worked");
            firstName.MaxLength.Should().Be(10, because: "the first endpoint has always worked");

            // ...and the SECOND endpoint MUST get them too (currently FAILS — Issue #223).
            secondName.MinLength.Should().Be(1,
                because: "every endpoint binding the shared HelloRequest must expose NotEmpty (Issue #223)");
            secondName.MaxLength.Should().Be(10,
                because: "every endpoint binding the shared HelloRequest must expose MaximumLength(10) (Issue #223)");
        }

        /// <summary>
        /// Runs the <see cref="FluentValidationOperationFilter"/> for one GET endpoint that binds
        /// <see cref="HelloRequest"/> via [FromQuery], and returns the resulting "Name" parameter schema.
        /// </summary>
        private static OpenApiSchema ApplyToEndpoint(
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

            var nameParamSchema = new OpenApiSchema { Type = "string" };
            var operation = new OpenApiOperation
            {
                Parameters = new List<OpenApiParameter>
                {
                    new OpenApiParameter { Name = "Name", In = ParameterLocation.Query, Schema = nameParamSchema },
                },
            };

            var context = new OperationFilterContext(
                apiDescription,
                schemaGenerator,
                schemaRepository,
                typeof(SharedDtoAcrossEndpointsTest).GetMethod(nameof(OperationFilter_Should_Apply_Rules_To_Every_Endpoint_Sharing_The_Same_FromQuery_Dto))!);

            operationFilter.Apply(operation, context);

            return nameParamSchema;
        }
#endif

// OPENAPI_V2 (net10.0) port of the same scenario — the net10.0 target where the
// state-healing cleanup (Issue #226) lives previously had no shared-DTO coverage at all.
#if OPENAPI_V2
        [Fact]
        public void OperationFilter_Should_Apply_Rules_To_Every_Endpoint_Sharing_The_Same_FromQuery_Dto()
        {
            // Arrange — a SINGLE SchemaRepository + SchemaGenerator shared across both operations,
            // exactly like the real Swagger pipeline where SchemaRepository persists for the whole document.
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

            // Act — two endpoints ("/api/hello" and "/api/hello2") binding the same HelloRequest via [FromQuery].
            var firstName = ApplyToEndpoint(operationFilter, schemaGenerator, schemaRepository);
            var secondName = ApplyToEndpoint(operationFilter, schemaGenerator, schemaRepository);

            // Assert — every endpoint binding the shared HelloRequest must expose the rules (Issue #223).
            firstName.MinLength.Should().Be(1, because: "the first endpoint has always worked");
            firstName.MaxLength.Should().Be(10, because: "the first endpoint has always worked");
            secondName.MinLength.Should().Be(1,
                because: "every endpoint binding the shared HelloRequest must expose NotEmpty (Issue #223)");
            secondName.MaxLength.Should().Be(10,
                because: "every endpoint binding the shared HelloRequest must expose MaximumLength(10) (Issue #223)");
        }

        /// <summary>
        /// Runs the <see cref="FluentValidationOperationFilter"/> for one GET endpoint that binds
        /// <see cref="HelloRequest"/> via [FromQuery], and returns the resulting "Name" parameter schema.
        /// </summary>
        private static OpenApiSchema ApplyToEndpoint(
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
                typeof(SharedDtoAcrossEndpointsTest).GetMethod(nameof(OperationFilter_Should_Apply_Rules_To_Every_Endpoint_Sharing_The_Same_FromQuery_Dto))!);

            operationFilter.Apply(operation, context);

            return nameParamSchema;
        }
#endif
    }
}
