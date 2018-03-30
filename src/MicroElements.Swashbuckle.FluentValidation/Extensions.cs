using System;

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
            if (inputString.Length < 2) return inputString.ToUpper();
            return inputString.Substring(0, 1).ToUpper() + inputString.Substring(1);
        }
    }
}