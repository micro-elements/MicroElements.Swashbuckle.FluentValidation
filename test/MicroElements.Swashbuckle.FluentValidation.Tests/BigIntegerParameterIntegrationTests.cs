// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using System.Net.Http;
using System.Numerics;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using FluentValidation;
using MicroElements.Swashbuckle.FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MicroElements.Swashbuckle.FluentValidation.Tests
{
    /// <summary>
    /// Integration tests verifying that BigInteger validation rules are applied
    /// to operation parameters (via OperationFilter/DocumentFilter) on all TFMs.
    /// Issue #146: BigInteger rendered as $ref on net10.0 should still get validation constraints.
    /// </summary>
    public class BigIntegerParameterIntegrationTests
    {
        public record BigIntegerRequest(BigInteger Limit, string Name);

        public class BigIntegerRequestValidator : AbstractValidator<BigIntegerRequest>
        {
            public BigIntegerRequestValidator()
            {
                RuleFor(x => x.Limit).InclusiveBetween(new BigInteger(1), new BigInteger(1000));
                RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
            }
        }

        private async Task<(JsonElement Doc, WebApplication App)> CreateAppAndGetSwaggerJson()
        {
            var builder = WebApplication.CreateBuilder();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            builder.Services.AddFluentValidationRulesToSwagger();
            builder.Services.AddScoped<IValidator<BigIntegerRequest>, BigIntegerRequestValidator>();

            var app = builder.Build();
            app.Urls.Add("http://127.0.0.1:0");
            app.UseSwagger();
            app.MapGet("/api/data", ([AsParameters] BigIntegerRequest request) => Results.Ok());

            await app.StartAsync();

            // Fetch Swagger JSON via HTTP to avoid OpenApi serialization API differences across TFMs
            var address = app.Urls.First();
            using var client = new HttpClient { BaseAddress = new System.Uri(address) };
            var json = await client.GetStringAsync("/swagger/v1/swagger.json");
            var doc = JsonDocument.Parse(json);

            return (doc.RootElement, app);
        }

        /// <summary>
        /// Verifies that BigInteger [AsParameters] properties have validation constraints
        /// applied in the Swagger output. This tests the full pipeline including
        /// OperationFilter's TryGetProperty → copy step.
        /// </summary>
        [Fact]
        public async Task BigInteger_AsParameters_Should_Have_Validation_Constraints()
        {
            var (doc, app) = await CreateAppAndGetSwaggerJson();

            try
            {
                var parameters = doc.GetProperty("paths")
                    .GetProperty("/api/data")
                    .GetProperty("get")
                    .GetProperty("parameters");

                // Find Limit parameter
                JsonElement? limitParam = null;
                JsonElement? nameParam = null;
                foreach (var param in parameters.EnumerateArray())
                {
                    var name = param.GetProperty("name").GetString();
                    if (name == "Limit" || name == "limit") limitParam = param;
                    if (name == "Name" || name == "name") nameParam = param;
                }

                limitParam.Should().NotBeNull("Limit parameter should exist");
                nameParam.Should().NotBeNull("Name parameter should exist");

                // Name should have standard string constraints (works on all TFMs)
                var nameSchema = nameParam!.Value.GetProperty("schema");
                nameSchema.GetProperty("minLength").GetInt32().Should().Be(1, "NotEmpty sets minLength=1");
                nameSchema.GetProperty("maxLength").GetInt32().Should().Be(50, "MaximumLength(50) sets maxLength=50");

                // Limit (BigInteger) should have min/max constraints
                // This is the key assertion: on net10.0, BigInteger is $ref → TryGetProperty must resolve it
                var limitSchema = limitParam!.Value.GetProperty("schema");
                limitSchema.TryGetProperty("minimum", out var minimum).Should().BeTrue(
                    "InclusiveBetween(1, 1000) should set minimum on BigInteger parameter");
                minimum.GetInt32().Should().Be(1);

                limitSchema.TryGetProperty("maximum", out var maximum).Should().BeTrue(
                    "InclusiveBetween(1, 1000) should set maximum on BigInteger parameter");
                maximum.GetInt32().Should().Be(1000);
            }
            finally
            {
                await app.StopAsync();
            }
        }
    }
}
