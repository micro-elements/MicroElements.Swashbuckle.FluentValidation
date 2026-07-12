// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using FluentValidation;
using MicroElements.OpenApi.FluentValidation;
using MicroElements.OpenApi.FluentValidation.FileUpload;
using MicroElements.Swashbuckle.FluentValidation.Generation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
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
    /// ADR-007: parity tests for <see cref="FluentValidationDocumentFilter"/> against the default
    /// SchemaFilter+OperationFilter pipeline — required-marking (#209), request bodies and
    /// encoding.contentType (#216), multi-verb paths, multi-validator, shared-DTO scenarios
    /// (#223/#226 — structurally impossible under the document filter), and OPENAPI_V2 specifics
    /// (#198 ref preservation, reserved-id healing).
    /// The schema generator in these tests has NO FluentValidationRules schema filter, matching the
    /// real document-filter pipeline where the schema filter is not registered.
    /// </summary>
    public class DocumentFilterParityTests : UnitTestBase
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

        public class HelloRequestSecondValidator : AbstractValidator<HelloRequest>
        {
            public HelloRequestSecondValidator()
            {
                RuleFor(x => x.Name).MinimumLength(2);
            }
        }

        public class Child
        {
            public string? Value { get; set; }
        }

        public class ChildValidator : AbstractValidator<Child>
        {
            public ChildValidator()
            {
                RuleFor(x => x.Value).NotEmpty();
            }
        }

        public class RootWithChild
        {
            public Child? Child { get; set; }
        }

        public class RootOptionalChildValidator : AbstractValidator<RootWithChild>
        {
            public RootOptionalChildValidator()
            {
                RuleFor(x => x.Child).SetValidator(new ChildValidator()!);
            }
        }

        public class RootRequiredChildValidator : AbstractValidator<RootWithChild>
        {
            public RootRequiredChildValidator()
            {
                RuleFor(x => x.Child).NotNull().SetValidator(new ChildValidator()!);
            }
        }

        /// <summary>Real action method so AsParametersHelper can resolve the root [FromQuery] type.</summary>
        public static void FakeNestedAction([FromQuery] RootWithChild root)
        {
        }

        public class UploadRequest
        {
            [FromForm(Name = "File")]
            public IFormFile File { get; set; } = null!;
        }

        public class UploadRequestValidator : AbstractValidator<UploadRequest>
        {
            public UploadRequestValidator()
            {
                RuleFor(x => x.File).NotNull().FileContentType("image/png");
            }
        }

        private static (FluentValidationDocumentFilter Filter, SchemaGenerationOptions Options) CreateDocumentFilter(
            Action<SchemaGenerationOptions>? configure = null,
            params IValidator[] validators)
        {
            var schemaGenerationOptions = new SchemaGenerationOptions
            {
                NameResolver = new SystemTextJsonNameResolver(),
                SchemaIdSelector = new SchemaGeneratorOptions().SchemaIdSelector,
            };
            configure?.Invoke(schemaGenerationOptions);

            var validatorRegistry = new ValidatorRegistry(
                validators,
                new OptionsWrapper<SchemaGenerationOptions>(schemaGenerationOptions));

            var documentFilter = new FluentValidationDocumentFilter(
                validatorRegistry: validatorRegistry,
                schemaGenerationOptions: new OptionsWrapper<SchemaGenerationOptions>(schemaGenerationOptions));

            return (documentFilter, schemaGenerationOptions);
        }

        private static ApiDescription QueryApiDescription(
            Type containerType,
            string propertyName,
            string parameterName,
            string relativePath,
            string httpMethod = "GET",
            System.Reflection.MethodInfo? methodInfo = null)
        {
            var apiDescription = new ApiDescription { RelativePath = relativePath, HttpMethod = httpMethod };
            if (methodInfo != null)
                apiDescription.ActionDescriptor = new ControllerActionDescriptor { MethodInfo = methodInfo };

            apiDescription.ParameterDescriptions.Add(new ApiParameterDescription
            {
                Name = parameterName,
                ModelMetadata = new EmptyModelMetadataProvider().GetMetadataForProperty(containerType, propertyName),
                Source = BindingSource.Query,
            });

            return apiDescription;
        }

        private static ApiDescription BodyApiDescription(Type bodyType, string relativePath, string httpMethod = "POST")
        {
            var apiDescription = new ApiDescription { RelativePath = relativePath, HttpMethod = httpMethod };
            apiDescription.ParameterDescriptions.Add(new ApiParameterDescription
            {
                Name = "body",
                ModelMetadata = new EmptyModelMetadataProvider().GetMetadataForType(bodyType),
                Source = BindingSource.Body,
            });

            return apiDescription;
        }

