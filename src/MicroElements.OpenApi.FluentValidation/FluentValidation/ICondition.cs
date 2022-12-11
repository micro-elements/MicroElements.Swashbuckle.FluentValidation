using System;
using System.Collections.Generic;
using System.Linq;
using MicroElements.CodeContracts;

namespace MicroElements.OpenApi.FluentValidation
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

    /// <summary>
    /// The simple condition that uses provided predicate.
    /// </summary>
    /// <typeparam name="T">Type for matching.</typeparam>
    public class Condition<T> : ICondition<T>
    {
        private readonly Func<T, bool> _matches;

        /// <summary>
        /// Initializes a new instance of the <see cref="Condition{T}"/> class.
        /// </summary>
        /// <param name="matches">Func that implements matching behaviour.</param>
        public Condition(Func<T, bool> matches)
        {
            _matches = matches.AssertArgumentNotNull(nameof(matches));
        }

        /// <inheritdoc />
        public bool Matches(T value)
        {
            return _matches(value);
        }
    }

    public sealed class EmptyCondition<T> : ICondition<T>
    {
        public static EmptyCondition<T> Instance = new EmptyCondition<T>();

        /// <inheritdoc />
        public bool Matches(T value) => true;
    }

    public static class Condition
    {
        public static ICondition<T> Empty<T>() => EmptyCondition<T>.Instance;

        public static ICondition<T> NotNull<T>(this ICondition<T>? condition) => condition ?? Empty<T>();
    }

    /// <summary>
    /// Condition that combines one or more conditions.
    /// </summary>
    /// <typeparam name="T">Condition context type.</typeparam>
    public class CompositeCondition<T> : ICondition<T>
    {
        private readonly IReadOnlyCollection<ICondition<T>>? _conditions;

        public CompositeCondition(IReadOnlyCollection<ICondition<T>>? conditions)
        {
            _conditions = conditions;
        }

        /// <inheritdoc />
        public bool Matches(T value)
        {
            if (_conditions is null || _conditions.Count == 0)
                return true;
            return _conditions.All(condition => condition.Matches(value));
        }
    }
}