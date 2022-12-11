using System;
using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using MicroElements.OpenApi.Core;
using Microsoft.Extensions.DependencyInjection;

namespace MicroElements.OpenApi.FluentValidation
{
    public static class ValidatorRegistryExtensions
    {
        public static IValidator? GetValidator(this IServiceProvider serviceProvider, Type modelType)
        {
            Type validatorType = typeof(IValidator<>).MakeGenericType(modelType);
            return serviceProvider.GetService(validatorType) as IValidator;
        }

        public static IEnumerable<IValidator> GetValidators(
            this IEnumerable<IValidator> validators,
            Type modelType,
            ISchemaGenerationOptions options)
        {
            foreach (var validator in validators)
            {
                if (options.ValidatorFilter.NotNull().Matches(new ValidatorContext(modelType, validator)))
                {
                    yield return validator;

                    if (options.ValidatorSearch == ValidatorSearch.OneForType)
                        break;
                }
            }
        }

        /// <summary>
        /// Gets all registered validators for type.
        /// </summary>
        /// <param name="serviceProvider">The source service provider.</param>
        /// <param name="modelType">Type to validate.</param>
        /// <returns>Enumeration of validators.</returns>
        public static IEnumerable<IValidator> GetValidators(
            this IServiceProvider serviceProvider,
            Type modelType,
            ISchemaGenerationOptions options,
            //TODO: to options
            bool searchBaseTypeValidators = true)
        {
            ICondition<ValidatorContext> validatorFilter = options.ValidatorFilter.NotNull();

            Type validatorType = typeof(IValidator<>).MakeGenericType(modelType);

            var validators = serviceProvider
                .GetServices(validatorType)
                .OfType<IValidator>();

            foreach (var validator in validators)
            {
                if (validatorFilter is null || validatorFilter.Matches(new ValidatorContext(modelType, validator)))
                {
                    yield return validator;

                    if (options.ValidatorSearch == ValidatorSearch.OneForType)
                    {
                        yield break;
                    }
                }
            }

            if (searchBaseTypeValidators)
            {
                Type? baseType = modelType.BaseType;
                if (baseType != null)
                {
                    var baseValidators = serviceProvider.GetValidators(baseType, options, searchBaseTypeValidators);
                    foreach (var validator in baseValidators)
                    {
                        yield return validator;

                        if (options.ValidatorSearch == ValidatorSearch.OneForType)
                        {
                            yield break;
                        }
                    }
                }
            }
        }
    }
}