// The OpenApi v1 (Swashbuckle 8.x) object model — net8.0/net9.0.
#if !OPENAPI_V2
        private static (OpenApiDocument Doc, OpenApiOperation Operation, OpenApiSchema ParamSchema) BuildQueryDoc(
            string relativePath, string paramName, OperationType operationType = OperationType.Get)
        {
            var paramSchema = new OpenApiSchema { Type = "string" };
            var operation = new OpenApiOperation
            {
                Parameters = new List<OpenApiParameter>
                {
                    new OpenApiParameter { Name = paramName, In = ParameterLocation.Query, Schema = paramSchema },
                },
            };
            var doc = new OpenApiDocument
            {
                Paths = new OpenApiPaths
                {
                    ["/" + relativePath] = new OpenApiPathItem
                    {
                        Operations = new Dictionary<OperationType, OpenApiOperation> { [operationType] = operation },
                    },
                },
            };
            return (doc, operation, paramSchema);
        }

        [Fact]
        public void DocumentFilter_Should_Mark_Flat_Parameter_Required_And_Copy_Constraints()
        {
            var (filter, _) = CreateDocumentFilter(null, new HelloRequestValidator());
            var (doc, operation, paramSchema) = BuildQueryDoc("api/hello", "Name");
            var apiDescription = QueryApiDescription(typeof(HelloRequest), nameof(HelloRequest.Name), "Name", "api/hello");

            var schemaRepository = new SchemaRepository();
            var schemaGenerator = TestExtensions.CreateSchemaGenerator();

            filter.Apply(doc, new DocumentFilterContext(new[] { apiDescription }, schemaGenerator, schemaRepository));

            operation.Parameters[0].Required.Should().BeTrue(
                because: "NotEmpty must mark the parameter required (#209 parity, ADR-007)");
            paramSchema.MinLength.Should().Be(1);
            paramSchema.MaxLength.Should().Be(10);
        }

        [Fact]
        public void DocumentFilter_Should_Not_Mark_Required_When_Ancestor_Is_Optional()
        {
            var (filter, _) = CreateDocumentFilter(null, new RootOptionalChildValidator(), new ChildValidator());
            var (doc, operation, paramSchema) = BuildQueryDoc("api/nested", "Child.Value");
            var apiDescription = QueryApiDescription(
                typeof(Child), nameof(Child.Value), "Child.Value", "api/nested",
                methodInfo: typeof(DocumentFilterParityTests).GetMethod(nameof(FakeNestedAction)));

            var schemaRepository = new SchemaRepository();
            var schemaGenerator = TestExtensions.CreateSchemaGenerator();

            filter.Apply(doc, new DocumentFilterContext(new[] { apiDescription }, schemaGenerator, schemaRepository));

            paramSchema.MinLength.Should().Be(1, because: "the wired child validator's constraints must be copied");
            operation.Parameters[0].Required.Should().BeFalse(
                because: "an optional ancestor (no NotNull on Child) must keep the flattened parameter optional (#209)");
        }

        [Fact]
        public void DocumentFilter_Should_Mark_Required_When_Whole_Path_Is_Required()
        {
            var (filter, _) = CreateDocumentFilter(null, new RootRequiredChildValidator(), new ChildValidator());
            var (doc, operation, paramSchema) = BuildQueryDoc("api/nested", "Child.Value");
            var apiDescription = QueryApiDescription(
                typeof(Child), nameof(Child.Value), "Child.Value", "api/nested",
                methodInfo: typeof(DocumentFilterParityTests).GetMethod(nameof(FakeNestedAction)));

            var schemaRepository = new SchemaRepository();
            var schemaGenerator = TestExtensions.CreateSchemaGenerator();

            filter.Apply(doc, new DocumentFilterContext(new[] { apiDescription }, schemaGenerator, schemaRepository));

            paramSchema.MinLength.Should().Be(1);
            operation.Parameters[0].Required.Should().BeTrue(
                because: "every segment of the dot-path is required (NotNull ancestor + NotEmpty leaf, #209)");
        }

        [Fact]
        public void DocumentFilter_Should_Process_Every_Operation_Of_A_MultiVerb_Path()
        {
            var (filter, _) = CreateDocumentFilter(null, new HelloRequestValidator());

            var getParamSchema = new OpenApiSchema { Type = "string" };
            var postParamSchema = new OpenApiSchema { Type = "string" };
            var getOperation = new OpenApiOperation
            {
                Parameters = new List<OpenApiParameter> { new OpenApiParameter { Name = "Name", In = ParameterLocation.Query, Schema = getParamSchema } },
            };
            var postOperation = new OpenApiOperation
            {
                Parameters = new List<OpenApiParameter> { new OpenApiParameter { Name = "Name", In = ParameterLocation.Query, Schema = postParamSchema } },
            };
            var doc = new OpenApiDocument
            {
                Paths = new OpenApiPaths
                {
                    ["/api/multi"] = new OpenApiPathItem
                    {
                        Operations = new Dictionary<OperationType, OpenApiOperation>
                        {
                            [OperationType.Get] = getOperation,
                            [OperationType.Post] = postOperation,
                        },
                    },
                },
            };

            var apiDescriptions = new[]
            {
                QueryApiDescription(typeof(HelloRequest), nameof(HelloRequest.Name), "Name", "api/multi", "GET"),
                QueryApiDescription(typeof(HelloRequest), nameof(HelloRequest.Name), "Name", "api/multi", "POST"),
            };

            var schemaRepository = new SchemaRepository();
            var schemaGenerator = TestExtensions.CreateSchemaGenerator();

            filter.Apply(doc, new DocumentFilterContext(apiDescriptions, schemaGenerator, schemaRepository));

            getParamSchema.MinLength.Should().Be(1, because: "the GET operation must get constraints");
            postParamSchema.MinLength.Should().Be(1,
                because: "the POST operation of the same path must get constraints too (FindParam previously inspected only the first operation)");
        }

        [Fact]
        public void DocumentFilter_Should_Apply_Rules_To_Json_Body_Component_And_Keep_It()
        {
            var (filter, _) = CreateDocumentFilter(null, new HelloRequestValidator());

            var schemaRepository = new SchemaRepository();
            var schemaGenerator = TestExtensions.CreateSchemaGenerator();

            // Swashbuckle registers the body component BEFORE document filters run.
            schemaGenerator.GenerateSchema(typeof(HelloRequest), schemaRepository);

            var doc = new OpenApiDocument
            {
                Paths = new OpenApiPaths
                {
                    ["/api/hello"] = new OpenApiPathItem
                    {
                        Operations = new Dictionary<OperationType, OpenApiOperation> { [OperationType.Post] = new OpenApiOperation() },
                    },
                },
            };
            var apiDescription = BodyApiDescription(typeof(HelloRequest), "api/hello");

            filter.Apply(doc, new DocumentFilterContext(new[] { apiDescription }, schemaGenerator, schemaRepository));

            schemaRepository.Schemas.Should().ContainKey(
                "HelloRequest",
                because: "the body component is referenced by the document and must survive the #180 cleanup");
            var component = schemaRepository.GetSchema("HelloRequest");
            var nameProperty = component.GetProperty(nameof(HelloRequest.Name), schemaRepository)!;
            nameProperty.MinLength.Should().Be(1, because: "the document filter owns component schemas in its pipeline");
            nameProperty.MaxLength.Should().Be(10);
        }

        [Fact]
        public void DocumentFilter_SharedDto_Query_And_Body_Should_Both_Get_Rules()
        {
            var (filter, _) = CreateDocumentFilter(null, new HelloRequestValidator());

            var schemaRepository = new SchemaRepository();
            var schemaGenerator = TestExtensions.CreateSchemaGenerator();

            // Swashbuckle registers the body component BEFORE document filters run.
            schemaGenerator.GenerateSchema(typeof(HelloRequest), schemaRepository);

            var (queryDoc, queryOperation, queryParamSchema) = BuildQueryDoc("api/query", "Name");
            queryDoc.Paths["/api/body"] = new OpenApiPathItem
            {
                Operations = new Dictionary<OperationType, OpenApiOperation> { [OperationType.Post] = new OpenApiOperation() },
            };

            var apiDescriptions = new[]
            {
                QueryApiDescription(typeof(HelloRequest), nameof(HelloRequest.Name), "Name", "api/query"),
                BodyApiDescription(typeof(HelloRequest), "api/body"),
            };

            filter.Apply(queryDoc, new DocumentFilterContext(apiDescriptions, schemaGenerator, schemaRepository));

            // Issue #223/#226 shape: under the document filter, the shared DTO keeps its component
            // (referenced by the body) AND the flattened query parameter gets the constraints.
            queryParamSchema.MinLength.Should().Be(1);
            queryOperation.Parameters[0].Required.Should().BeTrue();
            schemaRepository.Schemas.Should().ContainKey("HelloRequest");
            var component = schemaRepository.GetSchema("HelloRequest");
            component.GetProperty(nameof(HelloRequest.Name), schemaRepository)!.MinLength.Should().Be(1);
        }

        [Fact]
        public void DocumentFilter_Should_Emit_Encoding_ContentType_For_Form_Body()
        {
            var (filter, _) = CreateDocumentFilter(null, new UploadRequestValidator());

            var mediaType = new OpenApiMediaType
            {
                Schema = new OpenApiSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, OpenApiSchema>
                    {
                        ["File"] = new OpenApiSchema { Type = "string", Format = "binary" },
                    },
                },
            };
            var operation = new OpenApiOperation
            {
                RequestBody = new OpenApiRequestBody
                {
                    Content = new Dictionary<string, OpenApiMediaType> { ["multipart/form-data"] = mediaType },
                },
            };
            var doc = new OpenApiDocument
            {
                Paths = new OpenApiPaths
                {
                    ["/api/upload"] = new OpenApiPathItem
                    {
                        Operations = new Dictionary<OperationType, OpenApiOperation> { [OperationType.Post] = operation },
                    },
                },
            };

            var apiDescription = new ApiDescription { RelativePath = "api/upload", HttpMethod = "POST" };
            apiDescription.ParameterDescriptions.Add(new ApiParameterDescription
            {
                Name = "File",
                ModelMetadata = new EmptyModelMetadataProvider().GetMetadataForType(typeof(UploadRequest)),
                Source = BindingSource.Form,
            });

            var schemaRepository = new SchemaRepository();
            var schemaGenerator = TestExtensions.CreateSchemaGenerator();

            filter.Apply(doc, new DocumentFilterContext(new[] { apiDescription }, schemaGenerator, schemaRepository));

            mediaType.Encoding.Should().ContainKey("File", because: "#216 encoding parity (ADR-007)");
            mediaType.Encoding["File"].ContentType.Should().Be("image/png");
        }

        [Fact]
        public void DocumentFilter_Should_Apply_All_Validators_When_Multiple_Registered()
        {
            var (filter, _) = CreateDocumentFilter(
                options => options.ValidatorSearch = options.ValidatorSearch with { IsOneValidatorForType = false },
                new HelloRequestValidator(),
                new HelloRequestSecondValidator());

            var schemaRepository = new SchemaRepository();
            var schemaGenerator = TestExtensions.CreateSchemaGenerator();
            schemaGenerator.GenerateSchema(typeof(HelloRequest), schemaRepository);

            var doc = new OpenApiDocument
            {
                Paths = new OpenApiPaths
                {
                    ["/api/hello"] = new OpenApiPathItem
                    {
                        Operations = new Dictionary<OperationType, OpenApiOperation> { [OperationType.Post] = new OpenApiOperation() },
                    },
                },
            };
            var apiDescription = BodyApiDescription(typeof(HelloRequest), "api/hello");

            filter.Apply(doc, new DocumentFilterContext(new[] { apiDescription }, schemaGenerator, schemaRepository));

            var component = schemaRepository.GetSchema("HelloRequest");
            var nameProperty = component.GetProperty(nameof(HelloRequest.Name), schemaRepository)!;
            nameProperty.MaxLength.Should().Be(10, because: "the first validator must be applied");
            nameProperty.MinLength.Should().Be(2, because: "the second validator must be applied too (multi-validator parity)");
        }
