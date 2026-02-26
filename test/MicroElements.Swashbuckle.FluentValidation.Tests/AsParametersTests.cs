// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using FluentValidation;
using MicroElements.OpenApi.FluentValidation;
using MicroElements.Swashbuckle.FluentValidation.Generation;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Options;
#if OPENAPI_V2
using Microsoft.OpenApi;
#else
using Microsoft.OpenApi.Models;
#endif
using Swashbuckle.AspNetCore.SwaggerGen;
using Xunit;

namespace MicroElements.Swashbuckle.FluentValidation.Tests
{
    /// <summary>
    /// Issue #180: [AsParameters] types should not create unused schemas in components/schemas.
    /// https://github.com/micro-elements/MicroElements.Swashbuckle.FluentValidation/issues/180
    /// </summary>
    public class AsParametersTests : UnitTestBase
    {
        public class SearchRequest
        {
            public string Query { get; set; }
            public int Page { get; set; }
        }

        public class SearchRequestValidator : AbstractValidator<SearchRequest>
        {
            public SearchRequestValidator()
            {
                RuleFor(x => x.Query).NotEmpty().MaximumLength(200);
                RuleFor(x => x.Page).GreaterThan(0);
            }
        }

        public class AnotherModel
        {
            public string Name { get; set; }
            public int Value { get; set; }
        }

        /// <summary>
        /// Verifies that GetSchemaForType() registers a schema in SchemaRepository as a side-effect.
        /// This is the root cause of issue #180: for [AsParameters] types, this side-effect creates
        /// unused schemas because Swashbuckle expands these types into individual parameters.
        /// </summary>
        [Fact]
        public void GetSchemaForType_Creates_Schema_In_Repository_As_SideEffect()
        {
            var schemaRepository = new SchemaRepository();
            var schemaGenerator = SchemaGenerator(new SearchRequestValidator());
            var schemaProvider = new SwashbuckleSchemaProvider(schemaRepository, schemaGenerator);

            schemaRepository.Schemas.Should().BeEmpty();

            // GetSchemaForType creates schema as side-effect
            schemaProvider.GetSchemaForType(typeof(SearchRequest));

            schemaRepository.Schemas.Should().ContainKey("SearchRequest");
        }

        /// <summary>
        /// Verifies the check-and-cleanup approach: schemas created by our code (not by Swashbuckle)
        /// are removed after processing, while pre-existing schemas are preserved.
        /// </summary>
        [Fact]
        public void Schema_Cleanup_Should_Remove_Only_Newly_Created_Schemas()
        {
            var schemaRepository = new SchemaRepository();
            var schemaGenerator = SchemaGenerator(new SearchRequestValidator());

            // Pre-populate with a schema that "Swashbuckle" would have created (e.g. for [FromBody])
            schemaGenerator.GenerateSchema(typeof(SearchRequest), schemaRepository);
            schemaRepository.Schemas.Should().ContainKey("SearchRequest");
            var originalCount = schemaRepository.Schemas.Count;

            // Snapshot existing schemas (this is what our fix does)
            var existingSchemaIds = new HashSet<string>(schemaRepository.Schemas.Keys);

            // Create another type's schema as side-effect (simulating [AsParameters] container type)
            var schemaProvider = new SwashbuckleSchemaProvider(schemaRepository, schemaGenerator);
            schemaProvider.GetSchemaForType(typeof(AnotherModel));

            schemaRepository.Schemas.Should().ContainKey("AnotherModel");

            // Cleanup: remove schemas that were not in the original snapshot
            foreach (var schemaId in schemaRepository.Schemas.Keys.ToArray())
            {
                if (!existingSchemaIds.Contains(schemaId))
                    schemaRepository.Schemas.Remove(schemaId);
            }

            // The pre-existing schema should be preserved
            schemaRepository.Schemas.Should().ContainKey("SearchRequest",
                because: "schemas created by Swashbuckle before our processing should be preserved");

            // The side-effect schema should be removed
            schemaRepository.Schemas.Should().NotContainKey("AnotherModel",
                because: "schemas created as a side-effect of GetSchemaForType should be cleaned up");

            schemaRepository.Schemas.Count.Should().Be(originalCount);
        }

