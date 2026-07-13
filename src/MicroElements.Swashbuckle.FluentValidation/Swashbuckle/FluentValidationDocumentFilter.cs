// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using MicroElements.OpenApi;
using MicroElements.OpenApi.Core;
using MicroElements.OpenApi.FluentValidation;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
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
    /// Document filter that applies FluentValidation rules for the whole document at once:
    /// component schemas, operation parameters (including required-marking, Issue #209) and
    /// request bodies (Issue #216), with the Issue #180 schema cleanup performed once at the
    /// end of the document — so per-operation shared-DTO state issues (Issue #223/#226)
    /// cannot occur in this pipeline. Enabled via <c>RegistrationOptions.UseDocumentFilter</c>.
    /// </summary>
    public class FluentValidationDocumentFilter : IDocumentFilter
    {
        private readonly ILogger _logger;

        private readonly IValidatorRegistry _validatorRegistry;

        private readonly IReadOnlyList<IFluentValidationRule<OpenApiSchema>> _rules;
        private readonly SchemaGenerationOptions _schemaGenerationOptions;
        private readonly ParameterRequiredResolver _requiredResolver;
        private readonly RequestBodyRuleApplicator _requestBodyApplicator;

        /// <summary>
        /// Initializes a new instance of the <see cref="FluentValidationDocumentFilter"/> class.
        /// </summary>
        /// <param name="loggerFactory"><see cref="ILoggerFactory"/> for logging. Can be null.</param>
        /// <param name="serviceProvider">Validator factory.</param>
        /// <param name="validatorRegistry">Gets validators for a particular type.</param>
        /// <param name="rules">External FluentValidation rules. External rule overrides default rule with the same name.</param>
        /// <param name="schemaGenerationOptions">Schema generation options.</param>
        /// <param name="nameResolver">Name resolver.</param>
        /// <param name="fluentValidationRuleProvider">Rules provider. Appended last to keep positional call sites source-compatible.</param>
        public FluentValidationDocumentFilter(
            /* System services */
            ILoggerFactory? loggerFactory = null,
            IServiceProvider? serviceProvider = null,

            /* MicroElements services */
            IValidatorRegistry? validatorRegistry = null,
            IEnumerable<FluentValidationRule>? rules = null,
            IOptions<SchemaGenerationOptions>? schemaGenerationOptions = null,
            INameResolver? nameResolver = null,
            IFluentValidationRuleProvider<OpenApiSchema>? fluentValidationRuleProvider = null)
        {
            // System services
            _logger = loggerFactory?.CreateLogger(typeof(FluentValidationDocumentFilter)) ?? NullLogger.Instance;

            // FluentValidation services
            _validatorRegistry = validatorRegistry ?? new ServiceProviderValidatorRegistry(
                serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider)),
                schemaGenerationOptions);

            // MicroElements services
            fluentValidationRuleProvider ??= new DefaultFluentValidationRuleProvider(schemaGenerationOptions);
            _rules = fluentValidationRuleProvider.GetRules().ToArray().OverrideRules(rules);
            _schemaGenerationOptions = schemaGenerationOptions?.Value ?? new SchemaGenerationOptions();

            // #209 and #216 logic is shared with FluentValidationOperationFilter so the pipelines cannot drift.
            _requiredResolver = new ParameterRequiredResolver(_logger, _validatorRegistry, _rules, _schemaGenerationOptions);
            _requestBodyApplicator = new RequestBodyRuleApplicator(_logger, _validatorRegistry, _rules, _schemaGenerationOptions);

            _logger.LogDebug("FluentValidationDocumentFilter Created");
        }

        private sealed record SchemaItem
        {
            public required Type ModelType { get; init; }

            public required string SchemaName { get; init; }

            public required OpenApiSchema Schema { get; init; }
        }

        private sealed record ParameterItem
        {
            public required ApiDescription ApiDescription { get; init; }

            public required ApiParameterDescription ParameterDescription { get; init; }

            public required Type ModelType { get; init; }

            public required string SchemaName { get; init; }

            public required OpenApiSchema Schema { get; init; }

#if OPENAPI_V2
            public IOpenApiParameter? Parameter { get; init; }
#else
            public OpenApiParameter? Parameter { get; init; }
#endif

            public OpenApiSchema? ParameterSchema { get; init; }
        }

        /// <inheritdoc />
        public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
        {
            try
            {
                ApplyInternal(swaggerDoc, context);
            }
            catch (Exception e)
            {
                _logger.LogWarning(0, e, "Error on apply FluentValidation rules to the document.");
            }
        }

        private void ApplyInternal(OpenApiDocument swaggerDoc, DocumentFilterContext context)
        {
            var schemaRepositorySchemas = context.SchemaRepository.Schemas;
            var schemaIdSelector = _schemaGenerationOptions.SchemaIdSelector;

            // Issue #180: Track schemas that exist before our processing.
            // GetSchemaForType() has a side-effect of registering schemas in SchemaRepository.
            // For [AsParameters]/[FromQuery] container types, Swashbuckle does NOT create schemas
            // (it expands them into individual parameters), so any schemas we create are unused.
            HashSet<string>? existingSchemaIds = _schemaGenerationOptions.RemoveUnusedQuerySchemas
                ? new HashSet<string>(schemaRepositorySchemas.Keys)
                : null;
            var schemaProvider = new SwashbuckleSchemaProvider(context.SchemaRepository, context.SchemaGenerator, schemaIdSelector);

            var apiDescriptions = context.ApiDescriptions.ToArray();

            var modelTypes = apiDescriptions
                .SelectMany(description => description.ParameterDescriptions)
                .Where(description => description.ModelMetadata.ContainerType is null)
                .Select(description => description.ModelMetadata.ModelType)
                .Distinct();

            var containerTypes = apiDescriptions
                .SelectMany(apiDesc => apiDesc.ParameterDescriptions.Select(pd => new { apiDesc, pd }))
                .Select(x => x.pd.ModelMetadata.ContainerType
                    ?? AsParametersHelper.ResolveContainerType(x.pd.Name, AsParametersHelper.GetMethodInfo(x.apiDesc)))
                .Where(type => type != null)
                .Distinct();

            var schemasForTypes = modelTypes
                .Concat(containerTypes)
                .Distinct()
                .Select(modelType => new SchemaItem
                {
                    ModelType = modelType!,
                    SchemaName = schemaIdSelector.Invoke(modelType!),
                    Schema = schemaProvider.GetSchemaForType(modelType!),
                })
                .ToArray();

            // Maps an ApiDescription to ITS operation (path + HTTP method), so multi-verb paths
            // are fully processed (each ApiDescription is one operation).
#if OPENAPI_V2
            OpenApiOperation? FindOperation(ApiDescription apiDescription)
            {
                var path = swaggerDoc.Paths.FirstOrDefault(pair => pair.Key.TrimStart('/') == apiDescription.RelativePath);
                if (path.Value?.Operations is not { } operations)
                    return null;

                if (apiDescription.HttpMethod is { } httpMethod
                    && operations.TryGetValue(new System.Net.Http.HttpMethod(httpMethod), out var operation))
                {
                    return operation as OpenApiOperation;
                }

                return operations.Values.FirstOrDefault() as OpenApiOperation;
            }
#else
            OpenApiOperation? FindOperation(ApiDescription apiDescription)
            {
                var path = swaggerDoc.Paths.FirstOrDefault(pair => pair.Key.TrimStart('/') == apiDescription.RelativePath);
                if (path.Value?.Operations is not { } operations)
                    return null;

                if (apiDescription.HttpMethod is { } httpMethod
                    && Enum.TryParse<OperationType>(httpMethod, ignoreCase: true, out var operationType)
                    && operations.TryGetValue(operationType, out var operation))
                {
                    return operation;
                }

                return operations.Values.FirstOrDefault();
            }
#endif

#if OPENAPI_V2
            IOpenApiParameter? FindParameter(ApiDescription apiDescription, ApiParameterDescription parameterDescription)
#else
            OpenApiParameter? FindParameter(ApiDescription apiDescription, ApiParameterDescription parameterDescription)
#endif
            {
                var operation = FindOperation(apiDescription);

                // Case-insensitive: DescribeAllParametersInCamelCase emits "name" while ApiExplorer
                // reports "Name" — matching the operation filter's parameter lookup.
                return operation?.Parameters?.FirstOrDefault(parameter =>
                    string.Equals(parameter.Name, parameterDescription.Name, StringComparison.InvariantCultureIgnoreCase));
            }

            IEnumerable<ParameterItem> GetParameters()
            {
                foreach (var apiDescription in apiDescriptions)
                {
                    foreach (var apiParameterDescription in apiDescription.ParameterDescriptions)
                    {
                        var containerType = apiParameterDescription.ModelMetadata.ContainerType
                            ?? AsParametersHelper.ResolveContainerType(
                                apiParameterDescription.Name, AsParametersHelper.GetMethodInfo(apiDescription));
                        if (containerType != null)
                        {
                            var parameter = FindParameter(apiDescription, apiParameterDescription);
#if OPENAPI_V2
                            // Explicit cast-guard pattern: $ref-typed parameter schemas are skipped
                            // gracefully, matching the operation filter's behavior.
                            var parameterSchema = parameter?.Schema is OpenApiSchema concreteSchema ? concreteSchema : null;
#else
                            var parameterSchema = parameter?.Schema;
#endif

                            yield return new ParameterItem
                            {
                                ApiDescription = apiDescription,
                                ParameterDescription = apiParameterDescription,
                                ModelType = containerType,
                                SchemaName = schemaIdSelector.Invoke(containerType),
                                Schema = schemaProvider.GetSchemaForType(containerType),
                                Parameter = parameter,
                                ParameterSchema = parameterSchema,
                            };
                        }
                    }
                }
            }

            var schemasForParameters = GetParameters().ToArray();

            // 1) Apply rules to component schemas — the document-filter counterpart of the
            // FluentValidationRules schema filter (multi-validator, allOf/oneOf/anyOf, #198 refs).
            foreach (var item in schemasForTypes)
            {
                var (validators, _) = Functional
                    .Try(() => _validatorRegistry.GetValidators(item.ModelType).ToArray())
                    .OnError(e => _logger.LogWarning(0, e, "GetValidators for type '{ModelType}' failed", item.ModelType));

                if (validators == null || validators.Length == 0)
                    continue;

                var typeContext = new TypeContext(item.ModelType, _schemaGenerationOptions);

                var allSchemas = new List<OpenApiSchema>();
                FluentValidationRules.ProcessAllSchemas(item.Schema, allSchemas);

                foreach (var validator in validators)
                {
                    foreach (var schemaToProcess in allSchemas)
                    {
#if OPENAPI_V2
                        // Issue #198: Snapshot $ref properties before rule application (see FluentValidationRules).
                        var refSnapshot = OpenApiSchemaCompatibility.SnapshotRefs(schemaToProcess);
#endif

                        var validatorContext = new ValidatorContext(typeContext, validator);
                        var schemaContext = new SchemaGenerationContext(
                            schemaRepository: context.SchemaRepository,
                            schemaGenerator: context.SchemaGenerator,
                            schema: schemaToProcess,
                            schemaType: item.ModelType,
                            rules: _rules,
                            schemaGenerationOptions: _schemaGenerationOptions);

                        ApplyRulesToSchema(schemaContext, validator);

                        try
                        {
                            AddRulesFromIncludedValidators(schemaContext, validatorContext);
                        }
                        catch (Exception e)
                        {
                            _logger.LogWarning(0, e, $"Applying IncludeRules for type '{item.ModelType}' fails.");
                        }

#if OPENAPI_V2
                        // Issue #198: Restore $refs for properties that were not meaningfully modified by rules.
                        OpenApiSchemaCompatibility.RestoreUnmodifiedRefs(schemaToProcess, refSnapshot, context.SchemaRepository);
#endif
                    }
                }
            }

            // 2) Copy constraints to operation parameters and mark them required (Issue #209).
            foreach (var item in schemasForParameters)
            {
                var itemParameterDescription = item.ParameterDescription;
                var fullParameterName = itemParameterDescription.ModelMetadata?.BinderModelName ?? itemParameterDescription.Name;

                // A dot is legal in a header name ([FromHeader(Name = "X.Trace.Id")]), so the nested
                // [FromQuery] dot-path logic below must not apply to header-bound parameters.
                var isHeaderParameter = itemParameterDescription.Source?.Id == "Header";

                // Issue #211/#213: For a flattened nested [FromQuery] parameter only copy the nested type's
                // value constraints when the SetValidator/ChildRules chain from the ROOT validator actually
                // reaches the leaf container. Otherwise FluentValidation never enforces them at runtime.
                if (!isHeaderParameter
                    && fullParameterName != null && fullParameterName.IndexOf('.') >= 0
                    && !IsNestedValidationReachable(fullParameterName, item.ApiDescription))
                {
                    continue;
                }

                var schemaPropertyName = fullParameterName;

                // For nested [FromQuery] parameters (e.g., "operation.op"), use only the leaf property name.
                if (!isHeaderParameter && schemaPropertyName != null)
                {
                    var dotIndex = schemaPropertyName.LastIndexOf('.');
                    if (dotIndex >= 0)
                        schemaPropertyName = schemaPropertyName.Substring(dotIndex + 1);
                }

                var schema = item.Schema;

                if (schemaPropertyName == null || schema.Properties == null || schema.Properties.Count == 0)
                    continue;

                // Issue #230: resolve the schema property key and use it for BOTH required-marking and the
                // constraint copy below. The symbol-only match handles camelCase / PascalCase / kebab-case
                // aliases ("X-Correlation-Id" -> "XCorrelationId"); the NameResolver fallback handles renames
                // beyond separators (e.g. [JsonPropertyName]) — matching the operation filter.
                var resolvedName = OpenApiSchemaCompatibility.GetProperties(schema)
                    .Select(property => property.Key)
                    .FirstOrDefault(key => key.EqualsIgnoreAll(schemaPropertyName));
                if (resolvedName != null)
                {
                    schemaPropertyName = resolvedName;
                }
                else
                {
                    var propertyInfo = item.ModelType.GetProperty(schemaPropertyName);
                    if (propertyInfo != null && _schemaGenerationOptions.NameResolver != null)
                        schemaPropertyName = _schemaGenerationOptions.NameResolver.GetPropertyName(propertyInfo);
                }

                // Issue #209: a required leaf is only a required parameter when the WHOLE dot-path is required.
                // Header parameters are flat by definition — their dots are not path separators.
                // Required is set on the PARAMETER, independent of the schema cast guard below (a $ref-typed
                // parameter schema must not suppress required-marking — matching the operation filter).
                if (fullParameterName != null
                    && OpenApiSchemaCompatibility.RequiredContains(schema, schemaPropertyName)
                    && (isHeaderParameter || _requiredResolver.IsParameterPathRequired(
                        fullParameterName,
                        AsParametersHelper.GetMethodInfo(item.ApiDescription),
                        context.SchemaRepository,
                        context.SchemaGenerator,
                        schemaProvider)))
                {
#if OPENAPI_V2
                    // In OpenApi 2.x, IOpenApiParameter.Required is read-only; cast to the concrete type to set it.
                    if (item.Parameter is OpenApiParameter openApiParameter)
                        openApiParameter.Required = true;
#else
                    if (item.Parameter != null)
                        item.Parameter.Required = true;
#endif
                }

                var parameterSchema = item.ParameterSchema;
                if (parameterSchema != null)
                {
                    if (OpenApiSchemaCompatibility.TryGetProperty(schema, schemaPropertyName.ToLowerCamelCase(), out var property, context.SchemaRepository)
                        || OpenApiSchemaCompatibility.TryGetProperty(schema, schemaPropertyName, out property, context.SchemaRepository))
                    {
                        if (property != null)
                        {
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
                        }
                    }
                }
            }

            // 3) Apply rules to request bodies ([FromForm] + encoding.contentType, Issue #216) —
            // shared logic with the operation filter.
            foreach (var apiDescription in apiDescriptions)
            {
                var operation = FindOperation(apiDescription);
                if (operation != null)
                {
                    _requestBodyApplicator.ApplyRulesToRequestBody(
                        operation,
                        apiDescription,
                        context.SchemaRepository,
                        context.SchemaGenerator,
                        schemaProvider);
                }
            }

            // 4) Issue #180: Remove schemas that we created as a side-effect of GetSchemaForType().
            // These schemas were not created by Swashbuckle and are not referenced elsewhere.
            if (existingSchemaIds != null)
            {
                var schemasToRemove = schemaRepositorySchemas.Keys
                    .Where(key => !existingSchemaIds.Contains(key))
                    .ToList();

                foreach (var schemaId in schemasToRemove)
                {
#if OPENAPI_V2
                    // Issue #226: clear Swashbuckle's internal reserved-id together with the removal,
                    // so third-party document filters running after this one never observe the
                    // reserved-but-removed state for the container types this filter requested.
                    // ReplaceSchemaId (Swashbuckle 10.1.0+) must run BEFORE the removal.
                    var trackedType = schemaProvider.RequestedSchemaIds
                        .FirstOrDefault(pair => pair.Value == schemaId).Key;
                    if (trackedType != null)
                    {
                        var tempSchemaId = "__fv_removed_" + Guid.NewGuid().ToString("N");
                        if (context.SchemaRepository.ReplaceSchemaId(trackedType, tempSchemaId))
                        {
                            schemaRepositorySchemas.Remove(tempSchemaId);
                            continue;
                        }
                    }
#endif
                    schemaRepositorySchemas.Remove(schemaId);
                }
            }
        }

        /// <summary>
        /// Issue #211/#213: checks that a flattened nested [FromQuery] parameter's leaf type is actually
        /// validated — i.e. the SetValidator/ChildRules chain from the root validator reaches the leaf container.
        /// </summary>
        private bool IsNestedValidationReachable(string parameterName, ApiDescription? apiDescription)
        {
            try
            {
                var segments = parameterName.Split('.');

                var methodInfo = apiDescription != null ? AsParametersHelper.GetMethodInfo(apiDescription) : null;
                var rootType = AsParametersHelper.ResolveRootType(segments[0], methodInfo);

                // Cannot resolve the root container — preserve prior behavior.
                if (rootType == null)
                    return true;

                var rootValidator = _validatorRegistry.GetValidator(rootType);

                // No validator for the bound type — runtime validates nothing along this path.
                if (rootValidator == null)
                    return false;

                var ancestorMembers = new string[segments.Length - 1];
                Array.Copy(segments, ancestorMembers, segments.Length - 1);

                return FluentValidationExtensions.IsNestedValidationWired(rootValidator, ancestorMembers, _schemaGenerationOptions);
            }
            catch (Exception e)
            {
                _logger.LogDebug(e, "Could not determine nested validation reachability for parameter '{ParameterName}'; assuming reachable.", parameterName);
                return true;
            }
        }

        private void ApplyRulesToSchema(SchemaGenerationContext schemaGenerationContext, IValidator validator)
        {
            FluentValidationSchemaBuilder.ApplyRulesToSchema(
                schemaType: schemaGenerationContext.SchemaType,
                schemaPropertyNames: schemaGenerationContext.Properties,
                validator: validator,
                logger: _logger,
                schemaGenerationContext: schemaGenerationContext);
        }

        /// <summary>
        /// Adds rules from included validators (SetValidator/Include). Mirrors the private counterpart in
        /// <see cref="FluentValidationRules"/>; both delegate to <see cref="FluentValidationSchemaBuilder"/>.
        /// </summary>
        private void AddRulesFromIncludedValidators(SchemaGenerationContext schemaGenerationContext, ValidatorContext validatorContext)
        {
            FluentValidationSchemaBuilder.AddRulesFromIncludedValidators(
                validatorContext: validatorContext,
                logger: _logger,
                schemaGenerationContext: schemaGenerationContext);
        }
    }
}