#endif

// The OpenApi v2 (Microsoft.OpenApi 2.x / Swashbuckle 10.x) object model — net10.0.
#if OPENAPI_V2
        private static (OpenApiDocument Doc, OpenApiOperation Operation, OpenApiSchema ParamSchema) BuildQueryDocV2(
            string relativePath, string paramName)
        {
            var paramSchema = new OpenApiSchema { Type = JsonSchemaType.String };
            var operation = new OpenApiOperation
            {
                Parameters = new List<IOpenApiParameter>
                {
                    new OpenApiParameter { Name = paramName, In = ParameterLocation.Query, Schema = paramSchema },
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

        [Fact]
        public void DocumentFilter_Should_Mark_Flat_Parameter_Required_And_Copy_Constraints_V2()
        {
            var (filter, _) = CreateDocumentFilter(null, new HelloRequestValidator());
            var (doc, operation, paramSchema) = BuildQueryDocV2("api/hello", "Name");
            var apiDescription = QueryApiDescription(typeof(HelloRequest), nameof(HelloRequest.Name), "Name", "api/hello");

            var schemaRepository = new SchemaRepository();
            var schemaGenerator = TestExtensions.CreateSchemaGenerator();

            filter.Apply(doc, new DocumentFilterContext(new[] { apiDescription }, schemaGenerator, schemaRepository));

            operation.Parameters![0].Required.Should().BeTrue(
                because: "NotEmpty must mark the parameter required (#209 parity, ADR-007)");
            paramSchema.MinLength.Should().Be(1);
            paramSchema.MaxLength.Should().Be(10);
        }

        [Fact]
        public void DocumentFilter_Should_Process_Every_Operation_Of_A_MultiVerb_Path_V2()
        {
            var (filter, _) = CreateDocumentFilter(null, new HelloRequestValidator());

            var getParamSchema = new OpenApiSchema { Type = JsonSchemaType.String };
            var postParamSchema = new OpenApiSchema { Type = JsonSchemaType.String };
            var getOperation = new OpenApiOperation
            {
                Parameters = new List<IOpenApiParameter> { new OpenApiParameter { Name = "Name", In = ParameterLocation.Query, Schema = getParamSchema } },
            };
            var postOperation = new OpenApiOperation
            {
                Parameters = new List<IOpenApiParameter> { new OpenApiParameter { Name = "Name", In = ParameterLocation.Query, Schema = postParamSchema } },
            };
            var doc = new OpenApiDocument
            {
                Paths = new OpenApiPaths
                {
                    ["/api/multi"] = new OpenApiPathItem
                    {
                        Operations = new Dictionary<System.Net.Http.HttpMethod, OpenApiOperation>
                        {
                            [System.Net.Http.HttpMethod.Get] = getOperation,
                            [System.Net.Http.HttpMethod.Post] = postOperation,
                        },
                    },
                },
            };

            var apiDescriptions = new[]
            {
                QueryApiDescription(typeof(HelloRequest), nameof(HelloRequest.Name), "Name", "api/multi", "GET"),
                QueryApiDescription(typeof(HelloRequest), nameof(HelloRequest.Name), "Name", "api/multi", "POST"),
            };

            var schemaRepository = new SchemaRepository();
            var schemaGenerator = TestExtensions.CreateSchemaGenerator();

            filter.Apply(doc, new DocumentFilterContext(apiDescriptions, schemaGenerator, schemaRepository));

            getParamSchema.MinLength.Should().Be(1);
            postParamSchema.MinLength.Should().Be(1,
                because: "the POST operation of the same path must get constraints too (FindParam previously inspected only the first operation)");
        }

        [Fact]
        public void DocumentFilter_SharedDto_Query_And_Body_Should_Both_Get_Rules_V2()
        {
            var (filter, _) = CreateDocumentFilter(null, new HelloRequestValidator());

            var schemaRepository = new SchemaRepository();
            var schemaGenerator = TestExtensions.CreateSchemaGenerator();

            // Swashbuckle registers the body component BEFORE document filters run.
            schemaGenerator.GenerateSchema(typeof(HelloRequest), schemaRepository);

            var (doc, queryOperation, queryParamSchema) = BuildQueryDocV2("api/query", "Name");
            doc.Paths["/api/body"] = new OpenApiPathItem
            {
                Operations = new Dictionary<System.Net.Http.HttpMethod, OpenApiOperation>
                {
                    [System.Net.Http.HttpMethod.Post] = new OpenApiOperation(),
                },
            };

            var apiDescriptions = new[]
            {
                QueryApiDescription(typeof(HelloRequest), nameof(HelloRequest.Name), "Name", "api/query"),
                BodyApiDescription(typeof(HelloRequest), "api/body"),
            };

            filter.Apply(doc, new DocumentFilterContext(apiDescriptions, schemaGenerator, schemaRepository));

            queryParamSchema.MinLength.Should().Be(1);
            schemaRepository.Schemas.Should().ContainKey("HelloRequest");
            var component = schemaRepository.GetSchema("HelloRequest");
            component.GetProperty(nameof(HelloRequest.Name), schemaRepository)!.MinLength.Should().Be(1);
        }

        [Fact]
        public void DocumentFilter_Cleanup_Should_Heal_ReservedIds_So_Later_Filters_Can_Regenerate()
        {
            var (filter, _) = CreateDocumentFilter(null, new HelloRequestValidator());
            var (doc, _, _) = BuildQueryDocV2("api/query", "Name");
            var apiDescription = QueryApiDescription(typeof(HelloRequest), nameof(HelloRequest.Name), "Name", "api/query");

            var schemaRepository = new SchemaRepository();
            var schemaGenerator = TestExtensions.CreateSchemaGenerator();

            filter.Apply(doc, new DocumentFilterContext(new[] { apiDescription }, schemaGenerator, schemaRepository));

            // The side-effect container component is removed by the #180 cleanup...
            schemaRepository.Schemas.Should().NotContainKey("HelloRequest");

            // ...and thanks to the ReplaceSchemaId healing (ADR-006/ADR-007), a THIRD-PARTY document
            // filter running after ours can regenerate a full component instead of a dangling $ref.
            schemaGenerator.GenerateSchema(typeof(HelloRequest), schemaRepository);
            schemaRepository.Schemas.Should().ContainKey(
                "HelloRequest",
                because: "the reserved-id must be cleared together with the component removal (Issue #226)");
        }

        public class ChildDto
        {
            public string? Data { get; set; }
        }

        public class ParentWithChild
        {
            public string? Name { get; set; }

            public ChildDto? Child { get; set; }
        }

        public class ParentWithChildValidator : AbstractValidator<ParentWithChild>
        {
            public ParentWithChildValidator()
            {
                RuleFor(x => x.Name).NotEmpty();
            }
        }

        [Fact]
        public void DocumentFilter_Should_Preserve_Unmodified_Ref_Properties_V2()
        {
            var (filter, _) = CreateDocumentFilter(null, new ParentWithChildValidator());

            var schemaRepository = new SchemaRepository();
            var schemaGenerator = TestExtensions.CreateSchemaGenerator();

            // Swashbuckle registers the body components BEFORE document filters run.
            schemaGenerator.GenerateSchema(typeof(ParentWithChild), schemaRepository);

            var doc = new OpenApiDocument
            {
                Paths = new OpenApiPaths
                {
                    ["/api/parent"] = new OpenApiPathItem
                    {
                        Operations = new Dictionary<System.Net.Http.HttpMethod, OpenApiOperation>
                        {
                            [System.Net.Http.HttpMethod.Post] = new OpenApiOperation(),
                        },
                    },
                },
            };
            var apiDescription = BodyApiDescription(typeof(ParentWithChild), "api/parent");

            filter.Apply(doc, new DocumentFilterContext(new[] { apiDescription }, schemaGenerator, schemaRepository));

            // Issue #198 parity: the untouched Child property must stay a $ref after rule application.
            var component = schemaRepository.GetSchema("ParentWithChild");
            component.Properties.Should().ContainKey("Child");
            component.Properties!["Child"].Should().BeOfType<OpenApiSchemaReference>(
                because: "properties not modified by rules must keep their $ref structure (#198)");
        }

        public enum InvestigationEnum
        {
            First,
            Second,
        }

        /// <summary>
        /// ADR-007 Phase 3 investigation test (gap 7): pins whether Swashbuckle emits
        /// OpenApiSchemaReference parameter schemas on the OPENAPI_V2 target. If this test starts
        /// failing after a Swashbuckle upgrade, re-evaluate the repository-resolution enhancement
        /// (which per ADR-007 must then be added to BOTH pipelines via a shared helper).
        /// </summary>
        [Fact]
        public void Investigation_Enum_Schema_Is_Emitted_As_Reference_V2()
        {
            var schemaRepository = new SchemaRepository();
            var schemaGenerator = TestExtensions.CreateSchemaGenerator();

            var schema = schemaGenerator.GenerateSchema(typeof(InvestigationEnum), schemaRepository);

            // Documents the current Swashbuckle behavior: enums become components referenced via $ref,
            // so $ref-typed parameter schemas DO occur in practice. Both pipelines currently skip them
            // in the copy-back (see ADR-007 gap 7) — this test pins the scenario for the future
            // both-pipelines repository-resolution enhancement.
            schema.Should().BeOfType<OpenApiSchemaReference>();
            schemaRepository.Schemas.Should().ContainKey("InvestigationEnum");
        }
#endif
    }
}
