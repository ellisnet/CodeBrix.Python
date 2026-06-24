using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using CodeBrix.Python;
using CodeBrix.Python.Serialization;
using Xunit;

namespace CodeBrix.Python.Tests.Serialization;

/// <summary>
/// Thorough round-trip tests for <see cref="JsonFormatter"/> — the managed BinaryFormatter
/// replacement. These tests are an additive validation of the new formatter and do NOT touch any
/// existing functionality or the existing (still-failing) serialization tests.
/// </summary>
public class JsonFormatterTests
{
    private static T Roundtrip<T>(T value)
    {
        var formatter = new JsonFormatter();
        using var ms = new MemoryStream();
        formatter.Serialize(ms, value!);
        ms.Position = 0;
        return (T)formatter.Deserialize(ms);
    }

    private static object? RoundtripObject(object? value)
    {
        var formatter = new JsonFormatter();
        using var ms = new MemoryStream();
        formatter.Serialize(ms, value!);
        ms.Position = 0;
        return value is null ? null : formatter.Deserialize(ms);
    }

    // ---------------------------------------------------------------- primitives & simple types

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Bool_Roundtrips(bool v) => Assert.Equal(v, Roundtrip(v));

    [Theory]
    [InlineData(int.MinValue)]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(42)]
    [InlineData(int.MaxValue)]
    public void Int_Roundtrips(int v) => Assert.Equal(v, Roundtrip(v));

    [Fact]
    public void IntegerEdges_Roundtrip()
    {
        Assert.Equal(long.MinValue, Roundtrip(long.MinValue));
        Assert.Equal(long.MaxValue, Roundtrip(long.MaxValue));
        Assert.Equal(ulong.MaxValue, Roundtrip(ulong.MaxValue));
        Assert.Equal(byte.MaxValue, Roundtrip(byte.MaxValue));
        Assert.Equal((sbyte)-128, Roundtrip((sbyte)-128));
        Assert.Equal(short.MinValue, Roundtrip(short.MinValue));
        Assert.Equal(ushort.MaxValue, Roundtrip(ushort.MaxValue));
        Assert.Equal(uint.MaxValue, Roundtrip(uint.MaxValue));
    }

    [Fact]
    public void FloatingPointAndDecimal_Roundtrip()
    {
        Assert.Equal(3.14159265358979, Roundtrip(3.14159265358979));
        Assert.Equal(float.MaxValue, Roundtrip(float.MaxValue));
        Assert.Equal(double.Epsilon, Roundtrip(double.Epsilon));
        Assert.Equal(double.NegativeInfinity, Roundtrip(double.NegativeInfinity));
        Assert.True(double.IsNaN(Roundtrip(double.NaN)));
        Assert.Equal(79228162514264337593543950335m, Roundtrip(decimal.MaxValue));
        Assert.Equal(-0.0000000000001m, Roundtrip(-0.0000000000001m));
    }

    [Fact]
    public void TextAndIdentityTypes_Roundtrip()
    {
        Assert.Equal("hello \"world\"\n\té\U0001F600", Roundtrip("hello \"world\"\n\té\U0001F600"));
        Assert.Equal(string.Empty, Roundtrip(string.Empty));
        Assert.Equal('Z', Roundtrip('Z'));
        Assert.Equal('\0', Roundtrip('\0'));
        var guid = Guid.NewGuid();
        Assert.Equal(guid, Roundtrip(guid));
    }

    [Fact]
    public void DateAndTimeTypes_Roundtrip()
    {
        var utc = new DateTime(2026, 6, 23, 12, 34, 56, DateTimeKind.Utc).AddTicks(123);
        Assert.Equal(utc, Roundtrip(utc));
        Assert.Equal(DateTimeKind.Utc, Roundtrip(utc).Kind);

        var unspecified = new DateTime(1999, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
        Assert.Equal(unspecified, Roundtrip(unspecified));

        var dto = new DateTimeOffset(2026, 6, 23, 1, 2, 3, TimeSpan.FromHours(-7));
        Assert.Equal(dto, Roundtrip(dto));

        var span = new TimeSpan(5, 4, 3, 2, 1);
        Assert.Equal(span, Roundtrip(span));
    }

    // ---------------------------------------------------------------- enums

    private enum ByteEnum : byte { A = 1, B = 200 }

    [Flags]
    private enum FlagsEnum : long { None = 0, X = 1, Y = 2, Z = 4, All = X | Y | Z }

    [Fact]
    public void Enums_Roundtrip()
    {
        Assert.Equal(ByteEnum.B, Roundtrip(ByteEnum.B));
        Assert.Equal(FlagsEnum.X | FlagsEnum.Z, Roundtrip(FlagsEnum.X | FlagsEnum.Z));
        Assert.Equal((FlagsEnum)999, Roundtrip((FlagsEnum)999));
        // As an object (polymorphic) the concrete enum type must be preserved.
        object boxed = ByteEnum.A;
        Assert.Equal(ByteEnum.A, RoundtripObject(boxed));
    }

    // ---------------------------------------------------------------- null & arrays

    [Fact]
    public void Null_Roundtrips()
    {
        Assert.Null(RoundtripObject(null));
        var holder = Roundtrip(new FieldHost());           // all fields default/null
        Assert.Null(holder.Text);
        Assert.Null(holder.Numbers);
    }

    [Fact]
    public void Arrays_Roundtrip()
    {
        Assert.Equal(new[] { 1, 2, 3 }, Roundtrip(new[] { 1, 2, 3 }));
        Assert.Equal(Array.Empty<int>(), Roundtrip(Array.Empty<int>()));
        Assert.Equal(new[] { "a", null, "c" }, Roundtrip(new[] { "a", null, "c" }));

        var nested = new[] { new FieldHost { Text = "x", Number = 1 }, new FieldHost { Text = "y", Number = 2 } };
        var restored = Roundtrip(nested);
        Assert.Equal(2, restored.Length);
        Assert.Equal("x", restored[0].Text);
        Assert.Equal(2, restored[1].Number);
    }

    // ---------------------------------------------------------------- [Serializable] field-based types

    [Serializable]
    private class FieldHost
    {
        public string? Text;
        public int Number;
        public int[]? Numbers;
        private double _hidden = 2.5;
        [NonSerialized] public string? Transient;

        public double Hidden => _hidden;
        public void SetHidden(double v) => _hidden = v;
    }

    [Serializable]
    private class DerivedFieldHost : FieldHost
    {
        public bool Extra;
    }

    [Fact]
    public void FieldBasedType_RoundtripsAllFieldsIncludingPrivate()
    {
        var host = new FieldHost { Text = "abc", Number = 7, Numbers = new[] { 9, 8 }, Transient = "ephemeral" };
        host.SetHidden(99.5);

        var restored = Roundtrip(host);

        Assert.Equal("abc", restored.Text);
        Assert.Equal(7, restored.Number);
        Assert.Equal(new[] { 9, 8 }, restored.Numbers);
        Assert.Equal(99.5, restored.Hidden);          // private field round-trips
        Assert.Null(restored.Transient);              // [NonSerialized] is skipped
    }

    [Fact]
    public void DerivedType_SerializesBaseAndDerivedFields()
    {
        var host = new DerivedFieldHost { Text = "base", Number = 3, Extra = true };
        var restored = Roundtrip(host);
        Assert.Equal("base", restored.Text);
        Assert.Equal(3, restored.Number);
        Assert.True(restored.Extra);
    }

    [Fact]
    public void Polymorphism_ConcreteTypePreservedUnderBaseSlot()
    {
        FieldHost host = new DerivedFieldHost { Text = "poly", Extra = true };
        var restored = Roundtrip(host);
        var derived = Assert.IsType<DerivedFieldHost>(restored);
        Assert.True(derived.Extra);
        Assert.Equal("poly", derived.Text);
    }

    // ---------------------------------------------------------------- structs

    [Serializable]
    private struct PointStruct
    {
        public int X;
        public int Y;
        public string? Label;
    }

    [Fact]
    public void Struct_Roundtrips()
    {
        var p = new PointStruct { X = -3, Y = 4, Label = "origin-ish" };
        var restored = Roundtrip(p);
        Assert.Equal(-3, restored.X);
        Assert.Equal(4, restored.Y);
        Assert.Equal("origin-ish", restored.Label);
    }

    // ---------------------------------------------------------------- ISerializable types

    [Serializable]
    private sealed class CustomSerializable : ISerializable
    {
        public string Name { get; }
        public int[] Values { get; }

        public CustomSerializable(string name, int[] values)
        {
            Name = name;
            Values = values;
        }

        private CustomSerializable(SerializationInfo info, StreamingContext context)
        {
            Name = info.GetString("n")!;
            Values = (int[])info.GetValue("vals", typeof(int[]))!;
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("n", Name);
            info.AddValue("vals", Values, typeof(int[]));
        }
    }

    [Fact]
    public void ISerializableType_UsesGetObjectDataAndCtor()
    {
        var original = new CustomSerializable("widget", new[] { 5, 6, 7 });
        var restored = Roundtrip(original);
        Assert.Equal("widget", restored.Name);
        Assert.Equal(new[] { 5, 6, 7 }, restored.Values);
    }

    // ---------------------------------------------------------------- references & cycles

    [Serializable]
    private class Node
    {
        public string? Name;
        public Node? Next;
        public Node? Shared;
    }

    [Fact]
    public void SharedReference_IsPreservedAsSingleInstance()
    {
        var shared = new Node { Name = "shared" };
        var a = new Node { Name = "a", Shared = shared };
        var b = new Node { Name = "b", Shared = shared, Next = a };
        a.Shared = shared;

        var restored = Roundtrip(b);
        Assert.NotNull(restored.Shared);
        Assert.Same(restored.Shared, restored.Next!.Shared);   // same instance, not two copies
    }

    [Fact]
    public void Cycle_ThroughFieldBasedTypes_Roundtrips()
    {
        var a = new Node { Name = "a" };
        var b = new Node { Name = "b" };
        a.Next = b;
        b.Next = a;                                            // cycle a -> b -> a

        var restored = Roundtrip(a);
        Assert.Equal("a", restored.Name);
        Assert.Equal("b", restored.Next!.Name);
        Assert.Same(restored, restored.Next.Next);            // cycle preserved
    }

    // ---------------------------------------------------------------- callbacks

    [Serializable]
    private class CallbackHost : IDeserializationCallback
    {
        public int Value;
        [NonSerialized] public bool OnDeserializedCalled;
        [NonSerialized] public bool OnDeserializationCalled;

        [OnDeserialized]
        private void AfterDeserialized(StreamingContext context) => OnDeserializedCalled = true;

        public void OnDeserialization(object? sender) => OnDeserializationCalled = true;
    }

    [Fact]
    public void Callbacks_AreInvoked()
    {
        var restored = Roundtrip(new CallbackHost { Value = 11 });
        Assert.Equal(11, restored.Value);
        Assert.True(restored.OnDeserializedCalled, "[OnDeserialized] should have run");
        Assert.True(restored.OnDeserializationCalled, "IDeserializationCallback.OnDeserialization should have run");
    }

    // ---------------------------------------------------------------- unsupported input

    private class NotSerializableType
    {
        public int X;
    }

    [Fact]
    public void UnsupportedType_ThrowsSerializationException()
    {
        var formatter = new JsonFormatter();
        using var ms = new MemoryStream();
        Assert.Throws<SerializationException>(() => formatter.Serialize(ms, new NotSerializableType { X = 1 }));
    }

    // ---------------------------------------------------------------- parity with the real library type

    private class MethodTestHost
    {
        public MethodTestHost(int _) { }
        public void Generic<T>(T item, T[] array, ref T @ref) { }
    }

    [Fact]
    public void MaybeMethodBase_GenericMethod_RoundtripsLikeBinaryFormatter()
    {
        MethodBase? method = typeof(MethodTestHost).GetMethod(nameof(MethodTestHost.Generic));
        var maybe = new MaybeMethodBase<MethodBase>(method);

        var restored = Roundtrip(maybe);

        Assert.True(restored.Valid);
        Assert.Equal(method, restored.Value);
    }

    [Fact]
    public void MaybeMethodBase_Constructor_RoundtripsLikeBinaryFormatter()
    {
        MethodBase? ctor = typeof(MethodTestHost).GetConstructor(new[] { typeof(int) });
        var maybe = new MaybeMethodBase<MethodBase>(ctor);

        var restored = Roundtrip(maybe);

        Assert.True(restored.Valid);
        Assert.Equal(ctor, restored.Value);
    }

    [Fact]
    public void DeterministicOutput_SameGraphProducesSameBytes()
    {
        var graph = new FieldHost { Text = "stable", Number = 5, Numbers = new[] { 1, 2, 3 } };
        var formatter = new JsonFormatter();

        using var first = new MemoryStream();
        using var second = new MemoryStream();
        formatter.Serialize(first, graph);
        formatter.Serialize(second, graph);

        Assert.Equal(first.ToArray(), second.ToArray());
    }
}
