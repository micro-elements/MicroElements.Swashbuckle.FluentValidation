using System;
using System.Collections.Generic;

namespace MicroElements.OpenApi.Core
{
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

        public static bool IsSuccess<T>(this in (T? Result, Exception? Exception) result) => result.Exception == null;

        public static bool IsError<T>(this in (T? Result, Exception? Exception) result) => !result.IsSuccess();

        public static (T? Result, Exception? Exception) OnError<T>(this in (T? Result, Exception? Exception) result, Action<Exception> onError)
        {
            if (result.Exception != null)
            {
                onError(result.Exception);
            }

            return result;
        }

        public static (T? Result, Exception? Exception) OnSuccess<T>(this in (T? Result, Exception? Exception) result, Action<T> onSuccess)
        {
            if (result.Exception == null)
            {
                onSuccess(result.Result);
            }

            return result;
        }

        public static IEnumerable<T> EnumOneOrMany<T>(this IEnumerable<T> values, bool onlyOne)
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