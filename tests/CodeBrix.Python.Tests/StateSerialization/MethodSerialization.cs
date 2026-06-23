using System.IO;
using System.Reflection;
using Xunit;
using SilverAssertions;
using SilverAssertions.Collections;
using SilverAssertions.Numeric;
using SilverAssertions.Primitives;
using SilverAssertions.Specialized;
using CodeBrix.Python;

namespace CodeBrix.Python.Tests.StateSerialization; //was previously: Python.EmbeddingTest.StateSerialization;

public class MethodSerialization
{
    [Fact]
    public void GenericRoundtrip()
    {
        var method = typeof(MethodTestHost).GetMethod(nameof(MethodTestHost.Generic));
        var maybeMethod = new MaybeMethodBase<MethodBase>(method);
        var restored = SerializationRoundtrip(maybeMethod);
        Assert.True(restored.Valid);
        Assert.Equal(method, restored.Value);
    }

    [Fact]
    public void ConstructorRoundtrip()
    {
        var ctor = typeof(MethodTestHost).GetConstructor(new[] { typeof(int) });
        var maybeConstructor = new MaybeMethodBase<MethodBase>(ctor);
        var restored = SerializationRoundtrip(maybeConstructor);
        Assert.True(restored.Valid);
        Assert.Equal(ctor, restored.Value);
    }

    static T SerializationRoundtrip<T>(T item)
    {
        using var buf = new MemoryStream();
        var formatter = RuntimeData.CreateFormatter();
        if (typeof(NoopFormatter).IsAssignableFrom(formatter.GetType()))
        {
            Assert.Skip("NoopFormatter in use, cannot perform serialization test.");
        }
        formatter.Serialize(buf, item);
        buf.Position = 0;
        return (T)formatter.Deserialize(buf);
    }
}

public class MethodTestHost
{
    public MethodTestHost(int _) { }
    public void Generic<T>(T item, T[] array, ref T @ref) { }
}
