using System;
using Xunit;
using SilverAssertions;
using SilverAssertions.Collections;
using SilverAssertions.Numeric;
using SilverAssertions.Primitives;
using SilverAssertions.Specialized;
using CodeBrix.Python;

namespace CodeBrix.Python.Tests; //was previously: Python.EmbeddingTest;

public class TestPyNumber
{
    [Fact]
    public void IsNumberTypeTrue()
    {
        var i = new PyInt(1);
        Assert.True(PyNumber.IsNumberType(i));
    }

    [Fact]
    public void IsNumberTypeFalse()
    {
        var s = new PyString("Foo");
        Assert.False(PyNumber.IsNumberType(s));
    }
}
