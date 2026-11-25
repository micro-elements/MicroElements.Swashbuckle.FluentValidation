// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace MicroElements.OpenApi.FluentValidation
{
    /// <summary>
    /// String formats that schema generators emit for value types serialized as strings
    /// (Guid, DateTime, DateTimeOffset, DateOnly, TimeOnly, TimeSpan).
    /// </summary>
    internal static class ValueTypeStringFormats
    {
        private static readonly HashSet<string> Formats = new(StringComparer.OrdinalIgnoreCase)
        {
            "uuid", // Guid: Swashbuckle, Microsoft.AspNetCore.OpenApi
            "guid", // Guid: NJsonSchema
            "date-time",
            "date",
            "time",
            "duration", // TimeSpan: NJsonSchema
            "date-span", // TimeSpan: Swashbuckle
        };

        /// <summary>
        /// Returns <c>true</c> if the string schema describes a value type rather than text.
        /// NotEmpty on such a value means "not the default value", so string length keywords do not apply.
        /// </summary>
        /// <param name="format">Schema format.</param>
        /// <returns><c>true</c> for a value type format.</returns>
        internal static bool IsValueTypeFormat(string? format) => format != null && Formats.Contains(format);
    }
}
