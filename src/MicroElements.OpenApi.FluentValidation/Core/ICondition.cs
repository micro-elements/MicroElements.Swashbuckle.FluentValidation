// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MicroElements.OpenApi.Core
{
    /// <summary>
    /// Represents some matching condition.
    /// </summary>
    /// <typeparam name="T">Type for matching.</typeparam>
    public interface ICondition<in T>
    {
        /// <summary>
        /// Determine whether the value matches condition.
        /// </summary>
        /// <param name="value">The value to check against the condition.</param>
        /// <returns>true if the condition matches.</returns>
        bool Matches(T value);
    }
}