// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using Microsoft.AspNetCore.Http;

namespace MicroElements.OpenApi.FluentValidation.FileUpload
{
    /// <summary>
    /// FluentValidation rule builder extensions for validating <see cref="IFormFile"/> uploads.
    /// These rules both enforce validation at runtime and surface metadata for OpenAPI generation
    /// (allowed media types as <c>encoding.contentType</c>; size limits as a description / vendor extension).
    /// </summary>
    public static class FileUploadValidatorExtensions
    {
        /// <summary>
        /// Restricts the allowed media types of the uploaded file.
        /// Emits <c>encoding.&lt;part&gt;.contentType</c> on supported OpenAPI backends.
        /// </summary>
        /// <typeparam name="T">Validated object type.</typeparam>
        /// <param name="ruleBuilder">Rule builder.</param>
        /// <param name="allowedContentTypes">Allowed media types (e.g. <c>image/jpeg</c>, <c>image/png</c>).</param>
        /// <returns>Rule builder options for chaining.</returns>
        public static IRuleBuilderOptions<T, IFormFile> FileContentType<T>(
            this IRuleBuilder<T, IFormFile> ruleBuilder,
            params string[] allowedContentTypes)
            => ruleBuilder.FileContentType((IEnumerable<string>)allowedContentTypes);

        /// <summary>
        /// Restricts the allowed media types of the uploaded file.
        /// Emits <c>encoding.&lt;part&gt;.contentType</c> on supported OpenAPI backends.
        /// </summary>
        /// <typeparam name="T">Validated object type.</typeparam>
        /// <param name="ruleBuilder">Rule builder.</param>
        /// <param name="allowedContentTypes">Allowed media types.</param>
        /// <returns>Rule builder options for chaining.</returns>
        public static IRuleBuilderOptions<T, IFormFile> FileContentType<T>(
            this IRuleBuilder<T, IFormFile> ruleBuilder,
            IEnumerable<string> allowedContentTypes)
        {
            if (allowedContentTypes is null)
                throw new ArgumentNullException(nameof(allowedContentTypes));

            var normalized = allowedContentTypes
                .Where(contentType => !string.IsNullOrWhiteSpace(contentType))
                .Select(contentType => contentType.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (normalized.Length == 0)
                throw new ArgumentException("At least one non-empty content type must be specified.", nameof(allowedContentTypes));

            return ruleBuilder.SetValidator(new FileContentTypeValidator<T>(normalized));
        }

        /// <summary>
        /// Restricts the maximum size (in bytes) of the uploaded file.
        /// Emits a description and the <c>x-fileSizeBytes</c> vendor extension (annotation only — not validated by consumers).
        /// </summary>
        /// <typeparam name="T">Validated object type.</typeparam>
        /// <param name="ruleBuilder">Rule builder.</param>
        /// <param name="maxBytes">Maximum size in bytes.</param>
        /// <returns>Rule builder options for chaining.</returns>
        public static IRuleBuilderOptions<T, IFormFile> MaxFileSize<T>(
            this IRuleBuilder<T, IFormFile> ruleBuilder,
            long maxBytes)
            => ruleBuilder.SetValidator(new FileSizeValidator<T>(minSizeBytes: null, maxSizeBytes: maxBytes));

        /// <summary>
        /// Restricts the minimum size (in bytes) of the uploaded file.
        /// </summary>
        /// <typeparam name="T">Validated object type.</typeparam>
        /// <param name="ruleBuilder">Rule builder.</param>
        /// <param name="minBytes">Minimum size in bytes.</param>
        /// <returns>Rule builder options for chaining.</returns>
        public static IRuleBuilderOptions<T, IFormFile> MinFileSize<T>(
            this IRuleBuilder<T, IFormFile> ruleBuilder,
            long minBytes)
            => ruleBuilder.SetValidator(new FileSizeValidator<T>(minSizeBytes: minBytes, maxSizeBytes: null));

        /// <summary>
        /// Restricts the size (in bytes) of the uploaded file to the inclusive range <c>[minBytes, maxBytes]</c>.
        /// </summary>
        /// <typeparam name="T">Validated object type.</typeparam>
        /// <param name="ruleBuilder">Rule builder.</param>
        /// <param name="minBytes">Minimum size in bytes.</param>
        /// <param name="maxBytes">Maximum size in bytes.</param>
        /// <returns>Rule builder options for chaining.</returns>
        public static IRuleBuilderOptions<T, IFormFile> FileSizeBetween<T>(
            this IRuleBuilder<T, IFormFile> ruleBuilder,
            long minBytes,
            long maxBytes)
        {
            if (maxBytes < minBytes)
                throw new ArgumentException($"maxBytes ({maxBytes}) must be greater than or equal to minBytes ({minBytes}).", nameof(maxBytes));

            return ruleBuilder.SetValidator(new FileSizeValidator<T>(minSizeBytes: minBytes, maxSizeBytes: maxBytes));
        }
    }
}
