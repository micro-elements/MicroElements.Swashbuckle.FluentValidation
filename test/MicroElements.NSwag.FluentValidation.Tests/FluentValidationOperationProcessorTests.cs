// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using FluentAssertions;
using FluentValidation;
using MicroElements.OpenApi.FluentValidation;
using MicroElements.OpenApi.FluentValidation.FileUpload;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NJsonSchema;
using NJsonSchema.Generation;
using NSwag;
using NSwag.Generation;
using NSwag.Generation.Processors.Contexts;
using Xunit;

namespace MicroElements.NSwag.FluentValidation.Tests
{
    /// <summary>
    /// Issue #216: NSwag emits multipart/form-data file content types via the operation processor.
    /// https://github.com/micro-elements/MicroElements.Swashbuckle.FluentValidation/issues/216
    /// </summary>
    public class FluentValidationOperationProcessorTests
    {
        public class UploadProductImageRequest
        {
            [FromForm(Name = "File")]
            public IFormFile File { get; set; } = default!;
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

        // The processor inspects the action method parameters to resolve the form container type.
        public static void Upload([FromForm] UploadProductImageRequest request)
        {
        }

        private static (OperationProcessorContext Context, OpenApiMediaType MediaType) BuildContext(string contentTypeKey)
        {
            var fileSchema = new JsonSchemaProperty { Type = JsonObjectType.String, Format = "binary" };
            var formSchema = new JsonSchema { Type = JsonObjectType.Object };
            formSchema.Properties["File"] = fileSchema;

            var mediaType = new OpenApiMediaType { Schema = formSchema };

            var operation = new OpenApiOperation { RequestBody = new OpenApiRequestBody() };
            operation.RequestBody.Content[contentTypeKey] = mediaType;

            var document = new OpenApiDocument();
            var settings = new OpenApiDocumentGeneratorSettings();
            var resolver = new JsonSchemaResolver(document, settings.SchemaSettings);
            var generator = new OpenApiDocumentGenerator(settings, resolver);
            var operationDescription = new OpenApiOperationDescription { Operation = operation };
            var methodInfo = typeof(FluentValidationOperationProcessorTests).GetMethod(nameof(Upload))!;

            var context = new OperationProcessorContext(
                document,
                operationDescription,
                typeof(FluentValidationOperationProcessorTests),
                methodInfo,
                generator,
                resolver,
                settings,
                new List<OpenApiOperationDescription>());

            return (context, mediaType);
        }

        private static FluentValidationOperationProcessor CreateProcessor(params IValidator[] validators)
        {
            var options = Options.Create(new SchemaGenerationOptions());
            var validatorRegistry = new ValidatorRegistry(validators, options);
            return new FluentValidationOperationProcessor(validatorRegistry: validatorRegistry, schemaGenerationOptions: options);
        }

        [Fact]
        public void FileContentType_Emits_Encoding_For_File_Part()
        {
            var (context, mediaType) = BuildContext("multipart/form-data");

            CreateProcessor(new UploadProductImageRequestValidator()).Process(context);

            mediaType.Encoding.Should().ContainKey("File");
            // NSwag serializes EncodingType as the "encodingType" JSON field (a known NSwag limitation);
            // the value still carries the comma-joined allowed media types.
            mediaType.Encoding["File"].EncodingType.Should().Be("image/jpeg,image/png");
        }

        [Fact]
        public void Urlencoded_Body_Is_Not_Given_Encoding()
        {
            var (context, mediaType) = BuildContext("application/x-www-form-urlencoded");

            CreateProcessor(new UploadProductImageRequestValidator()).Process(context);

            mediaType.Encoding.Should().BeEmpty();
        }

        [Fact]
        public void No_FileContentType_Rule_Emits_No_Encoding()
        {
            var validator = new InlineValidator<UploadProductImageRequest>();
            validator.RuleFor(x => x.File).NotNull();

            var (context, mediaType) = BuildContext("multipart/form-data");

            CreateProcessor(validator).Process(context);

            mediaType.Encoding.Should().BeEmpty();
        }
    }
}
