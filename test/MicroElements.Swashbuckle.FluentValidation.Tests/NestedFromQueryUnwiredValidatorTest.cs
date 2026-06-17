// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
    /// Issue #211: a validator for a nested type bound via [FromQuery] must only be reflected in the
    /// OpenAPI document when it is actually reachable from the ROOT validator through SetValidator/ChildRules.
    /// FluentValidation never auto-validates a child object from DI, so an unwired nested validator would
    /// document constraints (required, MinLength, ...) that runtime validation never enforces.
    /// </summary>
    public class NestedFromQueryUnwiredValidatorTest : UnitTestBase
    {
        public sealed record FilterType
        {
            public required FilterSubType RequiredSubType { get; init; }
        }

        public sealed record FilterSubType
        {
            public string? SubProperty { get; init; }
        }

        public sealed class FilterSubTypeValidator : AbstractValidator<FilterSubType>
        {
            public FilterSubTypeValidator()
            {
                RuleFor(x => x.SubProperty).NotEmpty();
            }
        }

        /// <summary>Reproduces the issue: NotNull() on the nested object but no SetValidator wiring.</summary>
        public sealed class UnwiredFilterTypeValidator : AbstractValidator<FilterType>
        {
            public UnwiredFilterTypeValidator()
            {
                RuleFor(x => x.RequiredSubType).NotNull();
            }
        }

        /// <summary>Correct usage: the nested validator is wired with SetValidator.</summary>
        public sealed class WiredFilterTypeValidator : AbstractValidator<FilterType>
        {
            public WiredFilterTypeValidator()
            {
                RuleFor(x => x.RequiredSubType).NotNull().SetValidator(new FilterSubTypeValidator());
            }
        }

        /// <summary>Wiring via ChildRules must be detected just like SetValidator.</summary>
        public sealed class ChildRulesFilterTypeValidator : AbstractValidator<FilterType>
        {
            public ChildRulesFilterTypeValidator()
            {
                RuleFor(x => x.RequiredSubType).NotNull().ChildRules(sub => sub.RuleFor(x => x.SubProperty).NotEmpty());
            }
        }

        /// <summary>Conditional SetValidator: excluded by default (ConditionalRulesMode.Exclude), so not "wired".</summary>
        public sealed class ConditionalWiredFilterTypeValidator : AbstractValidator<FilterType>
        {
            public ConditionalWiredFilterTypeValidator()
            {
                RuleFor(x => x.RequiredSubType).NotNull();
                RuleFor(x => x.RequiredSubType).SetValidator(new FilterSubTypeValidator()).When(x => x.RequiredSubType != null);
            }
        }

        // Real action methods so the operation filter can resolve the root [FromQuery] type
        // and walk the dot-path of nested parameters.
        public string GetUnwired([FromQuery] FilterType filter) => filter.ToString();

        public string GetWired([FromQuery] FilterType filter) => filter.ToString();

        public string GetChildRules([FromQuery] FilterType filter) => filter.ToString();

        public string GetConditional([FromQuery] FilterType filter) => filter.ToString();

        // --- Three-level break fixtures: wiring exists at the first level but breaks at the second. ---
        public sealed record DeepRoot
        {
            public required DeepMiddle Middle { get; init; }
        }

        public sealed record DeepMiddle
        {
            public required DeepLeaf Leaf { get; init; }
        }

        public sealed record DeepLeaf
        {
            public string? Value { get; init; }
        }

        public sealed class DeepRootValidator : AbstractValidator<DeepRoot>
        {
            public DeepRootValidator()
            {
                // Middle is wired, but DeepMiddleValidator does NOT wire Leaf — the chain breaks there.
                RuleFor(x => x.Middle).NotNull().SetValidator(new DeepMiddleValidator());
            }
        }

        public sealed class DeepMiddleValidator : AbstractValidator<DeepMiddle>
        {
            public DeepMiddleValidator()
            {
                RuleFor(x => x.Leaf).NotNull();
            }
        }

        public sealed class DeepLeafValidator : AbstractValidator<DeepLeaf>
        {
            public DeepLeafValidator()
            {
                RuleFor(x => x.Value).NotEmpty();
            }
        }

        public string GetDeepBreak([FromQuery] DeepRoot filter) => filter.ToString();

// OperationFilter integration tests require framework-specific OpenApi types.
#if !OPENAPI_V2
        /// <summary>
        /// Issue #211: the root validator does NOT call SetValidator for the nested object, so
        /// 'RequiredSubType.SubProperty' is never validated at runtime. It must not be marked required,
        /// and no value constraints (e.g. MinLength) should leak onto the parameter.
        /// </summary>
        [Fact]
        public void Unwired_Nested_Validator_Should_Not_Constrain_Query_Parameter()
        {
            var paramSchema = RunOperationFilter(
                new IValidator[] { new UnwiredFilterTypeValidator(), new FilterSubTypeValidator() },
                typeof(NestedFromQueryUnwiredValidatorTest).GetMethod(nameof(GetUnwired))!,
                typeof(FilterSubType), nameof(FilterSubType.SubProperty), "RequiredSubType.SubProperty",
                out var operation);

            operation.Parameters[0].Required.Should().BeFalse(
                because: "FilterTypeValidator does not SetValidator the nested type, so SubProperty is never validated (Issue #211)");
            paramSchema.MinLength.Should().BeNull(
                because: "an unwired nested validator must not leak value constraints onto the query parameter (Issue #211)");
        }

        /// <summary>
        /// Counterpart: when the nested validator IS wired via SetValidator, the nested rules are enforced
        /// at runtime and must be reflected — 'RequiredSubType.SubProperty' becomes required with MinLength=1.
        /// </summary>
        [Fact]
        public void Wired_Nested_Validator_Should_Constrain_Query_Parameter()
        {
            var paramSchema = RunOperationFilter(
                new IValidator[] { new WiredFilterTypeValidator(), new FilterSubTypeValidator() },
                typeof(NestedFromQueryUnwiredValidatorTest).GetMethod(nameof(GetWired))!,
                typeof(FilterSubType), nameof(FilterSubType.SubProperty), "RequiredSubType.SubProperty",
                out var operation);

            operation.Parameters[0].Required.Should().BeTrue(
                because: "RequiredSubType is required and SetValidator wires SubProperty's NotEmpty(), so the whole path is required");
            paramSchema.MinLength.Should().Be(1,
                because: "NotEmpty() on the wired nested SubProperty constrains the parameter value");
        }

        /// <summary>
        /// Wiring via ChildRules is detected the same as SetValidator (both produce an IChildValidatorAdaptor).
        /// </summary>
        [Fact]
        public void ChildRules_Wired_Nested_Validator_Should_Constrain_Query_Parameter()
        {
            var paramSchema = RunOperationFilter(
                new IValidator[] { new ChildRulesFilterTypeValidator(), new FilterSubTypeValidator() },
                typeof(NestedFromQueryUnwiredValidatorTest).GetMethod(nameof(GetChildRules))!,
                typeof(FilterSubType), nameof(FilterSubType.SubProperty), "RequiredSubType.SubProperty",
                out var operation);

            operation.Parameters[0].Required.Should().BeTrue(
                because: "ChildRules wires the nested validation, so the path is reachable and required");
            paramSchema.MinLength.Should().Be(1);
        }

        /// <summary>
        /// A conditional When() SetValidator is excluded by default (ConditionalRulesMode.Exclude), so the
        /// nested type is treated as not wired — matching how conditional rules are otherwise excluded.
        /// </summary>
        [Fact]
        public void Conditional_Wired_Nested_Validator_Should_Not_Constrain_By_Default()
        {
            var paramSchema = RunOperationFilter(
                new IValidator[] { new ConditionalWiredFilterTypeValidator(), new FilterSubTypeValidator() },
                typeof(NestedFromQueryUnwiredValidatorTest).GetMethod(nameof(GetConditional))!,
                typeof(FilterSubType), nameof(FilterSubType.SubProperty), "RequiredSubType.SubProperty",
                out var operation);

            operation.Parameters[0].Required.Should().BeFalse(
                because: "the SetValidator is conditional and conditional rules are excluded by default, so it is not treated as wired");
            paramSchema.MinLength.Should().BeNull();
        }

        /// <summary>
        /// Counterpart to the default-Exclude case: with ConditionalRulesMode.Include the conditional
        /// SetValidator IS treated as wired (matching how Include surfaces conditional rules), so the
        /// nested constraints are reflected on the parameter.
        /// </summary>
        [Fact]
        public void Conditional_Wired_Nested_Validator_Should_Constrain_When_Include_Mode()
        {
            var paramSchema = RunOperationFilter(
                new IValidator[] { new ConditionalWiredFilterTypeValidator(), new FilterSubTypeValidator() },
                typeof(NestedFromQueryUnwiredValidatorTest).GetMethod(nameof(GetConditional))!,
                typeof(FilterSubType), nameof(FilterSubType.SubProperty), "RequiredSubType.SubProperty",
                out var operation,
                conditionalRules: ConditionalRulesMode.Include);

            operation.Parameters[0].Required.Should().BeTrue(
                because: "under ConditionalRulesMode.Include the conditional SetValidator is treated as wired");
            paramSchema.MinLength.Should().Be(1);
        }

        /// <summary>
        /// Multi-level: the chain is wired at the first ancestor but breaks at the second
        /// ('Middle.Leaf.Value' — DeepMiddleValidator does not wire Leaf). The leaf must stay unconstrained.
        /// </summary>
        [Fact]
        public void Broken_Chain_At_Inner_Level_Should_Not_Constrain_Query_Parameter()
        {
            var paramSchema = RunOperationFilter(
                new IValidator[] { new DeepRootValidator(), new DeepMiddleValidator(), new DeepLeafValidator() },
                typeof(NestedFromQueryUnwiredValidatorTest).GetMethod(nameof(GetDeepBreak))!,
                typeof(DeepLeaf), nameof(DeepLeaf.Value), "Middle.Leaf.Value",
                out var operation);

            operation.Parameters[0].Required.Should().BeFalse(
                because: "DeepMiddleValidator does not SetValidator Leaf, so the chain to Value is broken (Issue #211)");
            paramSchema.MinLength.Should().BeNull();
        }

        /// <summary>
        /// No validator is registered for the root [FromQuery] type (only the leaf container has one).
        /// Runtime would run no validation for this path, so the parameter must stay unconstrained.
        /// (Behavioral change vs prior versions — see CHANGELOG.)
        /// </summary>
        [Fact]
        public void No_Root_Validator_Should_Not_Constrain_Query_Parameter()
        {
            var paramSchema = RunOperationFilter(
                new IValidator[] { new FilterSubTypeValidator() }, // no validator for FilterType (the root)
                typeof(NestedFromQueryUnwiredValidatorTest).GetMethod(nameof(GetUnwired))!,
                typeof(FilterSubType), nameof(FilterSubType.SubProperty), "RequiredSubType.SubProperty",
                out var operation);

            operation.Parameters[0].Required.Should().BeFalse(
                because: "without a validator for the root [FromQuery] type FluentValidation runs no validation for this path");
            paramSchema.MinLength.Should().BeNull();
        }

        /// <summary>
        /// camelCase parameter names (as produced by the default ASP.NET Core naming convention) must
        /// still match the PascalCase rule property names via EqualsIgnoreAll when walking the chain.
        /// </summary>
        [Fact]
        public void CamelCase_Parameter_Name_Wired_Should_Constrain_Query_Parameter()
        {
            var paramSchema = RunOperationFilter(
                new IValidator[] { new WiredFilterTypeValidator(), new FilterSubTypeValidator() },
                typeof(NestedFromQueryUnwiredValidatorTest).GetMethod(nameof(GetWired))!,
                typeof(FilterSubType), nameof(FilterSubType.SubProperty), "requiredSubType.subProperty",
                out var operation);

            operation.Parameters[0].Required.Should().BeTrue(
                because: "the camelCase 'requiredSubType' segment must match the PascalCase rule via EqualsIgnoreAll");
            paramSchema.MinLength.Should().Be(1);
        }

        /// <summary>
        /// Builds an operation with a single nested dot-path query parameter (sharing the leaf container's
        /// metadata, as Swashbuckle produces) and runs <see cref="FluentValidationOperationFilter"/>.
        /// </summary>
        private OpenApiSchema RunOperationFilter(
            IValidator[] validators,
            MethodInfo methodInfo,
            System.Type leafContainerType,
            string leafPropertyName,
            string parameterName,
            out OpenApiOperation operation,
            ConditionalRulesMode conditionalRules = ConditionalRulesMode.Exclude)
        {
            var schemaGeneratorOptions = new SchemaGeneratorOptions();
            var schemaRepository = new SchemaRepository();
            var schemaGenerator = SchemaGenerator(validators);

            var schemaGenerationOptions = new SchemaGenerationOptions
            {
                NameResolver = new SystemTextJsonNameResolver(),
                SchemaIdSelector = schemaGeneratorOptions.SchemaIdSelector,
                ConditionalRules = conditionalRules,
            };

            var validatorRegistry = new ValidatorRegistry(
                validators,
                new OptionsWrapper<SchemaGenerationOptions>(schemaGenerationOptions));

            // Swashbuckle flattens [FromQuery] into params whose ModelMetadata.ContainerType is the leaf type.
            var leafMetadata = new EmptyModelMetadataProvider()
                .GetMetadataForProperty(leafContainerType, leafPropertyName);

            var apiDescription = new ApiDescription();
            apiDescription.ParameterDescriptions.Add(new ApiParameterDescription
            {
                Name = parameterName,
                ModelMetadata = leafMetadata,
                Source = BindingSource.Query,
            });

            var paramSchema = new OpenApiSchema { Type = "string" };
            operation = new OpenApiOperation
            {
                Parameters = new List<OpenApiParameter>
                {
                    new OpenApiParameter { Name = parameterName, In = ParameterLocation.Query, Schema = paramSchema },
                },
            };

            var context = new OperationFilterContext(apiDescription, schemaGenerator, schemaRepository, methodInfo);

            new FluentValidationOperationFilter(
                validatorRegistry: validatorRegistry,
                schemaGenerationOptions: new OptionsWrapper<SchemaGenerationOptions>(schemaGenerationOptions))
                .Apply(operation, context);

            return paramSchema;
        }
#endif
    }
}
