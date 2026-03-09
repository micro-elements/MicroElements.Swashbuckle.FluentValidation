// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Text.Json;
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
    /// Issue #162: Nested object validation for [FromQuery] parameters.
    /// When Swashbuckle decomposes [FromQuery] FluentTest into flat parameters like "operation.op",
    /// the dot-path prefix must be stripped to match the leaf property name in the nested type's schema.
    /// </summary>
    public class NestedFromQueryValidatorTest : UnitTestBase
    {
        public class FluentOperation
        {
            public string Op { get; set; }
        }

        public class FluentTest
        {
            public string Par { get; set; }
            public FluentOperation Operation { get; set; }
        }

        public class FluentOperationValidator : AbstractValidator<FluentOperation>
        {
            public FluentOperationValidator()
            {
                RuleFor(x => x.Op).NotEmpty();
            }
        }

        public class FluentTestValidator : AbstractValidator<FluentTest>
        {
            public FluentTestValidator()
            {
                RuleFor(x => x.Par).NotEmpty();
                RuleFor(x => x.Operation).SetValidator(new FluentOperationValidator());
            }
        }

// OperationFilter integration tests require framework-specific OpenApi types.
#if !OPENAPI_V2
        /// <summary>
        /// Issue #162: Verifies that nested [FromQuery] parameters with dot-path names (e.g., "operation.op")
        /// get validation rules applied from the nested type's validator.
        /// </summary>
        [Fact]
        public void OperationFilter_Should_Apply_Rules_To_Nested_FromQuery_Parameters()
        {
            // Arrange
            var schemaGeneratorOptions = new SchemaGeneratorOptions();
            var schemaRepository = new SchemaRepository();
            var schemaGenerator = SchemaGenerator(new FluentOperationValidator());

            var schemaGenerationOptions = new SchemaGenerationOptions
            {
                NameResolver = new SystemTextJsonNameResolver(),
                SchemaIdSelector = schemaGeneratorOptions.SchemaIdSelector,
            };

            // Register the validator for FluentOperation (the nested type)
            var validatorRegistry = new ValidatorRegistry(
                new IValidator[] { new FluentOperationValidator() },
                new OptionsWrapper<SchemaGenerationOptions>(schemaGenerationOptions));

            var metadataProvider = new EmptyModelMetadataProvider();

            // Swashbuckle creates metadata where ContainerType = FluentOperation for nested params
            var opMetadata = metadataProvider.GetMetadataForProperty(typeof(FluentOperation), nameof(FluentOperation.Op));

            var apiDescription = new ApiDescription();
            apiDescription.ParameterDescriptions.Add(new ApiParameterDescription
            {
                // Swashbuckle uses the full dot-path name for nested [FromQuery] parameters
                Name = "operation.op",
                ModelMetadata = opMetadata,
                Source = BindingSource.Query,
            });

            var opParamSchema = new OpenApiSchema { Type = "string" };

            var operation = new OpenApiOperation
            {
                Parameters = new List<OpenApiParameter>
                {
                    // The parameter name includes the full dot-path
                    new OpenApiParameter { Name = "operation.op", In = ParameterLocation.Query, Schema = opParamSchema },
                },
            };

            var context = new OperationFilterContext(
                apiDescription,
                schemaGenerator,
                schemaRepository,
                typeof(NestedFromQueryValidatorTest).GetMethod(nameof(OperationFilter_Should_Apply_Rules_To_Nested_FromQuery_Parameters))!);

            // Act
            var operationFilter = new FluentValidationOperationFilter(
                validatorRegistry: validatorRegistry,
                schemaGenerationOptions: new OptionsWrapper<SchemaGenerationOptions>(schemaGenerationOptions));

            operationFilter.Apply(operation, context);

            // Assert: NotEmpty on Op should set MinLength=1 and Required=true
            opParamSchema.MinLength.Should().Be(1,
                because: "NotEmpty should set MinLength to 1 for nested [FromQuery] parameter 'operation.op'");
            operation.Parameters[0].Required.Should().BeTrue(
                because: "NotEmpty should mark nested [FromQuery] parameter as required");
        }

        /// <summary>
        /// Regression test: flat (non-nested) parameters should still work correctly.
        /// </summary>
        [Fact]
        public void OperationFilter_Should_Still_Apply_Rules_To_Flat_Parameters()
        {
            // Arrange
            var schemaGeneratorOptions = new SchemaGeneratorOptions();
            var schemaRepository = new SchemaRepository();
            var schemaGenerator = SchemaGenerator(new FluentTestValidator());

            var schemaGenerationOptions = new SchemaGenerationOptions
            {
                NameResolver = new SystemTextJsonNameResolver(),
                SchemaIdSelector = schemaGeneratorOptions.SchemaIdSelector,
            };

            var validatorRegistry = new ValidatorRegistry(
                new IValidator[] { new FluentTestValidator() },
                new OptionsWrapper<SchemaGenerationOptions>(schemaGenerationOptions));

            var metadataProvider = new EmptyModelMetadataProvider();
            var parMetadata = metadataProvider.GetMetadataForProperty(typeof(FluentTest), nameof(FluentTest.Par));

            var apiDescription = new ApiDescription();
            apiDescription.ParameterDescriptions.Add(new ApiParameterDescription
            {
                Name = "Par",
                ModelMetadata = parMetadata,
                Source = BindingSource.Query,
            });

            var parParamSchema = new OpenApiSchema { Type = "string" };

            var operation = new OpenApiOperation
            {
                Parameters = new List<OpenApiParameter>
                {
                    new OpenApiParameter { Name = "Par", In = ParameterLocation.Query, Schema = parParamSchema },
                },
            };

            var context = new OperationFilterContext(
                apiDescription,
                schemaGenerator,
                schemaRepository,
                typeof(NestedFromQueryValidatorTest).GetMethod(nameof(OperationFilter_Should_Still_Apply_Rules_To_Flat_Parameters))!);

            // Act
            var operationFilter = new FluentValidationOperationFilter(
                validatorRegistry: validatorRegistry,
                schemaGenerationOptions: new OptionsWrapper<SchemaGenerationOptions>(schemaGenerationOptions));

            operationFilter.Apply(operation, context);

            // Assert: NotEmpty on Par should set MinLength=1
            parParamSchema.MinLength.Should().Be(1,
                because: "NotEmpty should set MinLength to 1 for flat [FromQuery] parameter 'Par'");
            operation.Parameters[0].Required.Should().BeTrue(
                because: "NotEmpty should mark flat [FromQuery] parameter as required");
        }

        /// <summary>
        /// Issue #162: Tests multiple nesting levels (e.g., "a.b.c" style parameters).
        /// Only the leaf name ("c") should be used for schema property matching.
        /// </summary>
        [Fact]
        public void OperationFilter_Should_Apply_Rules_To_Deeply_Nested_Parameters()
        {
            // Arrange
            var schemaGeneratorOptions = new SchemaGeneratorOptions();
            var schemaRepository = new SchemaRepository();
            var schemaGenerator = SchemaGenerator(new FluentOperationValidator());

            var schemaGenerationOptions = new SchemaGenerationOptions
            {
                NameResolver = new SystemTextJsonNameResolver(),
                SchemaIdSelector = schemaGeneratorOptions.SchemaIdSelector,
            };

            var validatorRegistry = new ValidatorRegistry(
                new IValidator[] { new FluentOperationValidator() },
                new OptionsWrapper<SchemaGenerationOptions>(schemaGenerationOptions));

            var metadataProvider = new EmptyModelMetadataProvider();
            var opMetadata = metadataProvider.GetMetadataForProperty(typeof(FluentOperation), nameof(FluentOperation.Op));

            var apiDescription = new ApiDescription();
            apiDescription.ParameterDescriptions.Add(new ApiParameterDescription
            {
                // Simulate deeply nested: parent.operation.op
                Name = "parent.operation.op",
                ModelMetadata = opMetadata,
                Source = BindingSource.Query,
            });

            var opParamSchema = new OpenApiSchema { Type = "string" };

            var operation = new OpenApiOperation
            {
                Parameters = new List<OpenApiParameter>
                {
                    new OpenApiParameter { Name = "parent.operation.op", In = ParameterLocation.Query, Schema = opParamSchema },
                },
            };

            var context = new OperationFilterContext(
                apiDescription,
                schemaGenerator,
                schemaRepository,
                typeof(NestedFromQueryValidatorTest).GetMethod(nameof(OperationFilter_Should_Apply_Rules_To_Deeply_Nested_Parameters))!);

            // Act
            var operationFilter = new FluentValidationOperationFilter(
                validatorRegistry: validatorRegistry,
                schemaGenerationOptions: new OptionsWrapper<SchemaGenerationOptions>(schemaGenerationOptions));

            operationFilter.Apply(operation, context);

            // Assert: LastIndexOf('.') strips to "op", which matches the schema property
            opParamSchema.MinLength.Should().Be(1,
                because: "NotEmpty should set MinLength to 1 even for deeply nested parameter 'parent.operation.op'");
            operation.Parameters[0].Required.Should().BeTrue(
                because: "NotEmpty should mark deeply nested parameter as required");
        }

        /// <summary>
        /// Issue #162: Verifies that the DocumentFilter correctly copies validation rules
        /// from schema properties to parameter schemas for nested [FromQuery] parameters.
        /// </summary>
        [Fact]
        public void DocumentFilter_Should_Apply_Rules_To_Nested_FromQuery_Parameters()
        {
            // Arrange — use camelCase serialization to match real-world ASP.NET Core defaults
            var schemaGeneratorOptions = new SchemaGeneratorOptions();
            var schemaRepository = new SchemaRepository();
            var schemaGenerator = SchemaGenerator(
                configureSerializer: opts => opts.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);

            var schemaGenerationOptions = new SchemaGenerationOptions
            {
                NameResolver = new SystemTextJsonNameResolver(),
                SchemaIdSelector = schemaGeneratorOptions.SchemaIdSelector,
            };

            var validatorRegistry = new ValidatorRegistry(
                new IValidator[] { new FluentOperationValidator() },
                new OptionsWrapper<SchemaGenerationOptions>(schemaGenerationOptions));

            var metadataProvider = new EmptyModelMetadataProvider();
            var opMetadata = metadataProvider.GetMetadataForProperty(typeof(FluentOperation), nameof(FluentOperation.Op));

            var apiDescription = new ApiDescription { RelativePath = "api/test" };
            apiDescription.ParameterDescriptions.Add(new ApiParameterDescription
            {
                Name = "operation.op",
                ModelMetadata = opMetadata,
                Source = BindingSource.Query,
            });

            var opParamSchema = new OpenApiSchema { Type = "string" };

            var swaggerDoc = new OpenApiDocument
            {
                Paths = new OpenApiPaths
                {
                    ["/api/test"] = new OpenApiPathItem
                    {
                        Operations = new Dictionary<OperationType, OpenApiOperation>
                        {
                            [OperationType.Get] = new OpenApiOperation
                            {
                                Parameters = new List<OpenApiParameter>
                                {
                                    new OpenApiParameter { Name = "operation.op", In = ParameterLocation.Query, Schema = opParamSchema },
                                },
                            },
                        },
                    },
                },
            };

            var documentFilterContext = new DocumentFilterContext(
                new[] { apiDescription },
                schemaGenerator,
                schemaRepository);

            // Act
            var documentFilter = new FluentValidationDocumentFilter(
                validatorRegistry: validatorRegistry,
                schemaGenerationOptions: new OptionsWrapper<SchemaGenerationOptions>(schemaGenerationOptions));

            documentFilter.Apply(swaggerDoc, documentFilterContext);

            // Assert: The parameter schema should have validation rules copied from the nested type's schema
            opParamSchema.MinLength.Should().Be(1,
                because: "DocumentFilter should copy MinLength from nested type schema for dot-path parameter 'operation.op'");
        }
#endif
    }
}