        /// <summary>
        /// Verifies that the default value of RemoveUnusedQuerySchemas is true.
        /// </summary>
        [Fact]
        public void Default_RemoveUnusedQuerySchemas_Should_Be_True()
        {
            var options = new SchemaGenerationOptions();
            options.RemoveUnusedQuerySchemas.Should().BeTrue(
                because: "the default should preserve v7.0.4 behavior of cleaning up unused schemas");
        }

// OperationFilter integration tests require framework-specific OpenApi types.
// Use #if to handle Swashbuckle v8/v9 vs v10 (OPENAPI_V2) differences.
#if !OPENAPI_V2
        /// <summary>
        /// Verifies that the OperationFilter cleans up container type schemas that were created
        /// as a side-effect of GetSchemaForType(), while still applying validation rules to parameters.
        /// </summary>
        [Fact]
        public void OperationFilter_Should_Cleanup_ContainerType_Schemas()
        {
            // Arrange
            var schemaGeneratorOptions = new SchemaGeneratorOptions();
            var schemaRepository = new SchemaRepository();
            var schemaGenerator = SchemaGenerator(new SearchRequestValidator());

            var schemaGenerationOptions = new SchemaGenerationOptions
            {
                NameResolver = new SystemTextJsonNameResolver(),
                SchemaIdSelector = schemaGeneratorOptions.SchemaIdSelector,
            };

            var validatorRegistry = new ValidatorRegistry(
                new IValidator[] { new SearchRequestValidator() },
                new OptionsWrapper<SchemaGenerationOptions>(schemaGenerationOptions));

            var metadataProvider = new EmptyModelMetadataProvider();
            var queryMetadata = metadataProvider.GetMetadataForProperty(typeof(SearchRequest), nameof(SearchRequest.Query));
            var pageMetadata = metadataProvider.GetMetadataForProperty(typeof(SearchRequest), nameof(SearchRequest.Page));

            var apiDescription = new ApiDescription();
            apiDescription.ParameterDescriptions.Add(new ApiParameterDescription
            {
                Name = "Query",
                ModelMetadata = queryMetadata,
                Source = BindingSource.Query,
            });
            apiDescription.ParameterDescriptions.Add(new ApiParameterDescription
            {
                Name = "Page",
                ModelMetadata = pageMetadata,
                Source = BindingSource.Query,
            });

            var operation = new OpenApiOperation
            {
                Parameters = new List<OpenApiParameter>
                {
                    new OpenApiParameter { Name = "Query", In = ParameterLocation.Query, Schema = new OpenApiSchema { Type = "string" } },
                    new OpenApiParameter { Name = "Page", In = ParameterLocation.Query, Schema = new OpenApiSchema { Type = "integer" } },
                },
            };

            var context = new OperationFilterContext(
                apiDescription,
                schemaGenerator,
                schemaRepository,
                typeof(AsParametersTests).GetMethod(nameof(OperationFilter_Should_Cleanup_ContainerType_Schemas))!);

            schemaRepository.Schemas.Should().BeEmpty();

            // Act
            var operationFilter = new FluentValidationOperationFilter(
                validatorRegistry: validatorRegistry,
                schemaGenerationOptions: new OptionsWrapper<SchemaGenerationOptions>(schemaGenerationOptions));

            operationFilter.Apply(operation, context);

            // Assert: SchemaRepository should NOT contain SearchRequest schema
            schemaRepository.Schemas.Should().NotContainKey("SearchRequest",
                because: "container type schemas created as a side-effect should be cleaned up (Issue #180)");
        }

