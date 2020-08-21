using System;
using System.Linq.Expressions;
using System.Reflection;

namespace MicroElements.Swashbuckle.FluentValidation
{
    public static class Include
    {
        public static int NewMaxValue(int? current, int newValue) => current.HasValue ? Math.Min(current.Value, newValue) : newValue;

        public static decimal NewMaxValue(decimal? current, decimal newValue) => current.HasValue ? Math.Min(current.Value, newValue) : newValue;

        public static int NewMinValue(int? current, int newValue) => current.HasValue ? Math.Max(current.Value, newValue) : newValue;

        public static decimal NewMinValue(decimal? current, decimal newValue) => current.HasValue ? Math.Max(current.Value, newValue) : newValue;

        public static void SetPropertyValue<T, TValue>(this T target, Expression<Func<T, TValue>> propertyLambda, TValue value)
        {
            if (propertyLambda.Body is MemberExpression memberSelectorExpression)
            {
                var property = memberSelectorExpression.Member as PropertyInfo;
                if (property != null)
                {
                    property.SetValue(target, value, null);
                }
            }
        }
    }
}