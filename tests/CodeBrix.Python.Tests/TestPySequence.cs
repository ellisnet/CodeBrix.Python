using System;
using Xunit;
using SilverAssertions;
using SilverAssertions.Collections;
using SilverAssertions.Numeric;
using SilverAssertions.Primitives;
using SilverAssertions.Specialized;
using CodeBrix.Python;

namespace CodeBrix.Python.Tests; //was previously: Python.EmbeddingTest;

public class TestPySequence
{
    [Fact]
    public void TestIsSequenceTrue()
    {
        var t = new PyString("FooBar");
        Assert.True(PySequence.IsSequenceType(t));
    }

    [Fact]
    public void TestIsSequenceFalse()
    {
        var t = new PyInt(5);
        Assert.False(PySequence.IsSequenceType(t));
    }

    [Fact]
    public void TestGetSlice()
    {
        var t = new PyString("FooBar");

        PyObject s = t.GetSlice(0, 3);
        Assert.Equal("Foo", s.ToString());

        PyObject s2 = t.GetSlice(3, 6);
        Assert.Equal("Bar", s2.ToString());

        PyObject s3 = t.GetSlice(0, 6);
        Assert.Equal("FooBar", s3.ToString());

        PyObject s4 = t.GetSlice(0, 12);
        Assert.Equal("FooBar", s4.ToString());
    }

    [Fact]
    public void TestConcat()
    {
        var t1 = new PyString("Foo");
        var t2 = new PyString("Bar");

        PyObject actual = t1.Concat(t2);

        Assert.Equal("FooBar", actual.ToString());
    }

    [Fact]
    public void TestRepeat()
    {
        var t1 = new PyString("Foo");

        PyObject actual = t1.Repeat(3);
        Assert.Equal("FooFooFoo", actual.ToString());

        actual = t1.Repeat(-3);
        Assert.Equal("", actual.ToString());
    }

    [Fact]
    public void TestContains()
    {
        var t1 = new PyString("FooBar");

        Assert.Contains(new PyString("a"), t1);
        Assert.DoesNotContain(new PyString("z"), t1);
    }

    [Fact]
    public void TestIndex()
    {
        var t1 = new PyString("FooBar");

        Assert.Equal(4, t1.Index32(new PyString("a")));
        Assert.Equal(5L, t1.Index64(new PyString("r")));
        Assert.Equal(-(nint)1, t1.Index(new PyString("z")));
    }
}
