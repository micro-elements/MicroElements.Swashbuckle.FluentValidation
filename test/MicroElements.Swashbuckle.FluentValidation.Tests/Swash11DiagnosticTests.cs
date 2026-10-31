// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// EXPERIMENT diagnostic (experiment/issue-236-swash11-openapi32): dumps the actual schema the
// Swashbuckle 11 preview generates for AbstractInstitutionModel/InstitutionModel, to explain the
// BaseTypeValidator 'Name' KeyNotFoundException on net11. Temporary; removed with the experiment.

#if NET11_0_OR_GREATER
using System;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace MicroElements.Swashbuckle.FluentValidation.Tests;

public class Swash11DiagnosticTests
{
    [Fact]
    public void Diagnostic_DumpInstitutionModelSchema()
    {
        var host = new SwaggerTestHost()
            .Configure()
            .RegisterValidator<SchemaGenerationTests.AbstractInstitutionModelValidator>();

        host.GenerateSchema<SchemaGenerationTests.AbstractInstitutionModel>(out var schemaBase);
        host.GenerateSchema<SchemaGenerationTests.InstitutionModel>(out var schemaChild);

        var dump = new
        {
            repoKeys = host.SchemaRepository.Schemas.Keys,
            baseProperties = schemaBase.Properties?.Keys,
            childProperties = schemaChild.Properties?.Keys,
            baseJson = JsonSerializer.Serialize(schemaBase),
            childJson = JsonSerializer.Serialize(schemaChild),
        };

        Console.WriteLine("PROBE-SCHEMA " + JsonSerializer.Serialize(dump));
        Assert.True(true);
    }
}
#endif
