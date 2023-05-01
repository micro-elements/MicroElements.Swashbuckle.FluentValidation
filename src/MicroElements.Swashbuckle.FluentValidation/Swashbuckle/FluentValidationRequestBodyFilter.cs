// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using MicroElements.OpenApi.FluentValidation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MicroElements.Swashbuckle.FluentValidation
{
    public class FluentValidationRequestBodyFilter : IRequestBodyFilter
    {
        private readonly ILogger _logger;

        private readonly IValidatorRegistry _validatorRegistry;

        private readonly IReadOnlyList<IFluentValidationRule<OpenApiSchema>> _rules;
        private readonly SchemaGenerationOptions _schemaGenerationOptions;

        public FluentValidationRequestBodyFilter(
            /* System services */
            ILoggerFactory? loggerFactory = null,
            IServiceProvider? serviceProvider = null,

            /* MicroElements services */
            IValidatorRegistry? validatorRegistry = null,
            IFluentValidationRuleProvider<OpenApiSchema>? fluentValidationRuleProvider = null,
            IEnumerable<FluentValidationRule>? rules = null,
            IOptions<SchemaGenerationOptions>? schemaGenerationOptions = null)
        {
            // System services
            _logger = loggerFactory?.CreateLogger(typeof(FluentValidationRules)) ?? NullLogger.Instance;

            // FluentValidation services
            _validatorRegistry = validatorRegistry ?? new ServiceProviderValidatorRegistry(serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider)));

            // MicroElements services
            fluentValidationRuleProvider ??= new DefaultFluentValidationRuleProvider(schemaGenerationOptions);
            _rules = fluentValidationRuleProvider.GetRules().ToArray().OverrideRules(rules);
            _schemaGenerationOptions = schemaGenerationOptions?.Value ?? new SchemaGenerationOptions();

            _logger.LogDebug("FluentValidationRequestBodyFilter Created");
        }

        /// <inheritdoc />
        public void Apply(OpenApiRequestBody requestBody, RequestBodyFilterContext context)
        {
            if (context.FormParameterDescriptions == null)
                return;

            var parameterDescriptions = context.FormParameterDescriptions.ToArray();
            if (parameterDescriptions.Length > 0)
            {
                var schemaProvider = new SwashbuckleSchemaProvider(context.SchemaRepository, context.SchemaGenerator, _schemaGenerationOptions.SchemaIdSelector);

                foreach (var apiParameterDescription in parameterDescriptions)
                {
                    var parameterType = apiParameterDescription.ParameterDescriptor.ParameterType;
                    if (parameterType != null)
                    {
                        var validator = _validatorRegistry.GetValidator(parameterType);
                        if (validator == null)
                            continue;

                        if (requestBody.Content.TryGetValue("multipart/form-data", out var openApiMediaType))
                        {
                            var schemaContext = new SchemaGenerationContext(
                                schemaRepository: context.SchemaRepository,
                                schemaGenerator: context.SchemaGenerator,
                                schema: openApiMediaType.Schema,
                                schemaType: parameterType,
                                rules: _rules,
                                schemaGenerationOptions: _schemaGenerationOptions,
                                schemaProvider: schemaProvider);

                            FluentValidationSchemaBuilder.ApplyRulesToSchema(
                                schemaType: parameterType,
                                schemaPropertyNames: new[] { apiParameterDescription.Name },
                                validator: validator,
                                logger: _logger,
                                schemaGenerationContext: schemaContext);
                        }
                    }
                }
            }
        }
    }
}