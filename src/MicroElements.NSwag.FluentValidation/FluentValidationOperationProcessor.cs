// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using MicroElements.OpenApi.Core;
using MicroElements.OpenApi.FluentValidation;
using MicroElements.OpenApi.FluentValidation.FileUpload;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NJsonSchema;
using NSwag;
using NSwag.Generation.Processors;
using NSwag.Generation.Processors.Contexts;

namespace MicroElements.NSwag.FluentValidation
{
    /// <summary>
    /// NSwag <see cref="IOperationProcessor"/> that emits <c>multipart/form-data</c> encoding for
    /// <c>IFormFile</c> parts restricted via <c>.FileContentType(...)</c>.
    /// <para>
    /// Note: NSwag's <c>OpenApiEncoding.EncodingType</c> serializes as <c>encodingType</c> rather than the
    /// OpenAPI-spec <c>contentType</c> (a known NSwag limitation through at least 14.7.x). The same allowed
    /// content types are also written to the file part's description by the schema rule, guaranteeing the
    /// information is visible regardless of the encoding key name.
    /// </para>
    /// </summary>
    public class FluentValidationOperationProcessor : IOperationProcessor
    {
        private const string MultipartContentType = "multipart/form-data";

        private readonly ILogger _logger;
        private readonly IValidatorRegistry? _validatorRegistry;
        private readonly SchemaGenerationOptions _schemaGenerationOptions;

        /// <summary>
        /// Initializes a new instance of the <see cref="FluentValidationOperationProcessor"/> class.
        /// </summary>
        /// <param name="loggerFactory"><see cref="ILoggerFactory"/> for logging. Can be null.</param>
        /// <param name="validatorRegistry">Gets validators for a particular type.</param>
        /// <param name="schemaGenerationOptions">Schema generation options.</param>
        public FluentValidationOperationProcessor(
            ILoggerFactory? loggerFactory = null,
            IValidatorRegistry? validatorRegistry = null,
            IOptions<SchemaGenerationOptions>? schemaGenerationOptions = null)
        {
            _logger = loggerFactory?.CreateLogger(typeof(FluentValidationOperationProcessor)) ?? NullLogger.Instance;
            _validatorRegistry = validatorRegistry;
            _schemaGenerationOptions = schemaGenerationOptions?.Value ?? new SchemaGenerationOptions();
        }

        /// <inheritdoc />
        public bool Process(OperationProcessorContext context)
        {
            try
            {
                ApplyEncoding(context);
            }
            catch (Exception e)
            {
                _logger.LogWarning(0, e, "Error applying FluentValidation file content types to operation.");
            }

            // Always keep the operation in the document.
            return true;
        }

        private void ApplyEncoding(OperationProcessorContext context)
        {
            if (_validatorRegistry == null || context.MethodInfo == null)
                return;

            var requestBody = context.OperationDescription.Operation.RequestBody;
            if (requestBody?.Content == null)
                return;

            if (!requestBody.Content.TryGetValue(MultipartContentType, out var media) || media?.Schema == null)
                return;

            var schema = media.Schema.ActualSchema;
            if (schema.ActualProperties == null || schema.ActualProperties.Count == 0)
                return;

            foreach (var parameter in context.MethodInfo.GetParameters())
            {
                var validator = _validatorRegistry.GetValidator(parameter.ParameterType);
                if (validator == null)
                    continue;

                var contentTypeRules = FileUploadIntrospection
                    .GetFileContentTypeValidators(validator, parameter.ParameterType, _schemaGenerationOptions)
                    .ToList();
                if (contentTypeRules.Count == 0)
                    continue;

                foreach (var part in schema.ActualProperties)
                {
                    var partSchema = part.Value.ActualSchema;
                    if (!partSchema.Type.HasFlag(JsonObjectType.String) || partSchema.Format != "binary")
                        continue;

                    var allowed = contentTypeRules
                        .Where(rule => rule.MemberName.EqualsIgnoreAll(part.Key))
                        .Select(rule => rule.Meta.AllowedContentTypes)
                        .FirstOrDefault();
                    if (allowed == null || allowed.Count == 0)
                        continue;

                    if (!media.Encoding.TryGetValue(part.Key, out var encoding) || encoding == null)
                        media.Encoding[part.Key] = encoding = new OpenApiEncoding();

                    encoding.EncodingType = string.Join(",", allowed);
                }
            }
        }
    }
}
