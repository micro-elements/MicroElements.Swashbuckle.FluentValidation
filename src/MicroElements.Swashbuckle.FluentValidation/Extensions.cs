using System;
using System.Collections.Generic;

namespace MicroElements.Swashbuckle.FluentValidation
{
    /// <summary>
    /// Extensions for some swagger specific work.
    /// </summary>
    public static class Extensions
    {
        /// <summary>
        /// Is supported swagger numeric type.
        /// </summary>
        public static bool IsNumeric(this object value) => value is int || value is long || value is float || value is double;

        /// <summary>
        /// Convert numeric to int.
        /// </summary>
        public static int NumericToInt(this object value) => Convert.ToInt32(value);

        /// <summary>
        /// Converts string to CamelCase to match .net standard naming conventions.
        /// </summary>
        /// <param name="inputString">Input string.</param>
        /// <returns>CamelCase variant of input string.</returns>
        public static string ToCamelCase(this string inputString)
        {
            if (inputString == null) return null;
            if (inputString == string.Empty) return string.Empty;
            if (char.IsUpper(inputString[0])) return inputString;
            return inputString.Substring(0, 1).ToUpper() + inputString.Substring(1);
        }

        /// <summary>
        /// Converts string to lowerCamelCase.
        /// </summary>
        /// <param name="inputString">Input string.</param>
        /// <returns>lowerCamelCase string.</returns>
        public static string ToLowerCamelCase(this string inputString)
        {
            if (inputString == null) return null;
            if (inputString == string.Empty) return string.Empty;
            if (char.IsLower(inputString[0])) return inputString;
            return inputString.Substring(0, 1).ToLower() + inputString.Substring(1);
        }

        public static Dictionary<TKey, TValue> NotNull<TKey, TValue>(this Dictionary<TKey, TValue> dictionary)
        {
            return dictionary ?? new Dictionary<TKey, TValue>();
        }
    }
}