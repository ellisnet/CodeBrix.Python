using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using Xunit;
using CodeBrix.Python;
using CodeBrix.Python.Serialization;

namespace CodeBrix.Python.Tests.StateSerialization; //was previously: Python.EmbeddingTest.StateSerialization;

public class MethodSerialization
{
    // -----------------------------------------------------------------------------------------
    // Without the new JsonFormatter, RuntimeData falls back to NoopFormatter on .NET 10 (because
    // BinaryFormatter is gone). NoopFormatter cannot round-trip — Serialize writes nothing and
    // Deserialize throws. These two tests assert that EXPECTED failure, so they pass by confirming
    // the known limitation rather than by performing a real round-trip.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void GenericRoundtrip_WithoutJsonFormatter_FailsAsExpected()
    {
        var method = typeof(MethodTestHost).GetMethod(nameof(MethodTestHost.Generic));
        var maybeMethod = new MaybeMethodBase<MethodBase>(method);
        AssertNoopFormatterCannotRoundtrip(maybeMethod);
    }

    [Fact]
    public void ConstructorRoundtrip_WithoutJsonFormatter_FailsAsExpected()
    {
        var ctor = typeof(MethodTestHost).GetConstructor(new[] { typeof(int) });
        var maybeConstructor = new MaybeMethodBase<MethodBase>(ctor);
        AssertNoopFormatterCannotRoundtrip(maybeConstructor);
    }

    // -----------------------------------------------------------------------------------------
    // With the new JsonFormatter these behave like the original round-trip tests did under
    // BinaryFormatter — they actually serialize and reconstruct the method/constructor, and pass.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void GenericRoundtrip_WithJsonFormatter_Succeeds()
    {
        var method = typeof(MethodTestHost).GetMethod(nameof(MethodTestHost.Generic));
        var maybeMethod = new MaybeMethodBase<MethodBase>(method);
        var restored = JsonRoundtrip(maybeMethod);
        Assert.True(restored.Valid);
        Assert.Equal(method, restored.Value);
    }

    [Fact]
    public void ConstructorRoundtrip_WithJsonFormatter_Succeeds()
    {
        var ctor = typeof(MethodTestHost).GetConstructor(new[] { typeof(int) });
        var maybeConstructor = new MaybeMethodBase<MethodBase>(ctor);
        var restored = JsonRoundtrip(maybeConstructor);
        Assert.True(restored.Valid);
        Assert.Equal(ctor, restored.Value);
    }

    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// Confirms the platform fallback (NoopFormatter) is in effect and that it cannot round-trip:
    /// Serialize is a no-op and Deserialize throws. Passing this test means the expected failure
    /// happened exactly as predicted.
    /// </summary>
    static void AssertNoopFormatterCannotRoundtrip<T>(T item)
    {
        var formatter = RuntimeData.CreateFormatter();
        Assert.IsType<NoopFormatter>(formatter); // we really are on the no-op fallback

        using var buf = new MemoryStream();
        formatter.Serialize(buf, item!);         // no-op: writes nothing
        buf.Position = 0;
        Assert.Throws<NotImplementedException>(() => formatter.Deserialize(buf));
    }

    static T JsonRoundtrip<T>(T item)
    {
        var formatter = new JsonFormatter();
        using var buf = new MemoryStream();
        formatter.Serialize(buf, item!);
        buf.Position = 0;
        return (T)formatter.Deserialize(buf);
    }
}

public class MethodTestHost
{
    public MethodTestHost(int _) { }
    public void Generic<T>(T item, T[] array, ref T @ref) { }
}
