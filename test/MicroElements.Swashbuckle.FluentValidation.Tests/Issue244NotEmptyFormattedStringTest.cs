// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using FluentAssertions;
using FluentValidation;
using Xunit;

namespace MicroElements.Swashbuckle.FluentValidation.Tests
{
    /// <summary>
    /// NotEmpty on a value type that is serialized as a formatted string (Guid as uuid, DateTime as date-time, ...)
    /// must not emit minLength: 1. For those types NotEmpty means "not the default value" (Guid.Empty, DateTime.MinValue),
    /// which JSON Schema cannot express, and the serialized string is never empty anyway.
    /// A plain string keeps minLength: 1.
    /// </summary>
    public class Issue244NotEmptyFormattedStringTest : UnitTestBase
    {
        public class Model
        {
            public Guid PersonId { get; set; }
            public Guid? OptionalPersonId { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTimeOffset UpdatedAt { get; set; }
            public DateOnly BirthDate { get; set; }
            public TimeOnly StartTime { get; set; }
            public TimeSpan Duration { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
        }

        // A fresh SchemaBuilder per case: its SchemaRepository caches the generated component,
        // so one builder must generate exactly one rule/schema.

        [Fact]
        public void NotEmpty_Guid_Should_Not_Emit_MinLength()
        {
            var schema = new SchemaBuilder<Model>().AddRule(x => x.PersonId, rule => rule.NotEmpty());
            schema.Format.Should().Be("uuid");
            schema.MinLength.Should().BeNull();
        }

        [Fact]
        public void NotEmpty_Nullable_Guid_Should_Not_Emit_MinLength()
        {
            var schema = new SchemaBuilder<Model>().AddRule(x => x.OptionalPersonId, rule => rule.NotEmpty());
            schema.Format.Should().Be("uuid");
            schema.MinLength.Should().BeNull();
        }

        [Fact]
        public void NotEmpty_DateTime_Should_Not_Emit_MinLength()
        {
            var schema = new SchemaBuilder<Model>().AddRule(x => x.CreatedAt, rule => rule.NotEmpty());
            schema.Format.Should().Be("date-time");
            schema.MinLength.Should().BeNull();
        }

        [Fact]
        public void NotEmpty_DateTimeOffset_Should_Not_Emit_MinLength()
        {
            var schema = new SchemaBuilder<Model>().AddRule(x => x.UpdatedAt, rule => rule.NotEmpty());
            schema.Format.Should().Be("date-time");
            schema.MinLength.Should().BeNull();
        }

        [Fact]
        public void NotEmpty_DateOnly_Should_Not_Emit_MinLength()
        {
            var schema = new SchemaBuilder<Model>().AddRule(x => x.BirthDate, rule => rule.NotEmpty());
            schema.Format.Should().Be("date");
            schema.MinLength.Should().BeNull();
        }

        [Fact]
        public void NotEmpty_TimeOnly_Should_Not_Emit_MinLength()
        {
            var schema = new SchemaBuilder<Model>().AddRule(x => x.StartTime, rule => rule.NotEmpty());
            schema.Format.Should().Be("time");
            schema.MinLength.Should().BeNull();
        }

        [Fact]
        public void NotEmpty_TimeSpan_Should_Not_Emit_MinLength()
        {
            var schema = new SchemaBuilder<Model>().AddRule(x => x.Duration, rule => rule.NotEmpty());
            schema.Format.Should().Be("date-span");
            schema.MinLength.Should().BeNull();
        }

        [Fact]
        public void NotEmpty_String_Should_Emit_MinLength()
        {
            var schema = new SchemaBuilder<Model>().AddRule(x => x.Name, rule => rule.NotEmpty());
            schema.MinLength.Should().Be(1);
        }

        [Fact]
        public void NotEmpty_Email_String_Should_Emit_MinLength()
        {
            var schema = new SchemaBuilder<Model>().AddRule(x => x.Email, rule => rule.EmailAddress().NotEmpty());
            schema.Format.Should().Be("email");
            schema.MinLength.Should().Be(1);
        }
    }
}
