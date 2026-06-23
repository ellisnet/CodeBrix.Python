using System;
using Xunit;
using SilverAssertions;
using SilverAssertions.Collections;
using SilverAssertions.Numeric;
using SilverAssertions.Primitives;
using SilverAssertions.Specialized;
using CodeBrix.Python;

namespace CodeBrix.Python.Tests; //was previously: Python.EmbeddingTest;

/// <remarks>
/// PyFloat implementation isn't complete, thus tests aren't complete.
/// </remarks>
public class TestPyFloat
{
    [Fact]
    public void FloatCtor()
    {
        const float a = 4.5F;
        var i = new PyFloat(a);
        Assert.True(PyFloat.IsFloatType(i));
        // Assert.Assert.Equal(i, a.ToInt32());
    }

    [Fact]
    public void PyObjectCtorGood()
    {
        var i = new PyFloat(5);
        var a = new PyFloat(i);
        Assert.True(PyFloat.IsFloatType(a));
        // Assert.Assert.Equal(i, a.ToInt32());
    }

    [Fact]
    public void PyObjectCtorBad()
    {
        var i = new PyString("Foo");
        PyFloat a = null;

        var ex = Assert.Throws<ArgumentException>(() => a = new PyFloat(i));

        Assert.StartsWith("object is not a float", ex.Message);
        Assert.Null(a);
    }

    [Fact]
    public void DoubleCtor()
    {
        const double a = 4.5;
        var i = new PyFloat(a);
        Assert.True(PyFloat.IsFloatType(i));
        // Assert.Assert.Equal(i, a.ToInt32());
    }

    [Fact]
    public void StringIntCtor()
    {
        const string a = "5";
        var i = new PyFloat(a);
        Assert.True(PyFloat.IsFloatType(i));
        // Assert.Assert.Equal(i, a.ToInt32());
    }

    [Fact]
    public void StringDoubleCtor()
    {
        const string a = "4.5";
        var i = new PyFloat(a);
        Assert.True(PyFloat.IsFloatType(i));
        // Assert.Assert.Equal(i, a.ToInt32());
    }

    [Fact]
    public void StringBadCtor()
    {
        const string i = "Foo";
        PyFloat a = null;

        var ex = Assert.Throws<PythonException>(() => a = new PyFloat(i));

        Assert.StartsWith("could not convert string to float", ex.Message);
        Assert.Null(a);
    }

    [Fact]
    public void IsFloatTrue()
    {
        const double a = 4.5;
        var i = new PyFloat(a);
        Assert.True(PyFloat.IsFloatType(i));
    }

    [Fact]
    public void IsFloatFalse()
    {
        var i = new PyString("Foo");
        Assert.False(PyFloat.IsFloatType(i));
    }

    [Fact]
    public void AsFloatGood()
    {
        const double a = 4.5;
        var i = new PyFloat(a);
        PyFloat s = PyFloat.AsFloat(i);

        Assert.True(PyFloat.IsFloatType(s));
        // Assert.Assert.Equal(i, a.ToInt32());
    }

    [Fact]
    public void AsFloatBad()
    {
        var s = new PyString("Foo");
        PyFloat a = null;

        var ex = Assert.Throws<PythonException>(() => a = PyFloat.AsFloat(s));
        Assert.StartsWith("could not convert string to float", ex.Message);
        Assert.Null(a);
    }

    [Fact]
    public void CompareTo()
    {
        var v = new PyFloat(42);

        Assert.Equal(0, v.CompareTo(42f));
        Assert.Equal(0, v.CompareTo(42d));

        Assert.Equal(1, v.CompareTo(41f));
        Assert.Equal(1, v.CompareTo(41d));

        Assert.Equal(-1, v.CompareTo(43f));
        Assert.Equal(-1, v.CompareTo(43d));
    }

    [Fact]
    public void EqualsTest()
    {
        var v = new PyFloat(42);

        Assert.True(v.Equals(42f));
        Assert.True(v.Equals(42d));

        Assert.False(v.Equals(41f));
        Assert.False(v.Equals(41d));
    }
}
