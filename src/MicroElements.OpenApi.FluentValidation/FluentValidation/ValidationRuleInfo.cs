// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentValidation;

namespace MicroElements.OpenApi.FluentValidation
{
    /// <summary>
    /// Contains <see cref="PropertyRule"/> and additional info.
    /// </summary>
    public record ValidationRuleInfo
    {
        /// <summary>
        /// PropertyRule.
        /// </summary>
        public IValidationRule PropertyRule { get; init; }

        /// <summary>
        /// Flag indication whether the <see cref="PropertyRule"/> is the CollectionRule.
        /// </summary>
        public bool IsCollectionRule { get; init; }

        /// <summary>
        /// Gets reflection context. Can be null for composite rules.
        /// </summary>
        public ReflectionContext? ReflectionContext { get; init; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ValidationRuleInfo"/> class.
        /// </summary>
        /// <param name="propertyRule">PropertyRule.</param>
        /// <param name="isCollectionRule">Is a CollectionPropertyRule.</param>
        /// <param name="reflectionContext">Reflection context.</param>
        public ValidationRuleInfo(
            IValidationRule propertyRule,
            bool isCollectionRule,
            ReflectionContext? reflectionContext)
        {
            PropertyRule = propertyRule;
            IsCollectionRule = isCollectionRule;
            ReflectionContext = reflectionContext;
        }
    }
}