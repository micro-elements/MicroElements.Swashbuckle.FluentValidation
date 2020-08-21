using System;

namespace MicroElements.Swashbuckle.FluentValidation
{
    /// <summary>
    /// Returns string equality only by symbols ignore case.
    /// It can be used for comparing camelCase, PascalCase, snake_case, kebab-case identifiers.
    /// </summary>
    public class IgnoreAllStringComparer : StringComparer
    {
        public static readonly StringComparer Instance = new IgnoreAllStringComparer();

        /// <inheritdoc />
        public override int Compare(string left, string right)
        {
            int leftIndex = 0;
            int rightIndex = 0;
            int compare = 0;
            while (true)
            {
                left.GetNextSymbol(ref leftIndex, out char leftSymbol, toUpper: true);
                right.GetNextSymbol(ref rightIndex, out char rightSymbol, toUpper: true);

                compare = leftSymbol.CompareTo(rightSymbol);
                if (compare != 0 || leftIndex < 0 || rightIndex < 0)
                {
                    break;
                }
            }

            return compare;
        }

        /// <inheritdoc />
        public override bool Equals(string left, string right)
        {
            if (left == null || right == null)
                return false;

            int leftIndex = 0;
            int rightIndex = 0;
            bool equals;
            while (true)
            {
                bool hasLeftSymbol = left.GetNextSymbol(ref leftIndex, out char leftSymbol, toUpper: true);
                bool hasRightSymbol = right.GetNextSymbol(ref rightIndex, out char rightSymbol, toUpper: true);

                equals = leftSymbol == rightSymbol;
                if (!equals || !hasLeftSymbol || !hasRightSymbol)
                {
                    break;
                }
            }

            return equals;
        }

        /// <inheritdoc />
        public override int GetHashCode(string obj)
        {
            unchecked
            {
                int index = 0;
                int hash = 0;
                while (obj.GetNextSymbol(ref index, out char symbol))
                {
                    hash = 31 * hash + char.ToUpperInvariant(symbol).GetHashCode();
                }

                return hash;
            }
        }
    }

    internal static class StringUtils
    {
        public static bool GetNextSymbol(this string value, ref int startIndex, out char symbol, bool toUpper = true)
        {
            while (startIndex >= 0 && startIndex < value.Length)
            {
                var current = value[startIndex++];
                if (char.IsLetterOrDigit(current))
                {
                    symbol = toUpper ? char.ToUpperInvariant(current) : current;
                    return true;
                }
            }

            startIndex = -1;
            symbol = char.MinValue;
            return false;
        }
    }
}