// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace MicroElements.AspNetCore.OpenApi.FluentValidation.Tests;

/// <summary>
/// Issue #216: IFormFile media type / size for the native Microsoft.AspNetCore.OpenApi backend.
/// The allowed content types and size limit are documented on the file part description, and the allowed
/// content types are also emitted as a machine-readable <c>encoding.contentType</c> on the multipart media type.
/// On net9 the multipart schema is inlined (description on the <c>file</c> property); on net10 the file part
/// is a <c>$ref</c> to a shared <c>IFormFile</c> component (description on that component).
/// </summary>
public class Issue216SpikeTests : IClassFixture<AspNetCoreOpenApiTests.TestWebApplicationFactory>
{
    private readonly AspNetCoreOpenApiTests.TestWebApplicationFactory _factory;

    public Issue216SpikeTests(AspNetCoreOpenApiTests.TestWebApplicationFactory factory) => _factory = factory;

    private async Task<JsonElement> GetMultipartMediaTypeAsync()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();

        // Clone so the returned element does not depend on the disposed JsonDocument's pooled buffer.
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("paths").GetProperty("/api/upload")
            .GetProperty("post").GetProperty("requestBody")
            .GetProperty("content").GetProperty("multipart/form-data").Clone();
    }

    [Fact]
    public async Task FileContentType_And_MaxFileSize_Are_Documented_For_Upload_Endpoint()
    {
        var client = _factory.CreateClient();
        var json = await (await client.GetAsync("/openapi/v1.json")).Content.ReadAsStringAsync();

        // The allowed content types and the size limit are documented (inline on net9, on the shared
        // IFormFile component on net10) — verified at document level so it is robust to inline-vs-$ref.
        json.Should().Contain("Allowed content types: image/jpeg, image/png");
        json.Should().Contain("Maximum file size: 2097152 bytes");
    }

    [Fact]
    public async Task FileContentType_Emits_Encoding_ContentType_For_Upload_Endpoint()
    {
        var multipart = await GetMultipartMediaTypeAsync();

        multipart.TryGetProperty("encoding", out var encoding)
            .Should().BeTrue("the multipart media type should carry an encoding object for the file part");

        // The "File" part's encoding.contentType lists the allowed media types (comma-joined, spec-style).
        var fileEncoding = encoding.EnumerateObject()
            .First(property => property.Name.Equals("File", StringComparison.OrdinalIgnoreCase))
            .Value;
        fileEncoding.GetProperty("contentType").GetString().Should().Be("image/jpeg, image/png");
    }
}
