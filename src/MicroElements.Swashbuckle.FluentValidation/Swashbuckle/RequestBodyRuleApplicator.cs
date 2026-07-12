// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using MicroElements.OpenApi;
using MicroElements.OpenApi.Core;
using MicroElements.OpenApi.FluentValidation;
using MicroElements.OpenApi.FluentValidation.FileUpload;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.Logging;
#if !OPENAPI_V2
using Microsoft.OpenApi.Models;
#endif
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MicroElements.Swashbuckle.FluentValidation
{
    /// <summary>
    /// Issue #216 request-body logic ([FromForm] rules and <c>encoding.contentType</c>) shared by
    /// <see cref="FluentValidationOperationFilter"/> and <see cref="FluentValidationDocumentFilter"/>,
    /// so the two pipelines cannot drift.
    /// </summary>
    internal sealed class RequestBodyRuleApplicator
    {
        private readonly ILogger _logger;
        private readonly IValidatorRegistry _validatorRegistry;
        private readonly IReadOnlyList<IFluentValidationRule<OpenApiSchema>> _rules;
        private readonly SchemaGenerationOptions _schemaGenerationOptions;

        /// <summary>
        /// Initializes a new instance of the <see cref="RequestBodyRuleApplicator"/> class.
        /// </summary>
        public RequestBodyRuleApplicator(
            ILogger logger,
            IValidatorRegistry validatorRegistry,
            IReadOnlyList<IFluentValidationRule<OpenApiSchema>> rules,
            SchemaGenerationOptions schemaGenerationOptions)
        {
            _logger = logger;
            _validatorRegistry = validatorRegistry;
            _rules = rules;
            _schemaGenerationOptions = schemaGenerationOptions;
        }

        /// <summary>
        /// Applies FluentValidation rules to the operation's request body ([FromForm] content types)
        /// and emits <c>encoding.contentType</c> for restricted file parts (Issue #216).
        /// </summary>
        public void ApplyRulesToRequestBody(
            OpenApiOperation operation,
            ApiDescription apiDescription,
            SchemaRepository schemaRepository,
            ISchemaGenerator schemaGenerator,
            SwashbuckleSchemaProvider schemaProvider)
        {
#if OPENAPI_V2
            var requestBody = operation.RequestBody as OpenApiRequestBody;
#else
            var requestBody = operation.RequestBody;
#endif
            if (requestBody?.Content == null)
                return;

            // Content types used by [FromForm] attribute
            var formContentTypes = new[] { "multipart/form-data", "application/x-www-form-urlencoded" };

            foreach (var contentType in requestBody.Content)
            {
                if (!formContentTypes.Contains(contentType.Key, StringComparer.OrdinalIgnoreCase))
                    continue;

#if OPENAPI_V2
                var rawSchema = contentType.Value.Schema;
                var contentSchema = rawSchema as OpenApiSchema;
                string? schemaRefId = rawSchema is OpenApiSchemaReference schemaRef ? schemaRef.Reference?.Id : null;
#else
                var contentSchema = contentType.Value.Schema;
                string? schemaRefId = contentSchema?.Reference?.Id;
#endif
                if (contentSchema == null)
                    continue;

                // Find the parameter type from ApiDescription
                var bodyParameter = apiDescription.ParameterDescriptions
                    .FirstOrDefault(p => p.Source?.Id == "Form" || p.Source?.Id == "Body");

                Type? parameterType = null;
                if (bodyParameter != null)
                {
                    parameterType = bodyParameter.ModelMetadata?.ContainerType ?? bodyParameter.ModelMetadata?.ModelType;
                }

                // If we couldn't find it from body parameter, try to find from schema reference
                if (parameterType == null && schemaRefId != null)
                {
                    parameterType = apiDescription.ParameterDescriptions
                        .Select(p => p.ModelMetadata?.ModelType)
                        .FirstOrDefault(t => t != null && _schemaGenerationOptions.SchemaIdSelector(t) == schemaRefId);
                }

                if (parameterType == null)
                    continue;

                var validator = _validatorRegistry.GetValidator(parameterType);
                if (validator == null)
                    continue;

                // Resolve the actual schema (dereference if needed)
                OpenApiSchema resolvedSchema = contentSchema;
                if (schemaRefId != null)
                {
                    resolvedSchema = schemaProvider.GetSchemaForType(parameterType);
                }

                if (resolvedSchema.Properties == null || resolvedSchema.Properties.Count == 0)
                    continue;

                var schemaContext = new SchemaGenerationContext(
                    schemaRepository: schemaRepository,
                    schemaGenerator: schemaGenerator,
                    schema: resolvedSchema,
                    schemaType: parameterType,
                    rules: _rules,
                    schemaGenerationOptions: _schemaGenerationOptions,
                    schemaProvider: schemaProvider);

                // Apply validation rules to all properties
                FluentValidationSchemaBuilder.ApplyRulesToSchema(
                    schemaType: parameterType,
                    schemaPropertyNames: schemaContext.Properties,
                    validator: validator,
                    logger: _logger,
                    schemaGenerationContext: schemaContext);

                // Issue #216: emit encoding.contentType for IFormFile parts restricted via .FileContentType(...).
                // Only multipart/form-data carries per-part media types (application/x-www-form-urlencoded does not).
                if (string.Equals(contentType.Key, "multipart/form-data", StringComparison.OrdinalIgnoreCase))
                {
                    ApplyFileContentTypeEncoding(contentType.Value, resolvedSchema, parameterType, validator, schemaRepository);
                }
            }
        }

        /// <summary>
        /// Issue #216: writes <c>encoding.&lt;part&gt;.contentType</c> for every binary file part that a
        /// <c>.FileContentType(...)</c> rule restricts. Part keys are taken verbatim from the rendered schema and
        /// matched to the rule name-insensitively. Content types reach this method via the SAME filtered rule
        /// traversal the schema pipeline uses, so a conditional rule is included/excluded consistently.
        /// </summary>
        private void ApplyFileContentTypeEncoding(
            OpenApiMediaType mediaType,
            OpenApiSchema resolvedSchema,
            Type parameterType,
            IValidator validator,
            SchemaRepository schemaRepository)
        {
            if (resolvedSchema.Properties == null || resolvedSchema.Properties.Count == 0)
                return;

            var contentTypeRules = FileUploadIntrospection
                .GetFileContentTypeValidators(validator, parameterType, _schemaGenerationOptions)
                .ToList();
            if (contentTypeRules.Count == 0)
                return;

            foreach (var partKey in resolvedSchema.Properties.Keys)
            {
                var partSchema = OpenApiSchemaCompatibility.GetProperty(resolvedSchema, partKey, schemaRepository);
                if (partSchema == null || !OpenApiSchemaCompatibility.IsBinaryFormat(partSchema))
                    continue;

                var allowed = contentTypeRules
                    .Where(rule => rule.MemberName.EqualsIgnoreAll(partKey))
                    .Select(rule => rule.Meta.AllowedContentTypes)
                    .FirstOrDefault();
                if (allowed == null || allowed.Count == 0)
                    continue;

                mediaType.Encoding ??= new Dictionary<string, OpenApiEncoding>();
                mediaType.Encoding[partKey] = new OpenApiEncoding { ContentType = string.Join(", ", allowed) };
            }
        }
    }
}
