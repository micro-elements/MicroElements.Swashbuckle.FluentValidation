// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Reflection;
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
                // SetValidator wires the child so SubProperty's rules are actually enforced at runtime
                // (Issue #211): the operation filter only reflects nested rules when the chain is wired.
                RuleFor(x => x.RequiredSubType).NotNull().SetValidator(new FilterSubTypeValidator());
                RuleFor(x => x.OptionalSubType!).SetValidator(new FilterSubTypeValidator());
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

        // --- Three-level nesting fixtures (Issue #209, review item #5) ---
        public sealed record class DeepRoot
        {
            public DeepMiddle? OptionalMiddle { get; init; }

            public required DeepMiddle RequiredMiddle { get; init; }
        }

        public sealed record class DeepMiddle
        {
            public required DeepLeaf RequiredLeaf { get; init; }
        }

        public sealed record class DeepLeaf
        {
            public string? Value { get; init; }
        }

        public sealed class DeepRootValidator : AbstractValidator<DeepRoot>
        {
            public DeepRootValidator()
            {
                RuleFor(x => x.RequiredMiddle).NotNull().SetValidator(new DeepMiddleValidator());
                RuleFor(x => x.OptionalMiddle!).SetValidator(new DeepMiddleValidator());
            }
        }

        public sealed class DeepMiddleValidator : AbstractValidator<DeepMiddle>
        {
            public DeepMiddleValidator()
            {
                RuleFor(x => x.RequiredLeaf).NotNull().SetValidator(new DeepLeafValidator());
            }
        }

        public sealed class DeepLeafValidator : AbstractValidator<DeepLeaf>
        {
            public DeepLeafValidator()
            {
                RuleFor(x => x.Value).NotEmpty();
            }
        }

        public string GetDeep([FromQuery] DeepRoot filter) => filter.ToString();

        // --- Validator-only requiredness fixtures (Issue #209, review item #6) ---
        // 'Sub' is nullable and NOT a C# 'required' member — its requiredness comes solely
        // from the FluentValidation NotNull() rule.
        public sealed record class FvOnlyRoot
        {
            public FvOnlySub? Sub { get; init; }
        }

        public sealed record class FvOnlySub
        {
            public string? Value { get; init; }
        }

        public sealed class FvOnlyRootValidator : AbstractValidator<FvOnlyRoot>
        {
            public FvOnlyRootValidator()
            {
                RuleFor(x => x.Sub!).NotNull().SetValidator(new FvOnlySubValidator());
            }
        }

        public sealed class FvOnlySubValidator : AbstractValidator<FvOnlySub>
        {
            public FvOnlySubValidator()
            {
                RuleFor(x => x.Value).NotEmpty();
            }
        }

        public string GetFvOnly([FromQuery] FvOnlyRoot filter) => filter.ToString();

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

        /// <summary>
        /// Issue #209 (review item #5): the dot-path walk must handle nesting deeper than two levels.
        /// Only "RequiredMiddle.RequiredLeaf.Value" (all ancestors required) is a required parameter;
        /// "OptionalMiddle.RequiredLeaf.Value" must stay optional because the first ancestor is optional.
        /// </summary>
        [Fact]
        public void Three_Level_Nesting_Marks_Required_Only_When_All_Ancestors_Required()
        {
            var operation = RunOperationFilter(
                new IValidator[] { new DeepRootValidator(), new DeepMiddleValidator(), new DeepLeafValidator() },
                typeof(NestedFromQueryOptionalAncestorTest).GetMethod(nameof(GetDeep))!,
                typeof(DeepLeaf),
                nameof(DeepLeaf.Value),
                "OptionalMiddle.RequiredLeaf.Value",
                "RequiredMiddle.RequiredLeaf.Value");

            operation.Parameters[0].Schema!.MinLength.Should().Be(1);
            operation.Parameters[1].Schema!.MinLength.Should().Be(1);

            operation.Parameters[1].Required.Should().BeTrue(
                because: "every segment of RequiredMiddle.RequiredLeaf.Value is required");
            operation.Parameters[0].Required.Should().BeFalse(
                because: "OptionalMiddle is optional, so the deeply nested leaf must stay optional (Issue #209)");
        }

        /// <summary>
        /// Issue #209 (review item #6): an ancestor that is required ONLY via a FluentValidation
        /// NotNull() rule (the property is nullable and not a C# 'required' member) must still make
        /// the nested leaf a required parameter.
        /// </summary>
        [Fact]
        public void Ancestor_Required_Via_Validator_Only_Marks_Leaf_Required()
        {
            var operation = RunOperationFilter(
                new IValidator[] { new FvOnlyRootValidator(), new FvOnlySubValidator() },
                typeof(NestedFromQueryOptionalAncestorTest).GetMethod(nameof(GetFvOnly))!,
                typeof(FvOnlySub),
                nameof(FvOnlySub.Value),
                "Sub.Value");

            operation.Parameters[0].Schema!.MinLength.Should().Be(1);
            operation.Parameters[0].Required.Should().BeTrue(
                because: "Sub is required via NotNull() alone (no C# 'required'), so Sub.Value is required");
        }

        /// <summary>
        /// Builds an operation with the given dot-path query parameters (all sharing the leaf container's
        /// metadata, as Swashbuckle produces) and runs <see cref="FluentValidationOperationFilter"/>.
        /// </summary>
        private OpenApiOperation RunOperationFilter(
            IValidator[] validators,
            MethodInfo methodInfo,
            Type leafContainerType,
            string leafPropertyName,
            params string[] parameterNames)
        {
            var schemaGeneratorOptions = new SchemaGeneratorOptions();
            var schemaRepository = new SchemaRepository();
            var schemaGenerator = SchemaGenerator(validators);

            var schemaGenerationOptions = new SchemaGenerationOptions
            {
                NameResolver = new SystemTextJsonNameResolver(),
                SchemaIdSelector = schemaGeneratorOptions.SchemaIdSelector,
            };

            var validatorRegistry = new ValidatorRegistry(
                validators,
                new OptionsWrapper<SchemaGenerationOptions>(schemaGenerationOptions));

            var leafMetadata = new EmptyModelMetadataProvider().GetMetadataForProperty(leafContainerType, leafPropertyName);

            var apiDescription = new ApiDescription();
            var parameters = new List<OpenApiParameter>();
            foreach (var name in parameterNames)
            {
                apiDescription.ParameterDescriptions.Add(new ApiParameterDescription
                {
                    Name = name,
                    ModelMetadata = leafMetadata,
                    Source = BindingSource.Query,
                });
                parameters.Add(new OpenApiParameter { Name = name, In = ParameterLocation.Query, Schema = new OpenApiSchema { Type = "string" } });
            }

            var operation = new OpenApiOperation { Parameters = parameters };

            var context = new OperationFilterContext(apiDescription, schemaGenerator, schemaRepository, methodInfo);

            new FluentValidationOperationFilter(
                validatorRegistry: validatorRegistry,
                schemaGenerationOptions: new OptionsWrapper<SchemaGenerationOptions>(schemaGenerationOptions))
                .Apply(operation, context);

            return operation;
        }
#endif
    }
}
