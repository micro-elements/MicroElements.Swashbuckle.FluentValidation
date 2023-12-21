// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using MicroElements.OpenApi.Core;
using Microsoft.Extensions.DependencyInjection;

namespace MicroElements.OpenApi.FluentValidation
{
    /// <summary>
    /// Extensions for validator registry.
    /// </summary>
    public static class ValidatorRegistryExtensions
    {
        /// <summary>
        /// Tries to get validator from the service provider.
        /// </summary>
        /// <param name="serviceProvider">The service provider.</param>
        /// <param name="modelType">Model type.</param>
        /// <returns>Validator or null.</returns>
        public static IValidator? GetValidator(this IServiceProvider serviceProvider, Type modelType)
        {
            Type validatorType = typeof(IValidator<>).MakeGenericType(modelType);
            return serviceProvider.GetService(validatorType) as IValidator;
        }

        /// <summary>
        /// Enumerates validators according provided options.
        /// </summary>
        /// <param name="validators">Source validators.</param>
        /// <param name="modelType">Model type.</param>
        /// <param name="options">Schema generation options.</param>
        /// <returns>Filtered validators.</returns>
        public static IEnumerable<IValidator> GetValidators(
            this IEnumerable<IValidator> validators,
            Type modelType,
            ISchemaGenerationOptions options)
        {
            var typeContext = new TypeContext(modelType, options);
            var validatorFilter = options.ValidatorFilter.NotNull();

            foreach (var validator in validators)
            {
                var validatorContext = new ValidatorContext(typeContext, validator);
                if (validatorFilter.Matches(validatorContext))
                {
                    yield return validator;

                    if (options.ValidatorSearch.IsOneValidatorForType)
                        break;
                }
            }
        }

        /// <summary>
        /// Gets all registered validators for type.
        /// </summary>
        /// <param name="serviceProvider">The source service provider.</param>
        /// <param name="modelType">Type to validate.</param>
        /// <param name="options">Schema generation options.</param>
        /// <returns>Enumeration of validators.</returns>
        public static IEnumerable<IValidator> GetValidators(
            this IServiceProvider serviceProvider,
            Type modelType,
            ISchemaGenerationOptions options)
        {
            if (typeof(void) == modelType)
            {
                yield break;
            }

            var typeContext = new TypeContext(modelType, options);
            ICondition<ValidatorContext> validatorFilter = options.ValidatorFilter.NotNull();

            Type validatorType = typeof(IValidator<>).MakeGenericType(modelType);

            var validators = serviceProvider
                .GetServices(validatorType)
                .OfType<IValidator>();

            foreach (var validator in validators)
            {
                if (validatorFilter is null || validatorFilter.Matches(new ValidatorContext(typeContext, validator)))
                {
                    yield return validator;

                    if (options.ValidatorSearch.IsOneValidatorForType)
                    {
                        yield break;
                    }
                }
            }

            if (options.ValidatorSearch.SearchBaseTypeValidators)
            {
                Type? baseType = modelType.BaseType;
                if (baseType != null)
                {
                    var baseValidators = serviceProvider.GetValidators(baseType, options);
                    foreach (var validator in baseValidators)
                    {
                        yield return validator;

                        if (options.ValidatorSearch.IsOneValidatorForType)
                        {
                            yield break;
                        }
                    }
                }
            }
        }
    }
}