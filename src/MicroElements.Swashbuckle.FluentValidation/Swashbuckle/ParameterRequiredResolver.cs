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
#if !OPENAPI_V2
using Microsoft.OpenApi.Models;
#endif
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MicroElements.Swashbuckle.FluentValidation
{
    /// <summary>
    /// Issue #209 required-path logic shared by <see cref="FluentValidationOperationFilter"/> and
    /// <see cref="FluentValidationDocumentFilter"/>, so the two pipelines cannot drift (ADR-007).
    /// A (possibly nested) operation parameter may be marked required only when EVERY ancestor
    /// segment of its dot-path is itself required.
    /// </summary>
    internal sealed class ParameterRequiredResolver
    {
        private readonly ILogger _logger;
        private readonly IValidatorRegistry _validatorRegistry;
        private readonly IReadOnlyList<IFluentValidationRule<OpenApiSchema>> _rules;
        private readonly SchemaGenerationOptions _schemaGenerationOptions;

        /// <summary>
        /// Initializes a new instance of the <see cref="ParameterRequiredResolver"/> class.
        /// </summary>
        public ParameterRequiredResolver(
            ILogger logger,
            IValidatorRegistry validatorRegistry,
            IReadOnlyList<IFluentValidationRule<OpenApiSchema>> rules,
            SchemaGenerationOptions schemaGenerationOptions)
        {
            _logger = logger;
            _validatorRegistry = validatorRegistry;
            _rules = rules;
            _schemaGenerationOptions = schemaGenerationOptions;
        }

        /// <summary>
        /// Determines whether a (possibly nested) operation parameter may be marked as required.
        /// For a flattened nested [FromQuery] parameter (e.g. "OptionalSubType.SubProperty") the leaf
        /// is a required parameter only when EVERY ancestor segment of the dot-path is itself required.
        /// If any ancestor (e.g. an optional nested object) is not required, the parameter stays optional.
        /// Issue #209.
        /// </summary>
        public bool IsParameterPathRequired(
            string parameterName,
            MethodInfo? methodInfo,
            SchemaRepository schemaRepository,
            ISchemaGenerator schemaGenerator,
            SwashbuckleSchemaProvider schemaProvider)
        {
            // Flat parameter: no ancestors to verify.
            if (parameterName.IndexOf('.') < 0)
                return true;

            var segments = parameterName.Split('.');

            // Resolve the root [FromQuery]/[AsParameters] type from the action method by matching the first segment.
            var currentType = AsParametersHelper.ResolveRootType(segments[0], methodInfo);

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
                if (!IsPropertyRequiredInType(currentType, segments[i], schemaRepository, schemaGenerator, schemaProvider))
                    return false;

                var propertyInfo = currentType.GetProperty(segments[i], BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (propertyInfo == null)
                    return true; // path does not map to a real property — preserve prior behavior

                currentType = propertyInfo.PropertyType;
            }

            return true;
        }

        /// <summary>
        /// Checks whether <paramref name="propertyName"/> is required in <paramref name="containerType"/>,
        /// combining the generated schema (native required, e.g. the C# 'required' modifier) with the
        /// FluentValidation rules (NotNull/NotEmpty) of the container type's validator.
        /// </summary>
        public bool IsPropertyRequiredInType(
            Type containerType,
            string propertyName,
            SchemaRepository schemaRepository,
            ISchemaGenerator schemaGenerator,
            SwashbuckleSchemaProvider schemaProvider)
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
            // container validator's rules explicitly, then re-check.
            var validator = _validatorRegistry.GetValidator(containerType);
            if (validator != null)
            {
                var schemaContext = new SchemaGenerationContext(
                    schemaRepository: schemaRepository,
                    schemaGenerator: schemaGenerator,
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
    }
}