        /// <summary>
        /// Verifies that validation rules are still applied to individual operation parameters
        /// even after the schema cleanup.
        /// </summary>
        [Fact]
        public void OperationFilter_Should_Still_Apply_Validation_Rules_To_Parameters()
        {
            // Arrange
            var schemaGeneratorOptions = new SchemaGeneratorOptions();
            var schemaRepository = new SchemaRepository();
            var schemaGenerator = SchemaGenerator(new SearchRequestValidator());

            var schemaGenerationOptions = new SchemaGenerationOptions
            {
                NameResolver = new SystemTextJsonNameResolver(),
                SchemaIdSelector = schemaGeneratorOptions.SchemaIdSelector,
            };

            var validatorRegistry = new ValidatorRegistry(
                new IValidator[] { new SearchRequestValidator() },
                new OptionsWrapper<SchemaGenerationOptions>(schemaGenerationOptions));

            var metadataProvider = new EmptyModelMetadataProvider();
            var queryMetadata = metadataProvider.GetMetadataForProperty(typeof(SearchRequest), nameof(SearchRequest.Query));
            var pageMetadata = metadataProvider.GetMetadataForProperty(typeof(SearchRequest), nameof(SearchRequest.Page));

            var apiDescription = new ApiDescription();
            apiDescription.ParameterDescriptions.Add(new ApiParameterDescription
            {
                Name = "Query",
                ModelMetadata = queryMetadata,
                Source = BindingSource.Query,
            });
            apiDescription.ParameterDescriptions.Add(new ApiParameterDescription
            {
                Name = "Page",
                ModelMetadata = pageMetadata,
                Source = BindingSource.Query,
            });

            var queryParamSchema = new OpenApiSchema { Type = "string" };
            var pageParamSchema = new OpenApiSchema { Type = "integer" };

            var operation = new OpenApiOperation
            {
                Parameters = new List<OpenApiParameter>
                {
                    new OpenApiParameter { Name = "Query", In = ParameterLocation.Query, Schema = queryParamSchema },
                    new OpenApiParameter { Name = "Page", In = ParameterLocation.Query, Schema = pageParamSchema },
                },
            };

            var context = new OperationFilterContext(
                apiDescription,
                schemaGenerator,
                schemaRepository,
                typeof(AsParametersTests).GetMethod(nameof(OperationFilter_Should_Still_Apply_Validation_Rules_To_Parameters))!);

            // Act
            var operationFilter = new FluentValidationOperationFilter(
                validatorRegistry: validatorRegistry,
                schemaGenerationOptions: new OptionsWrapper<SchemaGenerationOptions>(schemaGenerationOptions));

            operationFilter.Apply(operation, context);

            // Assert: Validation rules should be applied to the parameter schemas
            queryParamSchema.MinLength.Should().Be(1, because: "NotEmpty should set MinLength to 1");
            queryParamSchema.MaxLength.Should().Be(200, because: "MaximumLength(200) should set MaxLength to 200");

            pageParamSchema.GetMinimum().Should().Be(0, because: "GreaterThan(0) should set Minimum to 0");
            pageParamSchema.GetExclusiveMinimum().Should().Be(true, because: "GreaterThan(0) should set ExclusiveMinimum");
        }

