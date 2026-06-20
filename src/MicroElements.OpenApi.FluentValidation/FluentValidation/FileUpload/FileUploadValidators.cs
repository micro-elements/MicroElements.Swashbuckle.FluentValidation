// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using FluentValidation;
using FluentValidation.Validators;
using Microsoft.AspNetCore.Http;

namespace MicroElements.OpenApi.FluentValidation.FileUpload
{
    /// <summary>
    /// Validates that an uploaded <see cref="IFormFile"/> has one of the allowed media (content) types.
    /// A <c>null</c> file passes (composes with <c>NotNull()</c>); enforcement mirrors the documented constraint.
    /// </summary>
    /// <typeparam name="T">Validated object type.</typeparam>
    public sealed class FileContentTypeValidator<T> : PropertyValidator<T, IFormFile>, IFileContentTypeValidator
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FileContentTypeValidator{T}"/> class.
        /// </summary>
        /// <param name="allowedContentTypes">Allowed media types (already normalized and non-empty).</param>
        public FileContentTypeValidator(IReadOnlyList<string> allowedContentTypes)
        {
            AllowedContentTypes = allowedContentTypes;
        }

        /// <inheritdoc />
        public IReadOnlyList<string> AllowedContentTypes { get; }

        /// <inheritdoc />
        public override string Name => "FileContentTypeValidator";

        /// <inheritdoc />
        public override bool IsValid(ValidationContext<T> context, IFormFile value)
        {
            if (value is null)
                return true;

            foreach (var allowed in AllowedContentTypes)
            {
                if (string.Equals(allowed, value.ContentType, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            context.MessageFormatter.AppendArgument("AllowedContentTypes", string.Join(", ", AllowedContentTypes));
            return false;
        }

        /// <inheritdoc />
        protected override string GetDefaultMessageTemplate(string errorCode)
            => "'{PropertyName}' must be one of the allowed content types: {AllowedContentTypes}.";
    }

    /// <summary>
    /// Validates that an uploaded <see cref="IFormFile"/> size (in bytes) is within the configured bounds.
    /// A <c>null</c> file passes (composes with <c>NotNull()</c>).
    /// </summary>
    /// <typeparam name="T">Validated object type.</typeparam>
    public sealed class FileSizeValidator<T> : PropertyValidator<T, IFormFile>, IFileSizeValidator
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FileSizeValidator{T}"/> class.
        /// </summary>
        /// <param name="minSizeBytes">Minimum size in bytes, or <c>null</c>.</param>
        /// <param name="maxSizeBytes">Maximum size in bytes, or <c>null</c>.</param>
        public FileSizeValidator(long? minSizeBytes, long? maxSizeBytes)
        {
            MinSizeBytes = minSizeBytes;
            MaxSizeBytes = maxSizeBytes;
        }

        /// <inheritdoc />
        public long? MinSizeBytes { get; }

        /// <inheritdoc />
        public long? MaxSizeBytes { get; }

        /// <inheritdoc />
        public override string Name => "FileSizeValidator";

        /// <inheritdoc />
        public override bool IsValid(ValidationContext<T> context, IFormFile value)
        {
            if (value is null)
                return true;

            if (MinSizeBytes is { } min && value.Length < min)
            {
                context.MessageFormatter.AppendArgument("MinSizeBytes", min);
                return false;
            }

            if (MaxSizeBytes is { } max && value.Length > max)
            {
                context.MessageFormatter.AppendArgument("MaxSizeBytes", max);
                return false;
            }

            return true;
        }

        /// <inheritdoc />
        protected override string GetDefaultMessageTemplate(string errorCode)
            => "'{PropertyName}' has an invalid file size.";
    }
}
