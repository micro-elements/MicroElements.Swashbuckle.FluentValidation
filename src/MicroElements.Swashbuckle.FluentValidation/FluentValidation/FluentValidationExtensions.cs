using System;
using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using FluentValidation.Internal;
using FluentValidation.Validators;

namespace MicroElements.Swashbuckle.FluentValidation
{
    public static class FluentValidationExtensions
    {
        [Obsolete("allow to override")]
        public static IEnumerable<IValidator> GetIncludedValidators(this IValidator validator)
        {
            // Note: IValidatorDescriptor doesn't return IncludeRules so we need to get validators manually.
            IEnumerable<IValidator> validators = (validator as IEnumerable<IValidationRule>)
                .NotNull()
                .Where(rule => rule is IIncludeRule)
                .OfType<PropertyRule>()
                .Where(includeRule => includeRule.HasNoCondition())
                .SelectMany(includeRule => includeRule.Validators)
                .OfType<IChildValidatorAdaptor>()
                .Select(childAdapter => childAdapter.GetValidatorFromChildValidatorAdapter())
                .Where(v => v != null);

            return validators;
        }

        [Obsolete("allow to override")]
        public static IValidator GetValidatorFromChildValidatorAdapter(this IChildValidatorAdaptor childValidatorAdapter)
        {
            IValidator validator;
            //if (_validatorFactory is ValidatorFactoryBase instanceCreator)
            //{
            //    validator = instanceCreator.CreateInstance(childValidatorAdapter.ValidatorType);
            //}

            // Fake context. We have not got real context because no validation yet. 
            var fakeContext = new PropertyValidatorContext(new ValidationContext<object>(null), null, string.Empty);

            // Try to validator with reflection.
            var childValidatorAdapterType = childValidatorAdapter.GetType();
            var getValidatorMethod = childValidatorAdapterType.GetMethod(nameof(ChildValidatorAdaptor<object, object>.GetValidator));
            if (getValidatorMethod != null)
            {
                validator = (IValidator)getValidatorMethod.Invoke(childValidatorAdapter, new[] { fakeContext });
                return validator;
            }

            return null;
        }
    }
}
