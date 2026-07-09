// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace MicroElements.OpenApi.Core
{
    /// <summary>
    /// Extensions for some swagger specific work.
    /// </summary>
    public static class Extensions
    {
        /// <summary>
        /// Is supported swagger numeric type.
        /// </summary>
        /// <remarks>
        /// Issue #222: all integer primitives must be recognized. `short`/`byte`/`ushort`/`uint`/`ulong`/`sbyte`
        /// bounds (e.g. from <c>InclusiveBetween((short)1, (short)99)</c>) were dropped because they were absent
        /// here, so <c>minimum</c>/<c>maximum</c> were never emitted. <see cref="NumericToDecimal"/> already
        /// converts them all via <see cref="Convert.ToDecimal(object)"/>.
        /// </remarks>
        internal static bool IsNumeric(this object value) =>
            value is sbyte or byte or short or ushort or int or uint or long or ulong
                or float or double or decimal or BigInteger;

        /// <summary>
        /// Convert numeric to decimal.
        /// </summary>
        internal static decimal NumericToDecimal(this object value) => value is BigInteger bigInt ? (decimal)bigInt : Convert.ToDecimal(value);

        /// <summary>
        /// Returns not null enumeration.
        /// </summary>
        internal static IEnumerable<TValue> NotNull<TValue>(this IEnumerable<TValue>? collection) =>
            collection ?? Array.Empty<TValue>();

        /// <summary>
        /// Skip simple types from schema generation.
        /// </summary>
        internal static bool IsPrimitiveType(this Type type) => type.IsPrimitive || type == typeof(string) || type == typeof(decimal);

        /// <summary>
        /// Returns array in debug mode and the same collection in release.
        /// </summary>
        internal static IEnumerable<TValue> ToArrayDebug<TValue>(this IEnumerable<TValue>? collection)
        {
#if DEBUG
            return collection?.ToArray() ?? Array.Empty<TValue>();
#else
            return collection;
#endif
        }
    }
}