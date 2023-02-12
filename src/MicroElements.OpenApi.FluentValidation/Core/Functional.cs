// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

// ReSharper disable UnusedTupleComponentInReturnValue
namespace MicroElements.OpenApi.Core
{
    /// <summary>
    /// Very light functional extensions.
    /// </summary>
    internal static class Functional
    {
        public static (T? Result, Exception? Exception) Try<T>(Func<T> func)
        {
            try
            {
                var result = func();
                return (result, default);
            }
            catch (Exception e)
            {
                return (default, e);
            }
        }

        internal static bool IsSuccess<T>(this in (T? Result, Exception? Exception) result) => result.Exception == null;

        internal static bool IsError<T>(this in (T? Result, Exception? Exception) result) => !result.IsSuccess();

        internal static (T? Result, Exception? Exception) OnError<T>(this in (T? Result, Exception? Exception) result, Action<Exception> onError)
        {
            if (result.Exception != null)
            {
                onError(result.Exception);
            }

            return result;
        }

        internal static (T? Result, Exception? Exception) OnSuccess<T>(this in (T? Result, Exception? Exception) result, Action<T?> onSuccess)
        {
            if (result.Exception == null)
            {
                onSuccess(result.Result);
            }

            return result;
        }

        internal static IEnumerable<T> EnumOneOrMany<T>(this IEnumerable<T> values, bool onlyOne)
        {
            foreach (var value in values)
            {
                yield return value;

                if (onlyOne)
                    break;
            }
        }
    }
}