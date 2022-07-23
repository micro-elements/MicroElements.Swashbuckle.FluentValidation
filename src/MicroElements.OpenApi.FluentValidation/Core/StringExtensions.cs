// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MicroElements.OpenApi.Core
{
    internal static class StringExtensions
    {
        /// <summary>
        /// Converts string to lowerCamelCase.
        /// </summary>
        /// <param name="inputString">Input string.</param>
        /// <returns>lowerCamelCase string.</returns>
        internal static string? ToLowerCamelCase(this string? inputString)
        {
            if (inputString == null) return null;
            if (inputString == string.Empty) return string.Empty;
            if (char.IsLower(inputString[0])) return inputString;
            return inputString.Substring(0, 1).ToLower() + inputString.Substring(1);
        }

        /// <summary>
        /// Returns string equality only by symbols ignore case.
        /// It can be used for comparing camelCase, PascalCase, snake_case, kebab-case identifiers.
        /// </summary>
        /// <param name="left">Left string to compare.</param>
        /// <param name="right">Right string to compare.</param>
        /// <returns><c>true</c> if input strings are equals in terms of identifier formatting.</returns>
        internal static bool EqualsIgnoreAll(this string left, string right)
        {
            return IgnoreAllStringComparer.Instance.Equals(left, right);
        }
    }
}