// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
            string RuleName);

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
    }
}