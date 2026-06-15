// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using FluentAssertions;
using FluentValidation;
using MicroElements.OpenApi.FluentValidation;
using MicroElements.Swashbuckle.FluentValidation.Generation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Options;
#if !OPENAPI_V2
using Microsoft.OpenApi.Models;
#endif
using Swashbuckle.AspNetCore.SwaggerGen;
using Xunit;

namespace MicroElements.Swashbuckle.FluentValidation.Tests
{
    /// <summary>
    /// Issue #209: A required leaf property inside an OPTIONAL nested type bound via [FromQuery]
    /// must not be marked as a required operation parameter. A flattened dot-path parameter
    /// (e.g. "OptionalSubType.SubProperty") may be required only when EVERY ancestor segment of
    /// the path is itself required. The value constraints (e.g. MinLength) still apply.
    /// </summary>
    public class NestedFromQueryOptionalAncestorTest : UnitTestBase
    {
        public sealed record class FilterType
        {
            public FilterSubType? OptionalSubType { get; init; }

            public required FilterSubType RequiredSubType { get; init; }
        }

        public sealed record class FilterSubType
        {
            public string? SubProperty { get; init; }
        }

        public sealed class FilterTypeValidator : AbstractValidator<FilterType>
        {
            public FilterTypeValidator()
            {
                RuleFor(x => x.RequiredSubType).NotNull();
            }
        }

        public sealed class FilterSubTypeValidator : AbstractValidator<FilterSubType>
        {
            public FilterSubTypeValidator()
            {
                RuleFor(x => x.SubProperty).NotEmpty();
            }
        }

        // Real action method used as MethodInfo so the operation filter can resolve
        // the root [FromQuery] type and walk the dot-path of nested parameters.
        public string Get([FromQuery] FilterType filter) => filter.ToString();

// OperationFilter integration tests require framework-specific OpenApi types.
#if !OPENAPI_V2
        /// <summary>
        /// Issue #209: 'OptionalSubType' is optional, 'RequiredSubType' is required. Both expose a
        /// nested 'SubProperty' with NotEmpty(). Only 'RequiredSubType.SubProperty' should be a
        /// required query parameter; 'OptionalSubType.SubProperty' must stay optional.
        /// </summary>
        [Fact]
        public void Required_LeafProperty_In_Optional_Nested_Type_Should_Not_Be_Required()
        {
            // Arrange
            var schemaGeneratorOptions = new SchemaGeneratorOptions();
            var schemaRepository = new SchemaRepository();
            var schemaGenerator = SchemaGenerator(new FilterTypeValidator(), new FilterSubTypeValidator());

            var schemaGenerationOptions = new SchemaGenerationOptions
            {
                NameResolver = new SystemTextJsonNameResolver(),
                SchemaIdSelector = schemaGeneratorOptions.SchemaIdSelector,
            };

            var validatorRegistry = new ValidatorRegistry(
                new IValidator[] { new FilterTypeValidator(), new FilterSubTypeValidator() },
                new OptionsWrapper<SchemaGenerationOptions>(schemaGenerationOptions));

            var metadataProvider = new EmptyModelMetadataProvider();

            // Swashbuckle flattens [FromQuery] into params whose ModelMetadata.ContainerType is the leaf type.
            var subPropMetadata = metadataProvider.GetMetadataForProperty(typeof(FilterSubType), nameof(FilterSubType.SubProperty));

            var apiDescription = new ApiDescription();
            apiDescription.ParameterDescriptions.Add(new ApiParameterDescription
            {
                Name = "OptionalSubType.SubProperty",
                ModelMetadata = subPropMetadata,
                Source = BindingSource.Query,
            });
            apiDescription.ParameterDescriptions.Add(new ApiParameterDescription
            {
                Name = "RequiredSubType.SubProperty",
                ModelMetadata = subPropMetadata,
                Source = BindingSource.Query,
            });

            var optionalParamSchema = new OpenApiSchema { Type = "string" };
            var requiredParamSchema = new OpenApiSchema { Type = "string" };

            var operation = new OpenApiOperation
            {
                Parameters = new List<OpenApiParameter>
                {
                    new OpenApiParameter { Name = "OptionalSubType.SubProperty", In = ParameterLocation.Query, Schema = optionalParamSchema },
                    new OpenApiParameter { Name = "RequiredSubType.SubProperty", In = ParameterLocation.Query, Schema = requiredParamSchema },
                },
            };

            var context = new OperationFilterContext(
                apiDescription,
                schemaGenerator,
                schemaRepository,
                typeof(NestedFromQueryOptionalAncestorTest).GetMethod(nameof(Get))!);

            var operationFilter = new FluentValidationOperationFilter(
                validatorRegistry: validatorRegistry,
                schemaGenerationOptions: new OptionsWrapper<SchemaGenerationOptions>(schemaGenerationOptions));

            // Act
            operationFilter.Apply(operation, context);

            // Assert: the NotEmpty value constraint applies to BOTH params (when provided, must be non-empty).
            optionalParamSchema.MinLength.Should().Be(1,
                because: "NotEmpty still constrains the value of SubProperty when it is provided");
            requiredParamSchema.MinLength.Should().Be(1,
                because: "NotEmpty still constrains the value of SubProperty when it is provided");

            // Assert: required propagates ONLY when the whole dot-path is required.
            operation.Parameters[1].Required.Should().BeTrue(
                because: "RequiredSubType is required (NotNull) and SubProperty is required (NotEmpty) — the whole path is required");
            operation.Parameters[0].Required.Should().BeFalse(
                because: "OptionalSubType is optional, so the nested SubProperty must not become a required query parameter (Issue #209)");
        }
#endif
    }
}
