// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using FluentValidation.Validators;

namespace MicroElements.OpenApi.FluentValidation
{
    internal static class RuleHistoryCache
    {
        private static ConditionalWeakTable<object, List<RuleCacheItem>> SchemaRuleHistory { get; } = new();

        internal record RuleCacheItem(
            string SchemaTypeName,
            string SchemaPropertyName,
            IPropertyValidator PropertyValidator,
            string RuleName)
        {
            public virtual bool Equals(RuleCacheItem? other)
            {
                return !(other is null) &&
                    SchemaTypeName == other.SchemaTypeName &&
                    SchemaPropertyName == other.SchemaPropertyName &&
                    PropertyValidatorComparer.Instance.Equals(PropertyValidator, other.PropertyValidator)
                    && RuleName == other.RuleName;
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    PropertyValidator.Equals(PropertyValidator);
                    var hashCode = SchemaTypeName.GetHashCode();
                    hashCode = (hashCode * 397) ^ SchemaPropertyName.GetHashCode();
                    hashCode = (hashCode * 397) ^ PropertyValidator.Name.GetHashCode();
                    hashCode = (hashCode * 397) ^ RuleName.GetHashCode();
                    return hashCode;
                }
            }
        }

        internal static void AddRuleHistoryItem(this object schema, RuleCacheItem historyItem)
        {
            List<RuleCacheItem> historyItems = SchemaRuleHistory.GetOrCreateValue(schema);
            historyItems.Add(historyItem);
        }

        internal static bool ContainsRuleHistoryItem(this object schema, RuleCacheItem historyItem)
        {
            List<RuleCacheItem> historyItems = SchemaRuleHistory.GetOrCreateValue(schema);
            return historyItems.FirstOrDefault(item => Equals(item, historyItem)) != null;
        }

        internal class PropertyValidatorComparer : IEqualityComparer<IPropertyValidator>
        {
            public static readonly PropertyValidatorComparer Instance = new PropertyValidatorComparer();

            /// <inheritdoc />
            public bool Equals(IPropertyValidator? x, IPropertyValidator? y)
            {
                if (ReferenceEquals(x, y))
                    return true;

                if (x == null || y == null)
                    return false;

                if (x.GetType() != y.GetType())
                    return false;

                // stateless validators
                if (x is INotNullValidator || x is INotEmptyValidator || x is IEmailValidator)
                    return true;

                if (x is IRegularExpressionValidator expressionValidator1 &&
                    y is IRegularExpressionValidator expressionValidator2)
                    return Equals(expressionValidator1.Expression, expressionValidator2.Expression);

                if (x is ILengthValidator lengthValidator1 &&
                    y is ILengthValidator lengthValidator2)
                    return lengthValidator1.Min == lengthValidator2.Min && lengthValidator1.Max == lengthValidator2.Max;

                if (x is IComparisonValidator comparisonValidator1 &&
                    y is IComparisonValidator comparisonValidator2)
                {
                    return comparisonValidator1.Comparison == comparisonValidator2.Comparison &&
                           Equals(comparisonValidator1.MemberToCompare, comparisonValidator2.MemberToCompare) &&
                           Equals(comparisonValidator1.ValueToCompare, comparisonValidator2.ValueToCompare);
                }

                if (x is IBetweenValidator betweenValidator1 &&
                    y is IBetweenValidator betweenValidator2)
                    return Equals(betweenValidator1.From, betweenValidator2.From) && Equals(betweenValidator1.To, betweenValidator2.To);

                return false;
            }

            /// <inheritdoc />
            public int GetHashCode(IPropertyValidator? obj)
            {
                return obj?.GetHashCode() ?? 0;
            }
        }
    }
}