        /// <summary>
        /// Verifies that schemas which were already in the repository (created by Swashbuckle
        /// for [FromBody] types) are NOT removed by the cleanup.
        /// </summary>
        [Fact]
        public void OperationFilter_Should_Preserve_PreExisting_Schemas()
        {
            // Arrange
            var schemaGeneratorOptions = new SchemaGeneratorOptions();
            var schemaRepository = new SchemaRepository();
            var schemaGenerator = SchemaGenerator(new SearchRequestValidator());

            var schemaGenerationOptions = new SchemaGenerationOptions
            {
                NameResolver = new SystemTextJsonNameResolver(),
                SchemaIdSelector = schemaGeneratorOptions.SchemaIdSelector,
            };

            var validatorRegistry = new ValidatorRegistry(
                new IValidator[] { new SearchRequestValidator() },
                new OptionsWrapper<SchemaGenerationOptions>(schemaGenerationOptions));

            // Pre-populate SchemaRepository with a schema (simulating Swashbuckle having created it for [FromBody])
            schemaGenerator.GenerateSchema(typeof(SearchRequest), schemaRepository);
            schemaRepository.Schemas.Should().ContainKey("SearchRequest");

            var metadataProvider = new EmptyModelMetadataProvider();
            var queryMetadata = metadataProvider.GetMetadataForProperty(typeof(SearchRequest), nameof(SearchRequest.Query));

            var apiDescription = new ApiDescription();
            apiDescription.ParameterDescriptions.Add(new ApiParameterDescription
            {
                Name = "Query",
                ModelMetadata = queryMetadata,
                Source = BindingSource.Query,
            });

            var operation = new OpenApiOperation
            {
                Parameters = new List<OpenApiParameter>
                {
                    new OpenApiParameter { Name = "Query", In = ParameterLocation.Query, Schema = new OpenApiSchema { Type = "string" } },
                },
            };

            var context = new OperationFilterContext(
                apiDescription,
                schemaGenerator,
                schemaRepository,
                typeof(AsParametersTests).GetMethod(nameof(OperationFilter_Should_Preserve_PreExisting_Schemas))!);

            // Act
            var operationFilter = new FluentValidationOperationFilter(
                validatorRegistry: validatorRegistry,
                schemaGenerationOptions: new OptionsWrapper<SchemaGenerationOptions>(schemaGenerationOptions));

            operationFilter.Apply(operation, context);

            // Assert: Pre-existing schema should still be present
            schemaRepository.Schemas.Should().ContainKey("SearchRequest",
                because: "schemas that existed before OperationFilter processing should be preserved");
        }

        /// <summary>
        /// Verifies that when RemoveUnusedQuerySchemas is false, container type schemas
        /// created as a side-effect of GetSchemaForType() are preserved in SchemaRepository.
        /// This supports workflows where custom DocumentFilters consume these schemas.
        /// </summary>
        [Fact]
        public void OperationFilter_Should_Preserve_Schemas_When_RemoveUnusedQuerySchemas_Is_False()
        {
            // Arrange
            var schemaGeneratorOptions = new SchemaGeneratorOptions();
            var schemaRepository = new SchemaRepository();
            var schemaGenerator = SchemaGenerator(new SearchRequestValidator());

            var schemaGenerationOptions = new SchemaGenerationOptions
            {
                NameResolver = new SystemTextJsonNameResolver(),
                SchemaIdSelector = schemaGeneratorOptions.SchemaIdSelector,
                RemoveUnusedQuerySchemas = false,
            };

            var validatorRegistry = new ValidatorRegistry(
                new IValidator[] { new SearchRequestValidator() },
                new OptionsWrapper<SchemaGenerationOptions>(schemaGenerationOptions));

            var metadataProvider = new EmptyModelMetadataProvider();
            var queryMetadata = metadataProvider.GetMetadataForProperty(typeof(SearchRequest), nameof(SearchRequest.Query));
            var pageMetadata = metadataProvider.GetMetadataForProperty(typeof(SearchRequest), nameof(SearchRequest.Page));

            var apiDescription = new ApiDescription();
            apiDescription.ParameterDescriptions.Add(new ApiParameterDescription
            {
                Name = "Query",
                ModelMetadata = queryMetadata,
                Source = BindingSource.Query,
            });
            apiDescription.ParameterDescriptions.Add(new ApiParameterDescription
            {
                Name = "Page",
                ModelMetadata = pageMetadata,
                Source = BindingSource.Query,
            });

            var operation = new OpenApiOperation
            {
                Parameters = new List<OpenApiParameter>
                {
                    new OpenApiParameter { Name = "Query", In = ParameterLocation.Query, Schema = new OpenApiSchema { Type = "string" } },
                    new OpenApiParameter { Name = "Page", In = ParameterLocation.Query, Schema = new OpenApiSchema { Type = "integer" } },
                },
            };

            var context = new OperationFilterContext(
                apiDescription,
                schemaGenerator,
                schemaRepository,
                typeof(AsParametersTests).GetMethod(nameof(OperationFilter_Should_Preserve_Schemas_When_RemoveUnusedQuerySchemas_Is_False))!);

            schemaRepository.Schemas.Should().BeEmpty();

            // Act
            var operationFilter = new FluentValidationOperationFilter(
                validatorRegistry: validatorRegistry,
                schemaGenerationOptions: new OptionsWrapper<SchemaGenerationOptions>(schemaGenerationOptions));

            operationFilter.Apply(operation, context);

            // Assert: SchemaRepository SHOULD contain SearchRequest schema (not cleaned up)
            schemaRepository.Schemas.Should().ContainKey("SearchRequest",
                because: "RemoveUnusedQuerySchemas is false, so container type schemas should be preserved for custom DocumentFilters");
        }

