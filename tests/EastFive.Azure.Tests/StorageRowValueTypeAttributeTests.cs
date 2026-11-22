using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;

using Microsoft.Azure.Cosmos.Table;
using Xunit;

using EastFive.Azure.Persistence.StorageTables;
using EastFive.Persistence;
using EastFive.Persistence.Azure.StorageTables;
using EastFive.Serialization;

namespace EastFive.Azure.Tests;

/// <summary>
/// A column's value type can carry a type-level attribute that stores it: one that binds a single
/// column (<see cref="IBindEntityProperty"/>) or one that spreads it over several
/// (<see cref="ISerialize{TToFrom}"/> of the row's columns). Whether a type carries one is a fact
/// about the type, so it is looked up once per type and not again for every column of every row.
/// </summary>
public class StorageRowValueTypeAttributeTests
{
    /// <summary>
    /// Stores a <see cref="Code"/> in one column, marked so the test can see it went through here.
    /// Reflection builds a type's attributes each time they are read, so the number of these built
    /// is the number of lookups that reached <see cref="Code"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct)]
    private sealed class CountedBindingAttribute : Attribute, IBindEntityProperty, ICastEntityProperty
    {
        public const string Marker = "code:";

        private static int constructed;

        public static int Constructed => Volatile.Read(ref constructed);

        public CountedBindingAttribute() => Interlocked.Increment(ref constructed);

        public TResult BindEntityProperty<TResult>(EntityProperty value, Type type,
            Func<object, TResult> onBound,
            Func<TResult> onFailedToBind)
        {
            if (value.PropertyType != EdmType.String || !value.StringValue.StartsWith(Marker, StringComparison.Ordinal))
                return onFailedToBind();
            return onBound(new Code { text = value.StringValue.Substring(Marker.Length) });
        }

        public TResult CastEntityProperty<TResult>(object value, Type valueType,
            Func<EntityProperty, TResult> onValue,
            Func<TResult> onNoCast)
            => onValue(new EntityProperty(Marker + ((Code)value).text));
    }

    /// <summary>Stores a <see cref="Span"/> in two columns. Counted the same way.</summary>
    [AttributeUsage(AttributeTargets.Struct)]
    private sealed class CountedSerializerAttribute : Attribute, ISerialize<IDictionary<string, EntityProperty>>
    {
        private static int constructed;

        public static int Constructed => Volatile.Read(ref constructed);

        public CountedSerializerAttribute() => Interlocked.Increment(ref constructed);

        public TResult Cast<TResult>(object value, Type valueType, string path, MemberInfo member,
            Func<IDictionary<string, EntityProperty>, TResult> onValue,
            Func<TResult> onNoCast)
        {
            var span = (Span)value;
            return onValue(new Dictionary<string, EntityProperty>
            {
                { $"{path}_from", new EntityProperty(span.from) },
                { $"{path}_to", new EntityProperty(span.to) },
            });
        }

        public TResult Bind<TResult>(IDictionary<string, EntityProperty> value, Type type, string path, MemberInfo member,
            Func<object, TResult> onBound,
            Func<TResult> onFailedToBind)
        {
            if (!value.TryGetValue($"{path}_from", out var from) || !value.TryGetValue($"{path}_to", out var to))
                return onFailedToBind();
            return onBound(new Span { from = from.Int32Value.Value, to = to.Int32Value.Value });
        }
    }

    [CountedBinding]
    public struct Code
    {
        public string text;
    }

    [CountedSerializer]
    public struct Span
    {
        public int from;
        public int to;
    }

    public struct ValueTypeRow
    {
        [RowKey]
        [RowKeyPrefix(Characters = 2)]
        public Guid id;

        [Storage] public Code code;
        [Storage] public Span span;
        [Storage] public string plain;
    }

    private static readonly DateTimeOffset stamp = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

    private static ValueTypeRow Written() => new()
    {
        id = Guid.NewGuid(),
        code = new Code { text = "alpha" },
        span = new Span { from = 3, to = 7 },
        plain = "plain",
    };

    private static ValueTypeRow Read(StorageTableAttribute table, ValueTypeRow written, IDictionary<string, EntityProperty> columns)
        => table.CreateEntityInstance<ValueTypeRow>(written.id.ToString("N"), "ab", columns, "etag", stamp);

    [Fact]
    public void ReadingMoreRows_DoesNotLookUpAColumnsBindingAttributeAgain()
    {
        var table = new StorageTableAttribute();
        var written = Written();
        var columns = table.GetEntity(written).WriteEntity(null);
        Read(table, written, columns);
        var lookups = CountedBindingAttribute.Constructed;

        for (var more = 0; more < 100; more++)
            Read(table, written, columns);

        Assert.Equal(lookups, CountedBindingAttribute.Constructed);
    }

    [Fact]
    public void ReadingMoreRows_DoesNotLookUpAColumnsSerializerAttributeAgain()
    {
        var table = new StorageTableAttribute();
        var written = Written();
        var columns = table.GetEntity(written).WriteEntity(null);
        Read(table, written, columns);
        var lookups = CountedSerializerAttribute.Constructed;

        for (var more = 0; more < 100; more++)
            Read(table, written, columns);

        Assert.Equal(lookups, CountedSerializerAttribute.Constructed);
    }

    [Fact]
    public void EachColumn_IsStillStoredThroughItsValueTypesAttribute_OrWithoutOne()
    {
        var table = new StorageTableAttribute();
        var written = Written();
        var columns = table.GetEntity(written).WriteEntity(null);

        for (var row = 0; row < 3; row++)
        {
            var read = Read(table, written, columns);

            Assert.Equal(CountedBindingAttribute.Marker + "alpha", columns["code"].StringValue);
            Assert.Equal((3, 7), (columns["span_from"].Int32Value, columns["span_to"].Int32Value));
            Assert.Equal(written.code, read.code);
            Assert.Equal(written.span, read.span);
            Assert.Equal("plain", read.plain);
        }
    }
}
