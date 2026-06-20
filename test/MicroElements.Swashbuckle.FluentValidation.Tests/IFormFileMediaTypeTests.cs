// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using FluentValidation;
using MicroElements.OpenApi.FluentValidation;
using MicroElements.OpenApi.FluentValidation.FileUpload;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
    /// Issue #216: media type (content type) and file size support for <see cref="IFormFile"/> uploads.
    /// https://github.com/micro-elements/MicroElements.Swashbuckle.FluentValidation/issues/216
    /// </summary>
    public class IFormFileMediaTypeTests : UnitTestBase
    {
        public class UploadProductImageRequest
        {
            [FromForm(Name = "File")]
            public IFormFile File { get; set; }
        }

        public class UploadProductImageRequestValidator : AbstractValidator<UploadProductImageRequest>
        {
            public UploadProductImageRequestValidator()
            {
                RuleFor(x => x.File)
                    .NotNull()
                    .FileContentType("image/jpeg", "image/png")
                    .MaxFileSize(2 * 1024 * 1024);
            }
        }

        // --- Reproduction of the original (buggy) behavior reported in the issue -----------------------------

        public class NestedMemberRulesRequest
        {
            public IFormFile File { get; set; }
        }

        public class NestedMemberRulesValidator : AbstractValidator<NestedMemberRulesRequest>
        {
            private static readonly string[] AllowedContentTypes = { "image/jpeg", "image/png" };

            public NestedMemberRulesValidator()
            {
                // This is exactly how the issue author wrote the rules. FluentValidation names these rules
                // "File.Length" / "File.ContentType", which never match the flat schema property "File", so they
                // are silently dropped. This test LOCKS that documented limitation: use the File-level API instead.
                RuleFor(x => x.File.Length).GreaterThan(0).LessThanOrEqualTo(2 * 1024 * 1024).When(x => x.File != null);
                RuleFor(x => x.File.ContentType).Must(AllowedContentTypes.Contains).When(x => x.File != null);
            }
        }

        [Fact]
        public void Reproduction_Nested_Member_Rules_Are_Silently_Ignored()
        {
            var schemaRepository = new SchemaRepository();
            var referenceSchema = SchemaGenerator(new NestedMemberRulesValidator())
                .GenerateSchema(typeof(NestedMemberRulesRequest), schemaRepository);

            var schema = schemaRepository.GetSchema(referenceSchema.GetRefId()!);
            var fileProperty = schema.GetProperty(nameof(NestedMemberRulesRequest.File))!;

            // The nested-member rules produce NOTHING in the document (the gap behind issue #216).
            fileProperty.Description.Should().BeNullOrEmpty();
            fileProperty.MaxLength.Should().BeNull();
        }

        // --- Schema-level output of the new File-level API (runs on every TFM) ------------------------------

        [Fact]
        public void FileContentType_And_MaxFileSize_Add_Description_To_File_Property()
        {
            var schemaRepository = new SchemaRepository();
            var referenceSchema = SchemaGenerator(new UploadProductImageRequestValidator())
                .GenerateSchema(typeof(UploadProductImageRequest), schemaRepository);

            var schema = schemaRepository.GetSchema(referenceSchema.GetRefId()!);
            var fileProperty = schema.GetProperty(nameof(UploadProductImageRequest.File))!;

            fileProperty.GetTypeString().Should().Be("string");
            fileProperty.Format.Should().Be("binary");
            fileProperty.Description.Should().Contain("image/jpeg, image/png");
            fileProperty.Description.Should().Contain("2097152");
            // File size must never be expressed as maxLength (that counts characters, not bytes).
            fileProperty.MaxLength.Should().BeNull();
        }

        [Fact]
        public void NotNull_Only_Does_Not_Emit_Size_Or_ContentType_Notes()
        {
            var validator = new InlineValidator<UploadProductImageRequest>();
            validator.RuleFor(x => x.File).NotNull();

            var schemaRepository = new SchemaRepository();
            var referenceSchema = SchemaGenerator(validator).GenerateSchema(typeof(UploadProductImageRequest), schemaRepository);
            var schema = schemaRepository.GetSchema(referenceSchema.GetRefId()!);
            var fileProperty = schema.GetProperty(nameof(UploadProductImageRequest.File))!;

            fileProperty.Description.Should().BeNullOrEmpty();
        }

        // --- Operation-level output (encoding.contentType). v1 (Swashbuckle 8/9) object model. ---------------
#if !OPENAPI_V2
        private static (OpenApiOperation Operation, OperationFilterContext Context, OpenApiMediaType MediaType) BuildMultipartOperation(
            string contentTypeKey,
            SchemaRepository schemaRepository,
            SchemaGenerator schemaGenerator,
            System.Reflection.MethodInfo methodInfo)
        {
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
                    Content = new Dictionary<string, OpenApiMediaType> { [contentTypeKey] = mediaType },
                },
            };

            var apiDescription = new ApiDescription();
            apiDescription.ParameterDescriptions.Add(new ApiParameterDescription
            {
                Name = "File",
                ModelMetadata = new EmptyModelMetadataProvider().GetMetadataForType(typeof(UploadProductImageRequest)),
                Source = BindingSource.Form,
            });

            var context = new OperationFilterContext(apiDescription, schemaGenerator, schemaRepository, methodInfo);
            return (operation, context, mediaType);
        }

        private static FluentValidationOperationFilter CreateOperationFilter(params IValidator[] validators)
        {
            var schemaGenerationOptions = new SchemaGenerationOptions
            {
                NameResolver = new Generation.SystemTextJsonNameResolver(),
                SchemaIdSelector = new SchemaGeneratorOptions().SchemaIdSelector,
            };

            var validatorRegistry = new ValidatorRegistry(
                validators,
                new OptionsWrapper<SchemaGenerationOptions>(schemaGenerationOptions));

            return new FluentValidationOperationFilter(
                validatorRegistry: validatorRegistry,
                schemaGenerationOptions: new OptionsWrapper<SchemaGenerationOptions>(schemaGenerationOptions));
        }

        [Fact]
        public void FileContentType_Emits_Encoding_ContentType()
        {
            var schemaRepository = new SchemaRepository();
            var schemaGenerator = SchemaGenerator(new UploadProductImageRequestValidator());
            var methodInfo = typeof(IFormFileMediaTypeTests).GetMethod(nameof(FileContentType_Emits_Encoding_ContentType))!;

            var (operation, context, mediaType) = BuildMultipartOperation("multipart/form-data", schemaRepository, schemaGenerator, methodInfo);

            CreateOperationFilter(new UploadProductImageRequestValidator()).Apply(operation, context);

            mediaType.Encoding.Should().ContainKey("File");
            mediaType.Encoding["File"].ContentType.Should().Be("image/jpeg,image/png");
        }

        [Fact]
        public void Single_ContentType_Emits_Single_Value_Without_Comma()
        {
            var validator = new InlineValidator<UploadProductImageRequest>();
            validator.RuleFor(x => x.File).NotNull().FileContentType("image/png");

            var schemaRepository = new SchemaRepository();
            var schemaGenerator = SchemaGenerator(validator);
            var methodInfo = typeof(IFormFileMediaTypeTests).GetMethod(nameof(Single_ContentType_Emits_Single_Value_Without_Comma))!;

            var (operation, context, mediaType) = BuildMultipartOperation("multipart/form-data", schemaRepository, schemaGenerator, methodInfo);

            CreateOperationFilter(validator).Apply(operation, context);

            mediaType.Encoding["File"].ContentType.Should().Be("image/png");
        }

        [Fact]
        public void No_FileContentType_Rule_Emits_No_Encoding()
        {
            var validator = new InlineValidator<UploadProductImageRequest>();
            validator.RuleFor(x => x.File).NotNull();

            var schemaRepository = new SchemaRepository();
            var schemaGenerator = SchemaGenerator(validator);
            var methodInfo = typeof(IFormFileMediaTypeTests).GetMethod(nameof(No_FileContentType_Rule_Emits_No_Encoding))!;

            var (operation, context, mediaType) = BuildMultipartOperation("multipart/form-data", schemaRepository, schemaGenerator, methodInfo);

            CreateOperationFilter(validator).Apply(operation, context);

            (mediaType.Encoding == null || mediaType.Encoding.Count == 0).Should().BeTrue();
        }

        [Fact]
        public void Urlencoded_Content_Is_Not_Polluted_With_Encoding()
        {
            var schemaRepository = new SchemaRepository();
            var schemaGenerator = SchemaGenerator(new UploadProductImageRequestValidator());
            var methodInfo = typeof(IFormFileMediaTypeTests).GetMethod(nameof(Urlencoded_Content_Is_Not_Polluted_With_Encoding))!;

            var (operation, context, mediaType) = BuildMultipartOperation("application/x-www-form-urlencoded", schemaRepository, schemaGenerator, methodInfo);

            CreateOperationFilter(new UploadProductImageRequestValidator()).Apply(operation, context);

            (mediaType.Encoding == null || mediaType.Encoding.Count == 0).Should().BeTrue();
        }
#endif
    }
}
