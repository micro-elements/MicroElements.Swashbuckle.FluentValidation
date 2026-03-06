// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !OPENAPI_V2

using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using FluentValidation;
using MicroElements.Swashbuckle.FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Swagger;
using Xunit;

namespace MicroElements.Swashbuckle.FluentValidation.Tests
{
    /// <summary>
    /// Integration tests for [AsParameters] support.
    /// These tests use a real WebApplication to verify end-to-end behavior.
    /// Issue #180: https://github.com/micro-elements/MicroElements.Swashbuckle.FluentValidation/issues/180
    /// </summary>
    public class AsParametersIntegrationTests
    {
        public record WeatherForecastRequest(int Page, int PageSize);

        public class WeatherForecastRequestValidator : AbstractValidator<WeatherForecastRequest>
        {
            public WeatherForecastRequestValidator()
            {
                RuleFor(x => x.Page).GreaterThan(0);
                RuleFor(x => x.PageSize).GreaterThan(0).LessThanOrEqualTo(500);
            }
        }

        private async Task<(OpenApiDocument Doc, WebApplication App)> CreateAppAndGetSwagger()
        {
            var builder = WebApplication.CreateBuilder();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            builder.Services.AddFluentValidationRulesToSwagger();
            builder.Services.AddScoped<IValidator<WeatherForecastRequest>, WeatherForecastRequestValidator>();

            var app = builder.Build();
            app.Urls.Add("http://127.0.0.1:0");
            app.MapGet("/weatherforecast", ([AsParameters] WeatherForecastRequest request) => Results.Ok());

            await app.StartAsync();

            var swagger = app.Services.GetRequiredService<ISwaggerProvider>();
            var doc = swagger.GetSwagger("v1");

            return (doc, app);
        }

        /// <summary>
        /// Verifies that [AsParameters] endpoints have validation constraints in Swagger output.
        /// On .NET 8, ContainerType is null for [AsParameters] decomposed parameters.
        /// The AsParametersHelper fallback should resolve the container type via reflection.
        /// </summary>
        [Fact]
        public async Task AsParameters_Should_Have_Validation_Constraints_In_Swagger()
        {
            var (doc, app) = await CreateAppAndGetSwagger();

            try
            {
                var parameters = doc.Paths["/weatherforecast"].Operations.Values.First().Parameters;

                var page = parameters.First(p => p.Name == "Page");
                page.Schema.Minimum.Should().Be(0, because: "GreaterThan(0) should set Minimum to 0");
                page.Schema.ExclusiveMinimum.Should().Be(true, because: "GreaterThan(0) should set ExclusiveMinimum");

                var pageSize = parameters.First(p => p.Name == "PageSize");
                pageSize.Schema.Minimum.Should().Be(0, because: "GreaterThan(0) should set Minimum to 0");
                pageSize.Schema.ExclusiveMinimum.Should().Be(true, because: "GreaterThan(0) should set ExclusiveMinimum");
                pageSize.Schema.Maximum.Should().Be(500, because: "LessThanOrEqualTo(500) should set Maximum to 500");
            }
            finally
            {
                await app.StopAsync();
            }
        }

        /// <summary>
        /// Verifies that [AsParameters] does not create unused schemas in components/schemas.
        /// </summary>
        [Fact]
        public async Task AsParameters_Should_Not_Create_Unused_Schemas()
        {
            var (doc, app) = await CreateAppAndGetSwagger();

            try
            {
                // [AsParameters] types should NOT appear in components/schemas
                doc.Components?.Schemas?.Should().NotContainKey("WeatherForecastRequest",
                    because: "[AsParameters] types are expanded into individual parameters, not schema components");
            }
            finally
            {
                await app.StopAsync();
            }
        }
    }
}

#endif
