// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace MicroElements.OpenApi.FluentValidation
{
    /// <summary>
    /// Combines multiple regular expressions into a single OpenAPI <c>pattern</c>.
    /// </summary>
    /// <remarks>
    /// OpenAPI/JSON Schema allows only one <c>pattern</c> per schema, so multiple
    /// <c>.Matches()</c> rules on one property cannot be expressed as separate keywords.
    /// This combiner joins them into a single regular expression using lookahead
    /// assertions, which keeps the FluentValidation <c>.Matches()</c> semantics
    /// (<see cref="System.Text.RegularExpressions.Regex.IsMatch(string, string)"/> — "contains a match")
    /// and renders correctly in Swagger UI, Redoc and Scalar.
    /// </remarks>
    public static class PatternCombiner
    {
        /// <summary>
        /// Opening part of a lookahead produced by <see cref="Wrap"/>.
        /// Used to detect a pattern already combined by this class.
        /// The full prefix (not just <c>"(?="</c>) is matched on purpose, so a user-supplied
        /// pattern that merely starts with a lookahead is not mistaken for combiner output.
        /// </summary>
        private const string LookaheadPrefix = "(?=[\\s\\S]*(?:";

        /// <summary>
        /// Combines an existing <c>pattern</c> with an additional regular expression.
        /// </summary>
        /// <param name="existing">The current <c>pattern</c> value, or <see langword="null"/> if none was set.</param>
        /// <param name="newPattern">The additional regular expression to combine.</param>
        /// <returns>
        /// <paramref name="newPattern"/> as is when there is no existing pattern;
        /// otherwise a single regular expression that requires both to match.
        /// </returns>
        public static string Combine(string? existing, string newPattern)
        {
            if (string.IsNullOrEmpty(existing))
                return newPattern;

            // 'existing' was already combined by this method: just append a new lookahead.
            if (existing!.StartsWith(LookaheadPrefix, StringComparison.Ordinal))
                return existing + Wrap(newPattern);

            // 'existing' is a plain single pattern: wrap both into lookaheads.
            return Wrap(existing) + Wrap(newPattern);
        }

        private static string Wrap(string pattern) => $"{LookaheadPrefix}{pattern}))";
    }
}