        /// <summary>
        /// Verifies that when RemoveUnusedQuerySchemas is false, the DocumentFilter preserves
        /// container type schemas created as a side-effect of GetSchemaForType().
        /// This is the key scenario from the regression: custom DocumentFilters that run after
        /// this filter depend on these schemas being present.
        /// </summary>
        [Fact]
        public void DocumentFilter_Should_Preserve_Schemas_When_RemoveUnusedQuerySchemas_Is_False()
        {
            // Arrange
            var schemaGeneratorOptions = new SchemaGeneratorOptions();
            var schemaRepository = new SchemaRepository();
            var schemaGenerator = SchemaGenerator(new SearchRequestValidator());

            var schemaGenerationOptions = new SchemaGenerationOptions
            {
                NameResolver = new SystemTextJsonNameResolver(),
                SchemaIdSelector = schemaGeneratorOptions.SchemaIdSelector,
                RemoveUnusedQuerySchemas = false,
            };

            var validatorRegistry = new ValidatorRegistry(
                new IValidator[] { new SearchRequestValidator() },
                new OptionsWrapper<SchemaGenerationOptions>(schemaGenerationOptions));

            var metadataProvider = new EmptyModelMetadataProvider();
            var queryMetadata = metadataProvider.GetMetadataForProperty(typeof(SearchRequest), nameof(SearchRequest.Query));
            var pageMetadata = metadataProvider.GetMetadataForProperty(typeof(SearchRequest), nameof(SearchRequest.Page));

            var apiDescription = new ApiDescription { RelativePath = "api/search" };
            apiDescription.ParameterDescriptions.Add(new ApiParameterDescription
            {
                Name = "Query",
                ModelMetadata = queryMetadata,
                Source = BindingSource.Query,
            });
            apiDescription.ParameterDescriptions.Add(new ApiParameterDescription
            {
                Name = "Page",
                ModelMetadata = pageMetadata,
                Source = BindingSource.Query,
            });

            var swaggerDoc = new OpenApiDocument
            {
                Paths = new OpenApiPaths
                {
                    ["/api/search"] = new OpenApiPathItem
                    {
                        Operations = new Dictionary<OperationType, OpenApiOperation>
                        {
                            [OperationType.Get] = new OpenApiOperation
                            {
                                Parameters = new List<OpenApiParameter>
                                {
                                    new OpenApiParameter { Name = "Query", In = ParameterLocation.Query, Schema = new OpenApiSchema { Type = "string" } },
                                    new OpenApiParameter { Name = "Page", In = ParameterLocation.Query, Schema = new OpenApiSchema { Type = "integer" } },
                                },
                            },
                        },
                    },
                },
            };

            var documentFilterContext = new DocumentFilterContext(
                new[] { apiDescription },
                schemaGenerator,
                schemaRepository);

            schemaRepository.Schemas.Should().BeEmpty();

            // Act
            var documentFilter = new FluentValidationDocumentFilter(
                validatorRegistry: validatorRegistry,
                schemaGenerationOptions: new OptionsWrapper<SchemaGenerationOptions>(schemaGenerationOptions));

            documentFilter.Apply(swaggerDoc, documentFilterContext);

            // Assert: SchemaRepository SHOULD contain SearchRequest schema (not cleaned up)
            schemaRepository.Schemas.Should().ContainKey("SearchRequest",
                because: "RemoveUnusedQuerySchemas is false, so container type schemas should be preserved for custom DocumentFilters");
        }

