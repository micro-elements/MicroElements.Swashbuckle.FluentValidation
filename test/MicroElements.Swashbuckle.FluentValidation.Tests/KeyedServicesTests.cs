// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using FluentAssertions;
using FluentValidation;
using MicroElements.OpenApi;
using MicroElements.OpenApi.FluentValidation;
using MicroElements.Swashbuckle.FluentValidation.AspNetCore;
using Microsoft.Extensions.DependencyInjection;
using Swashbuckle.AspNetCore.SwaggerGen;
using Xunit;

namespace MicroElements.Swashbuckle.FluentValidation.Tests
{
    /// <summary>
    /// Tests for keyed service support (Issue #165).
    /// </summary>
    public class KeyedServicesTests : UnitTestBase
    {
        public class KeyedModel
        {
            public string? Name { get; set; }

            public int Age { get; set; }
        }

        public class KeyedModelValidator : AbstractValidator<KeyedModel>
        {
            public KeyedModelValidator()
            {
                RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
                RuleFor(x => x.Age).GreaterThan(0);
            }
        }

        [Fact]
        public void Keyed_Validator_Should_Be_Discovered_Via_GetValidators()
        {
            // Realistic ordering: library registered BEFORE validators
            var services = new ServiceCollection();
            services.AddFluentValidationRulesToSwagger();
            services.AddKeyedScoped<IValidator<KeyedModel>, KeyedModelValidator>("myKey");

            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var registry = scope.ServiceProvider.GetRequiredService<IValidatorRegistry>();

            registry.GetValidators(typeof(KeyedModel)).Should().ContainSingle();
        }

        [Fact]
        public void Keyed_Validator_Should_Be_Discovered_Via_GetValidator()
        {
            // Tests singular GetValidator path (used by OperationFilter)
            var services = new ServiceCollection();
            services.AddFluentValidationRulesToSwagger();
            services.AddKeyedScoped<IValidator<KeyedModel>, KeyedModelValidator>("myKey");

            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var registry = scope.ServiceProvider.GetRequiredService<IValidatorRegistry>();

            registry.GetValidator(typeof(KeyedModel)).Should().NotBeNull();
        }

        [Fact]
        public void NonKeyed_Validators_Still_Work()
        {
            var services = new ServiceCollection();
            services.AddFluentValidationRulesToSwagger();
            services.AddScoped<IValidator<KeyedModel>, KeyedModelValidator>();

            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var registry = scope.ServiceProvider.GetRequiredService<IValidatorRegistry>();

            registry.GetValidators(typeof(KeyedModel)).Should().ContainSingle();
        }

        [Fact]
        public void Mixed_Keyed_And_NonKeyed_No_Duplicates()
        {
            var services = new ServiceCollection();
            services.AddFluentValidationRulesToSwagger();
            services.AddScoped<IValidator<KeyedModel>, KeyedModelValidator>();
            services.AddKeyedScoped<IValidator<KeyedModel>, KeyedModelValidator>("myKey");

            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var registry = scope.ServiceProvider.GetRequiredService<IValidatorRegistry>();

            var validators = registry.GetValidators(typeof(KeyedModel)).ToList();
            validators.Should().HaveCount(1, "IsOneValidatorForType is true by default, so only the first (non-keyed) validator is returned");
        }

        [Fact]
        public void Schema_Gets_Validation_Rules_From_Keyed_Validator()
        {
            // Full integration: keyed validator -> schema generation -> rules applied
            var schemaRepository = new SchemaRepository();
            var schema = schemaRepository.GenerateSchemaForValidator(new KeyedModelValidator());

            schema.GetProperty("Name")!.MinLength.Should().Be(1);
            schema.GetProperty("Name")!.MaxLength.Should().Be(100);
            schema.GetProperty("Age")!.GetMinimum().Should().Be(0);
            schema.GetProperty("Age")!.GetExclusiveMinimum().Should().BeTrue();
        }
    }
}
