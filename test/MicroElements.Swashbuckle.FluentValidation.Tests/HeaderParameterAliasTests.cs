// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Text.Json.Serialization;
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
    /// Issue #230: aliased header parameters ([FromHeader(Name = "X-Correlation-Id")]) must get
    /// FluentValidation constraints from BOTH pipelines. Also covers the dotted-alias follow-up:
    /// a dot is legal in a header name ([FromHeader(Name = "X.Trace.Id")]) and must NOT trigger
    /// the nested [FromQuery] dot-path truncation (#209/#211) — header parameters are flat.
    /// </summary>
    public class HeaderParameterAliasTests : UnitTestBase
    {
        public class TraceRequest
        {
            // In a real app: [FromHeader(Name = "X-Trace-Id")] or [FromHeader(Name = "X.Trace.Id")].
            // In this harness the alias arrives through ApiParameterDescription.Name /
            // OpenApiParameter.Name, like ApiExplorer provides it.
            public string? XTraceId { get; set; }
        }

        public class TraceRequestValidator : AbstractValidator<TraceRequest>
        {
            public TraceRequestValidator()
            {
                RuleFor(x => x.XTraceId).NotEmpty().MaximumLength(36);
            }
        }

        public class RenamedRequest
        {
            [JsonPropertyName("cid")]
            public string? CorrelationId { get; set; }
        }

        public class RenamedRequestValidator : AbstractValidator<RenamedRequest>
        {
            public RenamedRequestValidator()
            {
                RuleFor(x => x.CorrelationId).NotEmpty().MaximumLength(36);
            }
        }

        private static ApiDescription HeaderApiDescription<TContainer>(string parameterName, string propertyName, string relativePath)
        {
            var apiDescription = new ApiDescription { RelativePath = relativePath, HttpMethod = "GET" };
            apiDescription.ParameterDescriptions.Add(new ApiParameterDescription
            {
                Name = parameterName,
                ModelMetadata = new EmptyModelMetadataProvider()
                    .GetMetadataForProperty(typeof(TContainer), propertyName),
                Source = BindingSource.Header,
            });
            return apiDescription;
        }

        private static SchemaGenerationOptions CreateOptions() => new SchemaGenerationOptions
        {
            NameResolver = new SystemTextJsonNameResolver(),
            SchemaIdSelector = new SchemaGeneratorOptions().SchemaIdSelector,
        };

        private static FluentValidationDocumentFilter CreateDocumentFilter(SchemaGenerationOptions options, params IValidator[] validators)
        {
            var validatorRegistry = new ValidatorRegistry(
                validators,
                new OptionsWrapper<SchemaGenerationOptions>(options));

            return new FluentValidationDocumentFilter(
                validatorRegistry: validatorRegistry,
                schemaGenerationOptions: new OptionsWrapper<SchemaGenerationOptions>(options));
        }

        private static FluentValidationOperationFilter CreateOperationFilter(SchemaGenerationOptions options, params IValidator[] validators)
        {
            var validatorRegistry = new ValidatorRegistry(
                validators,
                new OptionsWrapper<SchemaGenerationOptions>(options));

            return new FluentValidationOperationFilter(
                validatorRegistry: validatorRegistry,
                schemaGenerationOptions: new OptionsWrapper<SchemaGenerationOptions>(options));
        }

// OperationFilter/DocumentFilter integration tests require framework-specific OpenApi types.
#if !OPENAPI_V2
        private static (OpenApiDocument Doc, OpenApiOperation Operation, OpenApiSchema ParamSchema) BuildHeaderDoc(
            string relativePath, string paramName)
        {
            var paramSchema = new OpenApiSchema { Type = "string" };
            var operation = new OpenApiOperation
            {
                Parameters = new List<OpenApiParameter>
                {
                    new OpenApiParameter { Name = paramName, In = ParameterLocation.Header, Schema = paramSchema },
                },
            };
            var doc = new OpenApiDocument
            {
                Paths = new OpenApiPaths
                {
                    ["/" + relativePath] = new OpenApiPathItem
                    {
                        Operations = new Dictionary<OperationType, OpenApiOperation> { [OperationType.Get] = operation },
                    },
                },
            };
            return (doc, operation, paramSchema);
        }

        private static OperationFilterContext OperationContext(ApiDescription apiDescription, string testMethodName)
            => new OperationFilterContext(
                apiDescription,
                TestExtensions.CreateSchemaGenerator(),
                new SchemaRepository(),
                typeof(HeaderParameterAliasTests).GetMethod(testMethodName)!);

        /// <summary>Issue #230: the exact repro — kebab-case header alias under the document filter.</summary>
        [Fact]
        public void DocumentFilter_Header_With_Kebab_Alias_Should_Get_Constraints()
        {
            var filter = CreateDocumentFilter(CreateOptions(), new TraceRequestValidator());
            var (doc, operation, paramSchema) = BuildHeaderDoc("api/trace", "X-Trace-Id");
            var apiDescription = HeaderApiDescription<TraceRequest>("X-Trace-Id", nameof(TraceRequest.XTraceId), "api/trace");

            filter.Apply(doc, new DocumentFilterContext(new[] { apiDescription }, TestExtensions.CreateSchemaGenerator(), new SchemaRepository()));

            paramSchema.MaxLength.Should().Be(36,
                because: "the constraint copy must use the resolved schema property key, not the raw alias (#230)");
            paramSchema.MinLength.Should().Be(1);
            operation.Parameters[0].Required.Should().BeTrue(
                because: "NotEmpty on the bound property must mark the header parameter required");
        }

        [Fact]
        public void DocumentFilter_Header_With_Dotted_Alias_Should_Get_Constraints()
        {
            var filter = CreateDocumentFilter(CreateOptions(), new TraceRequestValidator());
            var (doc, operation, paramSchema) = BuildHeaderDoc("api/trace", "X.Trace.Id");
            var apiDescription = HeaderApiDescription<TraceRequest>("X.Trace.Id", nameof(TraceRequest.XTraceId), "api/trace");

            filter.Apply(doc, new DocumentFilterContext(new[] { apiDescription }, TestExtensions.CreateSchemaGenerator(), new SchemaRepository()));

            paramSchema.MaxLength.Should().Be(36,
                because: "a dot in a header alias must not trigger the nested [FromQuery] dot-path truncation");
            operation.Parameters[0].Required.Should().BeTrue();
        }

        [Fact]
        public void OperationFilter_Header_With_Dotted_Alias_Should_Get_Constraints()
        {
            var filter = CreateOperationFilter(CreateOptions(), new TraceRequestValidator());

            var paramSchema = new OpenApiSchema { Type = "string" };
            var operation = new OpenApiOperation
            {
                Parameters = new List<OpenApiParameter>
                {
                    new OpenApiParameter { Name = "X.Trace.Id", In = ParameterLocation.Header, Schema = paramSchema },
                },
            };
            var apiDescription = HeaderApiDescription<TraceRequest>("X.Trace.Id", nameof(TraceRequest.XTraceId), "api/trace");

            filter.Apply(operation, OperationContext(apiDescription, nameof(OperationFilter_Header_With_Dotted_Alias_Should_Get_Constraints)));

            paramSchema.MaxLength.Should().Be(36,
                because: "a dot in a header alias must not trigger the nested [FromQuery] dot-path truncation");
            operation.Parameters[0].Required.Should().BeTrue();
        }

        /// <summary>
        /// Control: the kebab-case alias on the operation filter worked before the fix
        /// (IgnoreAllStringComparer resolves "X-Trace-Id" ≈ "XTraceId") and must keep working.
        /// </summary>
        [Fact]
        public void OperationFilter_Header_With_Kebab_Alias_Should_Get_Constraints()
        {
            var filter = CreateOperationFilter(CreateOptions(), new TraceRequestValidator());

            var paramSchema = new OpenApiSchema { Type = "string" };
            var operation = new OpenApiOperation
            {
                Parameters = new List<OpenApiParameter>
                {
                    new OpenApiParameter { Name = "X-Trace-Id", In = ParameterLocation.Header, Schema = paramSchema },
                },
            };
            var apiDescription = HeaderApiDescription<TraceRequest>("X-Trace-Id", nameof(TraceRequest.XTraceId), "api/trace");

            filter.Apply(operation, OperationContext(apiDescription, nameof(OperationFilter_Header_With_Kebab_Alias_Should_Get_Constraints)));

            paramSchema.MaxLength.Should().Be(36);
            operation.Parameters[0].Required.Should().BeTrue();
        }

        /// <summary>
        /// Issue #230 follow-up: a rename beyond separators ([JsonPropertyName("cid")]) is not resolvable
        /// by the symbol-only comparer; the document filter must fall back to the NameResolver like the
        /// operation filter does.
        /// </summary>
        [Fact]
        public void DocumentFilter_Should_Resolve_Property_Renamed_By_NameResolver()
        {
            var filter = CreateDocumentFilter(CreateOptions(), new RenamedRequestValidator());
            var (doc, operation, paramSchema) = BuildHeaderDoc("api/renamed", "CorrelationId");
            var apiDescription = HeaderApiDescription<RenamedRequest>("CorrelationId", nameof(RenamedRequest.CorrelationId), "api/renamed");

            filter.Apply(doc, new DocumentFilterContext(new[] { apiDescription }, TestExtensions.CreateSchemaGenerator(), new SchemaRepository()));

            paramSchema.MaxLength.Should().Be(36,
                because: "the schema key 'cid' is only reachable through the NameResolver fallback (operation filter parity)");
            operation.Parameters[0].Required.Should().BeTrue();
        }
#endif

// The OpenApi v2 (Microsoft.OpenApi 2.x / Swashbuckle 10.x) object model — net10.0.
#if OPENAPI_V2
        private static (OpenApiDocument Doc, OpenApiOperation Operation, OpenApiSchema ParamSchema) BuildHeaderDocV2(
            string relativePath, string paramName)
        {
            var paramSchema = new OpenApiSchema { Type = JsonSchemaType.String };
            var operation = new OpenApiOperation
            {
                Parameters = new List<IOpenApiParameter>
                {
                    new OpenApiParameter { Name = paramName, In = ParameterLocation.Header, Schema = paramSchema },
                },
            };
            var doc = new OpenApiDocument
            {
                Paths = new OpenApiPaths
                {
                    ["/" + relativePath] = new OpenApiPathItem
                    {
                        Operations = new Dictionary<System.Net.Http.HttpMethod, OpenApiOperation>
                        {
                            [System.Net.Http.HttpMethod.Get] = operation,
                        },
                    },
                },
            };
            return (doc, operation, paramSchema);
        }

        /// <summary>Issue #230: the exact repro — kebab-case header alias under the document filter.</summary>
        [Fact]
        public void DocumentFilter_Header_With_Kebab_Alias_Should_Get_Constraints_V2()
        {
            var filter = CreateDocumentFilter(CreateOptions(), new TraceRequestValidator());
            var (doc, operation, paramSchema) = BuildHeaderDocV2("api/trace", "X-Trace-Id");
            var apiDescription = HeaderApiDescription<TraceRequest>("X-Trace-Id", nameof(TraceRequest.XTraceId), "api/trace");

            filter.Apply(doc, new DocumentFilterContext(new[] { apiDescription }, TestExtensions.CreateSchemaGenerator(), new SchemaRepository()));

            paramSchema.MaxLength.Should().Be(36,
                because: "the constraint copy must use the resolved schema property key, not the raw alias (#230)");
            paramSchema.MinLength.Should().Be(1);
            operation.Parameters![0].Required.Should().BeTrue();
        }

        [Fact]
        public void DocumentFilter_Header_With_Dotted_Alias_Should_Get_Constraints_V2()
        {
            var filter = CreateDocumentFilter(CreateOptions(), new TraceRequestValidator());
            var (doc, operation, paramSchema) = BuildHeaderDocV2("api/trace", "X.Trace.Id");
            var apiDescription = HeaderApiDescription<TraceRequest>("X.Trace.Id", nameof(TraceRequest.XTraceId), "api/trace");

            filter.Apply(doc, new DocumentFilterContext(new[] { apiDescription }, TestExtensions.CreateSchemaGenerator(), new SchemaRepository()));

            paramSchema.MaxLength.Should().Be(36,
                because: "a dot in a header alias must not trigger the nested [FromQuery] dot-path truncation");
            operation.Parameters![0].Required.Should().BeTrue();
        }

        [Fact]
        public void OperationFilter_Header_With_Dotted_Alias_Should_Get_Constraints_V2()
        {
            var filter = CreateOperationFilter(CreateOptions(), new TraceRequestValidator());

            var paramSchema = new OpenApiSchema { Type = JsonSchemaType.String };
            var operation = new OpenApiOperation
            {
                Parameters = new List<IOpenApiParameter>
                {
                    new OpenApiParameter { Name = "X.Trace.Id", In = ParameterLocation.Header, Schema = paramSchema },
                },
            };
            var context = new OperationFilterContext(
                HeaderApiDescription<TraceRequest>("X.Trace.Id", nameof(TraceRequest.XTraceId), "api/trace"),
                TestExtensions.CreateSchemaGenerator(),
                new SchemaRepository(),
                new OpenApiDocument(),
                typeof(HeaderParameterAliasTests).GetMethod(nameof(OperationFilter_Header_With_Dotted_Alias_Should_Get_Constraints_V2))!);

            filter.Apply(operation, context);

            paramSchema.MaxLength.Should().Be(36,
                because: "a dot in a header alias must not trigger the nested [FromQuery] dot-path truncation");
            operation.Parameters[0].Required.Should().BeTrue();
        }

        /// <summary>
        /// Control: the kebab-case alias on the operation filter worked before the fix and must keep working.
        /// </summary>
        [Fact]
        public void OperationFilter_Header_With_Kebab_Alias_Should_Get_Constraints_V2()
        {
            var filter = CreateOperationFilter(CreateOptions(), new TraceRequestValidator());

            var paramSchema = new OpenApiSchema { Type = JsonSchemaType.String };
            var operation = new OpenApiOperation
            {
                Parameters = new List<IOpenApiParameter>
                {
                    new OpenApiParameter { Name = "X-Trace-Id", In = ParameterLocation.Header, Schema = paramSchema },
                },
            };
            var context = new OperationFilterContext(
                HeaderApiDescription<TraceRequest>("X-Trace-Id", nameof(TraceRequest.XTraceId), "api/trace"),
                TestExtensions.CreateSchemaGenerator(),
                new SchemaRepository(),
                new OpenApiDocument(),
                typeof(HeaderParameterAliasTests).GetMethod(nameof(OperationFilter_Header_With_Kebab_Alias_Should_Get_Constraints_V2))!);

            filter.Apply(operation, context);

            paramSchema.MaxLength.Should().Be(36);
            operation.Parameters[0].Required.Should().BeTrue();
        }

        /// <summary>
        /// Issue #230 follow-up: a rename beyond separators ([JsonPropertyName("cid")]) requires the
        /// NameResolver fallback (operation filter parity).
        /// </summary>
        [Fact]
        public void DocumentFilter_Should_Resolve_Property_Renamed_By_NameResolver_V2()
        {
            var filter = CreateDocumentFilter(CreateOptions(), new RenamedRequestValidator());
            var (doc, operation, paramSchema) = BuildHeaderDocV2("api/renamed", "CorrelationId");
            var apiDescription = HeaderApiDescription<RenamedRequest>("CorrelationId", nameof(RenamedRequest.CorrelationId), "api/renamed");

            filter.Apply(doc, new DocumentFilterContext(new[] { apiDescription }, TestExtensions.CreateSchemaGenerator(), new SchemaRepository()));

            paramSchema.MaxLength.Should().Be(36,
                because: "the schema key 'cid' is only reachable through the NameResolver fallback (operation filter parity)");
            operation.Parameters![0].Required.Should().BeTrue();
        }
#endif
    }
}
