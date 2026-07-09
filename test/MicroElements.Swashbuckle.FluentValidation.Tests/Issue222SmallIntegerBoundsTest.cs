// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using FluentValidation;
using Xunit;

namespace MicroElements.Swashbuckle.FluentValidation.Tests
{
    /// <summary>
    /// Issue #222: numeric bounds whose value is a small integer type (sbyte/byte/short/ushort/uint/ulong)
    /// were silently dropped — the Between/Comparison rules matched, but IsNumeric() did not recognize the
    /// boxed value, so minimum/maximum were never emitted. For a `short` property FluentValidation's
    /// InclusiveBetween/GreaterThanOrEqualTo overloads only accept short bounds, so this could not be worked
    /// around at the validator definition site.
    /// https://github.com/micro-elements/MicroElements.Swashbuckle.FluentValidation/issues/222
    /// </summary>
    public class Issue222SmallIntegerBoundsTest : UnitTestBase
    {
        public class Model
        {
            public sbyte SByteValue { get; set; }
            public byte ByteValue { get; set; }
            public short ShortValue { get; set; }
            public ushort UShortValue { get; set; }
            public uint UIntValue { get; set; }
            public ulong ULongValue { get; set; }
        }

        // A fresh SchemaBuilder per case: its SchemaRepository caches the generated component,
        // so one builder must generate exactly one rule/schema.

        [Fact]
        public void InclusiveBetween_SByte_Should_Emit_Bounds()
        {
            var schema = new SchemaBuilder<Model>().AddRule(x => x.SByteValue, rule => rule.InclusiveBetween((sbyte)1, (sbyte)9));
            schema.GetMinimum().Should().Be(1);
            schema.GetMaximum().Should().Be(9);
        }

        [Fact]
        public void InclusiveBetween_Byte_Should_Emit_Bounds()
        {
            var schema = new SchemaBuilder<Model>().AddRule(x => x.ByteValue, rule => rule.InclusiveBetween((byte)2, (byte)200));
            schema.GetMinimum().Should().Be(2);
            schema.GetMaximum().Should().Be(200);
        }

        [Fact]
        public void InclusiveBetween_Short_Should_Emit_Bounds()
        {
            var schema = new SchemaBuilder<Model>().AddRule(x => x.ShortValue, rule => rule.InclusiveBetween((short)1, (short)99));
            schema.GetMinimum().Should().Be(1);
            schema.GetMaximum().Should().Be(99);
        }

        [Fact]
        public void InclusiveBetween_UShort_Should_Emit_Bounds()
        {
            var schema = new SchemaBuilder<Model>().AddRule(x => x.UShortValue, rule => rule.InclusiveBetween((ushort)3, (ushort)300));
            schema.GetMinimum().Should().Be(3);
            schema.GetMaximum().Should().Be(300);
        }

        [Fact]
        public void InclusiveBetween_UInt_Should_Emit_Bounds()
        {
            var schema = new SchemaBuilder<Model>().AddRule(x => x.UIntValue, rule => rule.InclusiveBetween(4u, 400u));
            schema.GetMinimum().Should().Be(4);
            schema.GetMaximum().Should().Be(400);
        }

        [Fact]
        public void InclusiveBetween_ULong_Should_Emit_Bounds()
        {
            var schema = new SchemaBuilder<Model>().AddRule(x => x.ULongValue, rule => rule.InclusiveBetween(5ul, 5000ul));
            schema.GetMinimum().Should().Be(5);
            schema.GetMaximum().Should().Be(5000);
        }

        [Fact]
        public void ExclusiveBetween_Short_Should_Emit_Exclusive_Bounds()
        {
            var schema = new SchemaBuilder<Model>().AddRule(x => x.ShortValue, rule => rule.ExclusiveBetween((short)1, (short)99));
            schema.GetMinimum().Should().Be(1);
            schema.GetMaximum().Should().Be(99);
            schema.GetExclusiveMinimum().Should().BeTrue();
            schema.GetExclusiveMaximum().Should().BeTrue();
        }

        [Fact]
        public void GreaterThanOrEqualTo_Short_Should_Emit_Minimum()
        {
            var schema = new SchemaBuilder<Model>().AddRule(x => x.ShortValue, rule => rule.GreaterThanOrEqualTo((short)1));
            schema.GetMinimum().Should().Be(1);
        }

        [Fact]
        public void LessThanOrEqualTo_Byte_Should_Emit_Maximum()
        {
            var schema = new SchemaBuilder<Model>().AddRule(x => x.ByteValue, rule => rule.LessThanOrEqualTo((byte)200));
            schema.GetMaximum().Should().Be(200);
        }

        [Fact]
        public void GreaterThan_UInt_Should_Emit_Exclusive_Minimum()
        {
            var schema = new SchemaBuilder<Model>().AddRule(x => x.UIntValue, rule => rule.GreaterThan(4u));
            schema.GetMinimum().Should().Be(4);
            schema.GetExclusiveMinimum().Should().BeTrue();
        }
    }
}
