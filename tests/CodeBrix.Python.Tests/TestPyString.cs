using System;
using Xunit;
using SilverAssertions;
using SilverAssertions.Collections;
using SilverAssertions.Numeric;
using SilverAssertions.Primitives;
using SilverAssertions.Specialized;
using CodeBrix.Python;

namespace CodeBrix.Python.Tests; //was previously: Python.EmbeddingTest;

public class TestPyString
{
    [Fact]
    public void TestStringCtor()
    {
        const string expected = "foo";
        var actual = new PyString(expected);
        Assert.Equal(expected, actual.ToString());
    }

    [Fact]
    public void TestEmptyStringCtor()
    {
        const string expected = "";
        var actual = new PyString(expected);
        Assert.Equal(expected, actual.ToString());
    }

    [Fact]
    public void TestPyObjectCtor()
    {
        const string expected = "Foo";

        var t = new PyString(expected);
        var actual = new PyString(t);

        Assert.Equal(expected, actual.ToString());
    }

    [Fact]
    public void TestBadPyObjectCtor()
    {
        var t = new PyInt(5);
        PyString actual = null;

        var ex = Assert.Throws<ArgumentException>(() => actual = new PyString(t));

        Assert.StartsWith("object is not a string", ex.Message);
        Assert.Null(actual);
    }

    [Fact]
    public void TestCtorBorrowed()
    {
        const string expected = "foo";

        var t = new PyString(expected);
        var actual = new PyString(t.Reference);

        Assert.Equal(expected, actual.ToString());
    }

    [Fact]
    public void IsStringTrue()
    {
        var t = new PyString("foo");

        Assert.True(PyString.IsStringType(t));
    }

    [Fact]
    public void IsStringFalse()
    {
        var t = new PyInt(5);

        Assert.False(PyString.IsStringType(t));
    }

    [Fact]
    public void TestUnicode()
    {
        const string expected = "foo\u00e9";
        PyObject actual = new PyString(expected);
        Assert.Equal(expected, actual.ToString());
    }

    [Fact]
    public void TestUnicodeSurrogateToString()
    {
        var expected = "foo\ud83d\udc3c";
        var actual = PythonEngine.Eval("'foo\ud83d\udc3c'");
        Assert.Equal(4, actual.Length());
        Assert.Equal(expected, actual.ToString());
    }

    [Fact]
    public void TestUnicodeSurrogate()
    {
        const string expected = "foo\ud83d\udc3c"; // "foo🐼"
        PyObject actual = new PyString(expected);
        // python treats "foo🐼" as 4 characters, dotnet as 5
        Assert.Equal(4, actual.Length());
        Assert.Equal(expected, actual.ToString());
    }

    [Fact]
    public void CompareTo()
    {
        var a = new PyString("foo");

        Assert.Equal(0, a.CompareTo("foo"));
        Assert.Equal("foo".CompareTo("bar"), a.CompareTo("bar"));
        Assert.Equal("foo".CompareTo("foz"), a.CompareTo("foz"));
    }

    [Fact]
    public void EqualsTest()
    {
        var a = new PyString("foo");

        Assert.True(a.Equals("foo"));
        Assert.False(a.Equals("bar"));
    }
}
