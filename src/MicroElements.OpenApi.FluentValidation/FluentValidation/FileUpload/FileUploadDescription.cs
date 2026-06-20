// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Globalization;

namespace MicroElements.OpenApi.FluentValidation.FileUpload
{
    /// <summary>
    /// Builds the human-readable description notes emitted for file-upload constraints, so every OpenAPI
    /// backend (Swashbuckle, NSwag, Microsoft.AspNetCore.OpenApi) produces identical wording.
    /// </summary>
    public static class FileUploadDescription
    {
        /// <summary>
        /// Formats the file-size note, or returns <c>null</c> when no bounds are configured.
        /// </summary>
        /// <param name="meta">File size metadata.</param>
        /// <returns>A note such as <c>"Maximum file size: 2097152 bytes."</c>, or <c>null</c>.</returns>
        public static string? FormatSizeNote(IFileSizeValidator meta)
        {
            long? min = meta.MinSizeBytes;
            long? max = meta.MaxSizeBytes;

            if (min is { } minValue && max is { } maxValue)
                return $"File size must be between {Format(minValue)} and {Format(maxValue)} bytes.";

            if (max is { } onlyMax)
                return $"Maximum file size: {Format(onlyMax)} bytes.";

            if (min is { } onlyMin)
                return $"Minimum file size: {Format(onlyMin)} bytes.";

            return null;
        }

        /// <summary>
        /// Formats the allowed-content-types note.
        /// </summary>
        /// <param name="meta">Content type metadata.</param>
        /// <returns>A note such as <c>"Allowed content types: image/jpeg, image/png."</c>.</returns>
        public static string FormatContentTypeNote(IFileContentTypeValidator meta)
            => $"Allowed content types: {string.Join(", ", meta.AllowedContentTypes)}.";

        /// <summary>
        /// Appends <paramref name="note"/> to an existing description, idempotently (a note already present
        /// is not duplicated). Preserves any user-authored description text.
        /// </summary>
        /// <param name="existing">Existing description (may be <c>null</c>).</param>
        /// <param name="note">Note to append.</param>
        /// <returns>The combined description.</returns>
        public static string Append(string? existing, string note)
        {
            if (string.IsNullOrEmpty(existing))
                return note;

            if (existing.Contains(note, StringComparison.Ordinal))
                return existing;

            return existing + " " + note;
        }

        private static string Format(long bytes) => bytes.ToString(CultureInfo.InvariantCulture);
    }
}
