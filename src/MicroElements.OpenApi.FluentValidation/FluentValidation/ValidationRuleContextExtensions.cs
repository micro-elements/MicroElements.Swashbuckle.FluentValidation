// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MicroElements.OpenApi.FluentValidation
{
    /// <summary>
    /// <see cref="ValidationRuleContext"/> extensions.
    /// </summary>
    public static class ValidationRuleContextExtensions
    {
        public static bool IsCollectionRule(this ValidationRuleContext ruleContext)
        {
            // CollectionPropertyRule<T, TElement> is also a PropertyRule.
            return ruleContext.ValidationRule.GetType().Name.StartsWith("CollectionPropertyRule");
        }

        public static ReflectionContext? GetReflectionContext(this ValidationRuleContext ruleContext)
        {
            var ruleMember = ruleContext.ValidationRule.Member;
            return ruleMember != null ? new ReflectionContext(type: ruleMember.ReflectedType, propertyInfo: ruleMember) : null;
        }
    }
}