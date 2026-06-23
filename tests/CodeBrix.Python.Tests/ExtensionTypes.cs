using System;
using Xunit;
using SilverAssertions;
using SilverAssertions.Collections;
using SilverAssertions.Numeric;
using SilverAssertions.Primitives;
using SilverAssertions.Specialized;
using CodeBrix.Python;

namespace CodeBrix.Python.Tests; //was previously: Python.EmbeddingTest;

public class ExtensionTypes
{
    [Fact]
    public void WeakrefIsNone_AfterBoundMethodIsGone()
    {
        using var makeref = Py.Import("weakref").GetAttr("ref");
        var boundMethod = new UriBuilder().ToPython().GetAttr(nameof(UriBuilder.GetHashCode));
        var weakref = makeref.Invoke(boundMethod);
        boundMethod.Dispose();
        Assert.True(weakref.Invoke().IsNone());
    }
}
