// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;

namespace MicroElements.Swashbuckle.FluentValidation.AspNetCore
{
    /// <summary>
    /// AspNetCore Mvc wrapper that can be used in netstandard.
    /// </summary>
    public class AspNetJsonSerializerOptions
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AspNetJsonSerializerOptions"/> class.
        /// </summary>
        /// <param name="value"><see cref="JsonSerializerOptions"/> from AspNet host.</param>
        public AspNetJsonSerializerOptions(JsonSerializerOptions value)
        {
            Value = value;
        }

        /// <summary>
        /// Gets the <see cref="JsonSerializerOptions"/> from AspNet host.
        /// </summary>
        public JsonSerializerOptions Value { get; }
    }
}