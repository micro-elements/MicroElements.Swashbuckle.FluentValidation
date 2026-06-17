// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MicroElements.OpenApi;
using MicroElements.OpenApi.Core;
using MicroElements.OpenApi.FluentValidation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
#if !OPENAPI_V2
using Microsoft.OpenApi.Models;
#endif
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MicroElements.Swashbuckle.FluentValidation
{
    /// <summary>
    /// Swagger <see cref="IOperationFilter"/> that applies FluentValidation rules
    /// for GET parameters bounded from validatable models.
    /// </summary>
    public class FluentValidationOperationFilter : IOperationFilter
    {
        private readonly ILogger _logger;

        private readonly IValidatorRegistry? _validatorRegistry;

        private readonly IReadOnlyList<IFluentValidationRule<OpenApiSchema>> _rules;
        private readonly SchemaGenerationOptions _schemaGenerationOptions;

        /// <summary>
        /// Initializes a new instance of the <see cref="FluentValidationOperationFilter"/> class.
        /// </summary>
        /// <param name="loggerFactory"><see cref="ILoggerFactory"/> for logging. Can be null.</param>
        /// <param name="serviceProvider">Validator factory.</param>
        /// <param name="validatorRegistry">Gets validators for a particular type.</param>
        /// <param name="fluentValidationRuleProvider">Rules provider.</param>
        /// <param name="rules">External FluentValidation rules. External rule overrides default rule with the same name.</param>
        /// <param name="schemaGenerationOptions">Schema generation options.</param>
        public FluentValidationOperationFilter(
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

            _logger.LogDebug("FluentValidationOperationFilter Created");
        }

        /// <inheritdoc />
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            try
            {
                ApplyInternal(operation, context);
            }
            catch (Exception e)
            {
                var operationId = operation.OperationId ?? context.ApiDescription.RelativePath;
                _logger.LogWarning(0, e, $"Error on apply rules for operation '{operationId}'.");
            }
        }

        private void ApplyInternal(OpenApiOperation operation, OperationFilterContext context)
        {
            if (_validatorRegistry == null)
            {
                _logger.LogWarning(0, "ValidatorFactory is not provided. Please register FluentValidation.");
                return;
            }

            var schemaProvider = new SwashbuckleSchemaProvider(context.SchemaRepository, context.SchemaGenerator, _schemaGenerationOptions.SchemaIdSelector);

            // Process operation parameters (FromQuery, FromRoute, FromHeader)
            if (operation.Parameters != null)
            {
                ApplyRulesToParameters(operation, context, schemaProvider);
            }

            // Process RequestBody for FromForm and FromBody parameters
            ApplyRulesToRequestBody(operation, context, schemaProvider);
        }

        private void ApplyRulesToParameters(OpenApiOperation operation, OperationFilterContext context, SwashbuckleSchemaProvider schemaProvider)
        {
            // Issue #180: Track schemas that exist before our processing.
            // GetSchemaForType() has a side-effect of registering schemas in SchemaRepository.
            // For [AsParameters]/[FromQuery] container types, Swashbuckle does NOT create schemas
            // (it expands them into individual parameters), so any schemas we create are unused.
            HashSet<string>? existingSchemaIds = _schemaGenerationOptions.RemoveUnusedQuerySchemas
                ? new HashSet<string>(context.SchemaRepository.Schemas.Keys)
                : null;

            foreach (var operationParameter in operation.Parameters!)
            {
                var apiParameterDescription = context.ApiDescription.ParameterDescriptions.FirstOrDefault(description =>
                    description.Name.Equals(operationParameter.Name, StringComparison.InvariantCultureIgnoreCase));

                var modelMetadata = apiParameterDescription?.ModelMetadata;
                if (modelMetadata != null)
                {
                    var parameterType = modelMetadata.ContainerType;

                    // Fallback for .NET 8 where ContainerType may be null for [AsParameters]
                    if (parameterType == null)
                        parameterType = AsParametersHelper.ResolveContainerType(
                            operationParameter.Name, context.MethodInfo);

                    if (parameterType == null)
                        continue;

                    var validator = _validatorRegistry.GetValidator(parameterType);
                    if (validator == null)
                        continue;

                    // Issue #211: For a flattened nested [FromQuery] parameter (e.g. "RequiredSubType.SubProperty")
                    // only reflect the nested type's validation when it is actually reachable from the ROOT
                    // validator via SetValidator/ChildRules. FluentValidation never auto-validates a child object
                    // from DI, so an unwired nested validator would document constraints (required, MinLength, ...)
                    // that runtime validation never enforces.
                    if (operationParameter.Name.IndexOf('.') >= 0
                        && !IsNestedValidationReachable(operationParameter.Name, context))
                    {
                        continue;
                    }

                    OpenApiSchema schema = schemaProvider.GetSchemaForType(parameterType);

                    if (schema.Properties != null && schema.Properties.Count > 0)
                    {
                        var schemaPropertyName = operationParameter.Name;

                        // For nested [FromQuery] parameters (e.g., "operation.op"), use only the leaf
                        // property name since the schema for the nested type only has the leaf property.
                        var dotIndex = schemaPropertyName.LastIndexOf('.');
                        if (dotIndex >= 0)
                            schemaPropertyName = schemaPropertyName.Substring(dotIndex + 1);

                        var apiProperty = OpenApiSchemaCompatibility.GetProperties(schema)
                            .FirstOrDefault(property => property.Key.EqualsIgnoreAll(schemaPropertyName));
                        if (apiProperty.Key != null)
                        {
                            schemaPropertyName = apiProperty.Key;
                        }
                        else
                        {
                            var propertyInfo = parameterType.GetProperty(schemaPropertyName);
                            if (propertyInfo != null && _schemaGenerationOptions.NameResolver != null)
                            {
                                schemaPropertyName = _schemaGenerationOptions.NameResolver.GetPropertyName(propertyInfo);
                            }
                        }

                        var schemaContext = new SchemaGenerationContext(
                            schemaRepository: context.SchemaRepository,
                            schemaGenerator: context.SchemaGenerator,
                            schema: schema,
                            schemaType: parameterType,
                            rules: _rules,
                            schemaGenerationOptions: _schemaGenerationOptions,
                            schemaProvider: schemaProvider);

                        FluentValidationSchemaBuilder.ApplyRulesToSchema(
                            schemaType: parameterType,
                            schemaPropertyNames: new[] { schemaPropertyName },
                            validator: validator,
                            logger: _logger,
                            schemaGenerationContext: schemaContext);

                        // Issue #209: a required leaf is only a required parameter when the WHOLE dot-path
                        // is required. For a flattened nested [FromQuery] parameter (e.g. "OptionalSubType.SubProperty")
                        // an optional ancestor (e.g. an optional nested object) must keep the parameter optional.
                        if (OpenApiSchemaCompatibility.RequiredContains(schema, schemaPropertyName)
                            && IsParameterPathRequired(operationParameter.Name, context, schemaProvider))
                        {
#if OPENAPI_V2
                            // In OpenApi 2.x, IOpenApiParameter.Required is read-only
                            // We need to cast to OpenApiParameter to set it
                            if (operationParameter is OpenApiParameter openApiParameter)
                                openApiParameter.Required = true;
#else
                            operationParameter.Required = true;
#endif
                        }

                        var parameterSchema = operationParameter.Schema;
                        if (parameterSchema != null)
                        {
                            if (OpenApiSchemaCompatibility.TryGetProperty(schema, schemaPropertyName.ToLowerCamelCase(), out var property, context.SchemaRepository)
                                || OpenApiSchemaCompatibility.TryGetProperty(schema, schemaPropertyName, out property, context.SchemaRepository))
                            {
                                if (property != null)
                                {
#if OPENAPI_V2
                                    // In OpenApi 2.x, IOpenApiSchema properties are read-only
                                    // We need to cast to OpenApiSchema to set them
                                    if (parameterSchema is OpenApiSchema targetSchema)
                                    {
                                        // Copy from property schema to parameter schema.
                                        targetSchema.Description = property.Description;
                                        targetSchema.MinLength = property.MinLength;
                                        OpenApiSchemaCompatibility.SetNullable(targetSchema, OpenApiSchemaCompatibility.GetNullable(property));
                                        targetSchema.MaxLength = property.MaxLength;
                                        targetSchema.Pattern = property.Pattern;
                                        targetSchema.Minimum = property.Minimum;
                                        targetSchema.Maximum = property.Maximum;
                                        targetSchema.ExclusiveMaximum = property.ExclusiveMaximum;
                                        targetSchema.ExclusiveMinimum = property.ExclusiveMinimum;
                                        targetSchema.Enum = property.Enum;
                                        targetSchema.AllOf = property.AllOf;
                                    }
#else
                                    // Copy from property schema to parameter schema.
                                    parameterSchema.Description = property.Description;
                                    parameterSchema.MinLength = property.MinLength;
                                    OpenApiSchemaCompatibility.SetNullable(parameterSchema, OpenApiSchemaCompatibility.GetNullable(property));
                                    parameterSchema.MaxLength = property.MaxLength;
                                    parameterSchema.Pattern = property.Pattern;
                                    parameterSchema.Minimum = property.Minimum;
                                    parameterSchema.Maximum = property.Maximum;
                                    parameterSchema.ExclusiveMaximum = property.ExclusiveMaximum;
                                    parameterSchema.ExclusiveMinimum = property.ExclusiveMinimum;
                                    parameterSchema.Enum = property.Enum;
                                    parameterSchema.AllOf = property.AllOf;
#endif
                                }
                            }
                        }
                    }
                }
            }

            // Issue #180: Remove schemas that we created as a side-effect of GetSchemaForType().
            // These schemas were not created by Swashbuckle and are not referenced elsewhere.
            if (existingSchemaIds != null)
            {
                foreach (var schemaId in context.SchemaRepository.Schemas.Keys.ToArray())
                {
                    if (!existingSchemaIds.Contains(schemaId))
                    {
                        context.SchemaRepository.Schemas.Remove(schemaId);
                    }
                }
            }
        }

        /// <summary>
        /// Issue #211: checks that a flattened nested [FromQuery] parameter's leaf type is actually validated —
        /// i.e. the SetValidator/ChildRules chain from the root [FromQuery] validator reaches the leaf container.
        /// If the chain is broken (no SetValidator), the nested rules are not enforced at runtime and must not
        /// appear in the OpenAPI document.
        /// </summary>
        private bool IsNestedValidationReachable(string parameterName, OperationFilterContext context)
        {
            try
            {
                var segments = parameterName.Split('.');

                // Resolve the root [FromQuery]/[AsParameters] type from the action method by matching the first segment.
                var rootType = ResolveRootType(segments[0], context.MethodInfo);

                // Cannot resolve the root container (e.g. a synthetic MethodInfo in tests) — preserve prior behavior.
                if (rootType == null)
                    return true;

                var rootValidator = _validatorRegistry!.GetValidator(rootType);

                // No validator for the bound [FromQuery] type — runtime validates nothing along this path.
                if (rootValidator == null)
                    return false;

                // Ancestors are every segment except the leaf; the leaf's own rules live in its container validator.
                var ancestorMembers = new string[segments.Length - 1];
                Array.Copy(segments, ancestorMembers, segments.Length - 1);

                return FluentValidationExtensions.IsNestedValidationWired(rootValidator, ancestorMembers, _schemaGenerationOptions);
            }
            catch (Exception e)
            {
                // A dynamic SetValidator(ctx => ...) factory may throw when probed with a fake context.
                // Such a member IS wired, so fall back to prior behavior (reachable) and never let one
                // problematic validator skip the whole operation's parameters.
                _logger.LogDebug(e, "Could not determine nested validation reachability for parameter '{ParameterName}'; assuming reachable.", parameterName);
                return true;
            }
        }

        /// <summary>
        /// Determines whether a (possibly nested) operation parameter may be marked as required.
        /// For a flattened nested [FromQuery] parameter (e.g. "OptionalSubType.SubProperty") the leaf
        /// is a required parameter only when EVERY ancestor segment of the dot-path is itself required.
        /// If any ancestor (e.g. an optional nested object) is not required, the parameter stays optional.
        /// Issue #209.
        /// </summary>
        private bool IsParameterPathRequired(string parameterName, OperationFilterContext context, SwashbuckleSchemaProvider schemaProvider)
        {
            // Flat parameter: no ancestors to verify.
            if (parameterName.IndexOf('.') < 0)
                return true;

            var segments = parameterName.Split('.');

            // Resolve the root [FromQuery]/[AsParameters] type from the action method by matching the first segment.
            var currentType = ResolveRootType(segments[0], context.MethodInfo);

            // If the root type cannot be determined, preserve the prior behavior (mark required).
            if (currentType == null)
                return true;

            // Walk every ancestor segment (all but the leaf); leaf requiredness is handled by the caller.
            // Resolving ancestor requiredness registers the ancestor (container) schemas in the repository,
            // exactly like the leaf container is registered above. Those unused [FromQuery] container schemas
            // are removed by the Issue #180 cleanup when RemoveUnusedQuerySchemas is enabled. We must NOT
            // remove them mid-loop: Swashbuckle's generator remembers already-generated types and would not
            // re-register the component on the next GetSchemaForType call, leaving an unresolvable $ref.
            for (int i = 0; i < segments.Length - 1; i++)
            {
                if (!IsPropertyRequiredInType(currentType, segments[i], context, schemaProvider))
                    return false;

                var propertyInfo = currentType.GetProperty(segments[i], BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (propertyInfo == null)
                    return true; // path does not map to a real property — preserve prior behavior

                currentType = propertyInfo.PropertyType;
            }

            return true;
        }

        /// <summary>
        /// Finds the action parameter type that exposes <paramref name="firstSegment"/> as a property.
        /// This is the root container of a flattened nested [FromQuery] parameter.
        /// Limitation: if several action parameters expose a property with the same name, the first
        /// match wins. This is an unlikely edge case; a wrong guess only affects the required flag and
        /// the conservative fallbacks keep prior behavior.
        /// </summary>
        private static Type? ResolveRootType(string firstSegment, MethodInfo? methodInfo)
        {
            if (methodInfo == null)
                return null;

            foreach (var parameter in methodInfo.GetParameters())
            {
                if (parameter.ParameterType.GetProperty(firstSegment, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase) != null)
                    return parameter.ParameterType;
            }

            return null;
        }

        /// <summary>
        /// Checks whether <paramref name="propertyName"/> is required in <paramref name="containerType"/>,
        /// combining the generated schema (native required, e.g. the C# 'required' modifier) with the
        /// FluentValidation rules (NotNull/NotEmpty) of the container type's validator.
        /// </summary>
        private bool IsPropertyRequiredInType(Type containerType, string propertyName, OperationFilterContext context, SwashbuckleSchemaProvider schemaProvider)
        {
            OpenApiSchema schema = schemaProvider.GetSchemaForType(containerType);

            // No properties to reason about — treat the ancestor as not required (safe-to-optional default,
            // intentionally the opposite of IsParameterPathRequired's "assume required" fallback: there we
            // could not resolve the path at all and keep prior behavior, here we positively know the type
            // exposes nothing to require against).
            if (schema.Properties == null || schema.Properties.Count == 0)
                return false;

            // Resolve the schema property key (handles camelCase / PascalCase differences).
            var resolvedName = OpenApiSchemaCompatibility.GetProperties(schema)
                .Select(property => property.Key)
                .FirstOrDefault(key => key.EqualsIgnoreAll(propertyName)) ?? propertyName;

            // GetSchemaForType runs the FluentValidationRules schema filter during generation, so the
            // requiredness (native 'required' modifier + NotNull/NotEmpty rules) is usually already present.
            // Check first to avoid the write side effect of re-applying rules on a shared cached schema.
            if (OpenApiSchemaCompatibility.RequiredContains(schema, resolvedName))
                return true;

            // Fallback for setups whose schema generator has no FluentValidationRules filter: apply the
            // container validator's rules explicitly, then re-check. _validatorRegistry is non-null here —
            // ApplyInternal returns early when it is null, before any parameter processing.
            var validator = _validatorRegistry!.GetValidator(containerType);
            if (validator != null)
            {
                var schemaContext = new SchemaGenerationContext(
                    schemaRepository: context.SchemaRepository,
                    schemaGenerator: context.SchemaGenerator,
                    schema: schema,
                    schemaType: containerType,
                    rules: _rules,
                    schemaGenerationOptions: _schemaGenerationOptions,
                    schemaProvider: schemaProvider);

                FluentValidationSchemaBuilder.ApplyRulesToSchema(
                    schemaType: containerType,
                    schemaPropertyNames: new[] { resolvedName },
                    validator: validator,
                    logger: _logger,
                    schemaGenerationContext: schemaContext);

                return OpenApiSchemaCompatibility.RequiredContains(schema, resolvedName);
            }

            return false;
        }

        private void ApplyRulesToRequestBody(OpenApiOperation operation, OperationFilterContext context, SwashbuckleSchemaProvider schemaProvider)
        {
#if OPENAPI_V2
            var requestBody = operation.RequestBody as OpenApiRequestBody;
#else
            var requestBody = operation.RequestBody;
#endif
            if (requestBody?.Content == null)
                return;

            // Content types used by [FromForm] attribute
            var formContentTypes = new[] { "multipart/form-data", "application/x-www-form-urlencoded" };

            foreach (var contentType in requestBody.Content)
            {
                if (!formContentTypes.Contains(contentType.Key, StringComparer.OrdinalIgnoreCase))
                    continue;

#if OPENAPI_V2
                var rawSchema = contentType.Value.Schema;
                var contentSchema = rawSchema as OpenApiSchema;
                string? schemaRefId = rawSchema is OpenApiSchemaReference schemaRef ? schemaRef.Reference?.Id : null;
#else
                var contentSchema = contentType.Value.Schema;
                string? schemaRefId = contentSchema?.Reference?.Id;
#endif
                if (contentSchema == null)
                    continue;

                // Find the parameter type from ApiDescription
                var bodyParameter = context.ApiDescription.ParameterDescriptions
                    .FirstOrDefault(p => p.Source?.Id == "Form" || p.Source?.Id == "Body");

                Type? parameterType = null;
                if (bodyParameter != null)
                {
                    parameterType = bodyParameter.ModelMetadata?.ContainerType ?? bodyParameter.ModelMetadata?.ModelType;
                }

                // If we couldn't find it from body parameter, try to find from schema reference
                if (parameterType == null && schemaRefId != null)
                {
                    parameterType = context.ApiDescription.ParameterDescriptions
                        .Select(p => p.ModelMetadata?.ModelType)
                        .FirstOrDefault(t => t != null && _schemaGenerationOptions.SchemaIdSelector(t) == schemaRefId);
                }

                if (parameterType == null)
                    continue;

                var validator = _validatorRegistry!.GetValidator(parameterType);
                if (validator == null)
                    continue;

                // Resolve the actual schema (dereference if needed)
                OpenApiSchema resolvedSchema = contentSchema;
                if (schemaRefId != null)
                {
                    resolvedSchema = schemaProvider.GetSchemaForType(parameterType);
                }

                if (resolvedSchema.Properties == null || resolvedSchema.Properties.Count == 0)
                    continue;

                var schemaContext = new SchemaGenerationContext(
                    schemaRepository: context.SchemaRepository,
                    schemaGenerator: context.SchemaGenerator,
                    schema: resolvedSchema,
                    schemaType: parameterType,
                    rules: _rules,
                    schemaGenerationOptions: _schemaGenerationOptions,
                    schemaProvider: schemaProvider);

                // Apply validation rules to all properties
                FluentValidationSchemaBuilder.ApplyRulesToSchema(
                    schemaType: parameterType,
                    schemaPropertyNames: schemaContext.Properties,
                    validator: validator,
                    logger: _logger,
                    schemaGenerationContext: schemaContext);
            }
        }
    }
}
