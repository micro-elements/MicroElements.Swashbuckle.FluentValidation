// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace MicroElements.OpenApi.FluentValidation.FileUpload
{
    /// <summary>
    /// Implemented by property validators that restrict the allowed media (content) types of an uploaded file.
    /// The OpenAPI layer reads <see cref="AllowedContentTypes"/> to emit <c>encoding.&lt;part&gt;.contentType</c>
    /// for a <c>multipart/form-data</c> request body.
    /// </summary>
    public interface IFileContentTypeValidator
    {
        /// <summary>
        /// Gets the allowed media types (e.g. <c>image/jpeg</c>, <c>image/png</c>).
        /// </summary>
        IReadOnlyList<string> AllowedContentTypes { get; }
    }

    /// <summary>
    /// Implemented by property validators that restrict the size (in bytes) of an uploaded file.
    /// The OpenAPI layer reads the limits to emit a human-readable description and the
    /// <c>x-fileSizeBytes</c> vendor extension on the file property (OpenAPI has no standard byte-size keyword).
    /// </summary>
    public interface IFileSizeValidator
    {
        /// <summary>
        /// Gets the minimum allowed file size in bytes, or <c>null</c> when unbounded.
        /// </summary>
        long? MinSizeBytes { get; }

        /// <summary>
        /// Gets the maximum allowed file size in bytes, or <c>null</c> when unbounded.
        /// </summary>
        long? MaxSizeBytes { get; }
    }
}