        /// <summary>
        /// Verifies that with the default RemoveUnusedQuerySchemas=true, the DocumentFilter
        /// removes container type schemas created as a side-effect.
        /// </summary>
        [Fact]
        public void DocumentFilter_Should_Cleanup_ContainerType_Schemas_By_Default()
        {
            // Arrange
            var schemaGeneratorOptions = new SchemaGeneratorOptions();
            var schemaRepository = new SchemaRepository();
            var schemaGenerator = SchemaGenerator(new SearchRequestValidator());

            var schemaGenerationOptions = new SchemaGenerationOptions
            {
                NameResolver = new SystemTextJsonNameResolver(),
                SchemaIdSelector = schemaGeneratorOptions.SchemaIdSelector,
            };

            var validatorRegistry = new ValidatorRegistry(
                new IValidator[] { new SearchRequestValidator() },
                new OptionsWrapper<SchemaGenerationOptions>(schemaGenerationOptions));

            var metadataProvider = new EmptyModelMetadataProvider();
            var queryMetadata = metadataProvider.GetMetadataForProperty(typeof(SearchRequest), nameof(SearchRequest.Query));
            var pageMetadata = metadataProvider.GetMetadataForProperty(typeof(SearchRequest), nameof(SearchRequest.Page));

            var apiDescription = new ApiDescription { RelativePath = "api/search" };
            apiDescription.ParameterDescriptions.Add(new ApiParameterDescription
            {
                Name = "Query",
                ModelMetadata = queryMetadata,
                Source = BindingSource.Query,
            });
            apiDescription.ParameterDescriptions.Add(new ApiParameterDescription
            {
                Name = "Page",
                ModelMetadata = pageMetadata,
                Source = BindingSource.Query,
            });

            var swaggerDoc = new OpenApiDocument
            {
                Paths = new OpenApiPaths
                {
                    ["/api/search"] = new OpenApiPathItem
                    {
                        Operations = new Dictionary<OperationType, OpenApiOperation>
                        {
                            [OperationType.Get] = new OpenApiOperation
                            {
                                Parameters = new List<OpenApiParameter>
                                {
                                    new OpenApiParameter { Name = "Query", In = ParameterLocation.Query, Schema = new OpenApiSchema { Type = "string" } },
                                    new OpenApiParameter { Name = "Page", In = ParameterLocation.Query, Schema = new OpenApiSchema { Type = "integer" } },
                                },
                            },
                        },
                    },
                },
            };

            var documentFilterContext = new DocumentFilterContext(
                new[] { apiDescription },
                schemaGenerator,
                schemaRepository);

            schemaRepository.Schemas.Should().BeEmpty();

            // Act
            var documentFilter = new FluentValidationDocumentFilter(
                validatorRegistry: validatorRegistry,
                schemaGenerationOptions: new OptionsWrapper<SchemaGenerationOptions>(schemaGenerationOptions));

            documentFilter.Apply(swaggerDoc, documentFilterContext);

            // Assert: SchemaRepository should NOT contain SearchRequest schema (cleaned up)
            schemaRepository.Schemas.Should().NotContainKey("SearchRequest",
                because: "container type schemas created as a side-effect should be cleaned up by default (Issue #180)");
        }
#endif
    }
}
