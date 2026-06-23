using System;
using Xunit;
using SilverAssertions;
using SilverAssertions.Collections;
using SilverAssertions.Numeric;
using SilverAssertions.Primitives;
using SilverAssertions.Specialized;
using CodeBrix.Python;

namespace CodeBrix.Python.Tests; //was previously: Python.EmbeddingTest;

public class TestPyTuple
{
    /// <summary>
    /// Test IsTupleType without having to Initialize a tuple.
    /// PyTuple constructor use IsTupleType. This decouples the tests.
    /// </summary>
    [Fact]
    public void TestStringIsTupleType()
    {
        var s = new PyString("foo");
        Assert.False(PyTuple.IsTupleType(s));
    }

    /// <summary>
    /// Test IsTupleType with Tuple.
    /// </summary>
    [Fact]
    public void TestPyTupleIsTupleType()
    {
        var t = new PyTuple();
        Assert.True(PyTuple.IsTupleType(t));
    }

    [Fact]
    public void TestPyTupleEmpty()
    {
        var t = new PyTuple();
        Assert.Equal(0, t.Length());
    }

    [Fact]
    public void TestPyTupleBadCtor()
    {
        var i = new PyInt(5);
        PyTuple t = null;

        var ex = Assert.Throws<ArgumentException>(() => t = new PyTuple(i));

        Assert.Equal("object is not a tuple", ex.Message);
        Assert.Null(t);
    }

    [Fact]
    public void TestPyTupleCtorEmptyArray()
    {
        var a = new PyObject[] { };
        var t = new PyTuple(a);

        Assert.Equal(0, t.Length());
    }

    [Fact]
    public void TestPyTupleCtorArrayPyIntEmpty()
    {
        var a = new PyInt[] { };
        var t = new PyTuple(a);

        Assert.Equal(0, t.Length());
    }

    [Fact]
    public void TestPyTupleCtorArray()
    {
        var a = new PyObject[] { new PyInt(1), new PyString("Foo") };
        var t = new PyTuple(a);

        Assert.Equal(2, t.Length());
    }

    /// <summary>
    /// Test PyTuple.Concat(...) doesn't let invalid appends happen
    /// and throws and exception.
    /// </summary>
    /// <remarks>
    /// Test has second purpose. Currently it generated an Exception
    /// that the GC failed to remove often and caused AppDomain unload
    /// errors at the end of tests. See GH#397 for more info.
    /// </remarks>
    [Fact]
    public void TestPyTupleInvalidAppend()
    {
        PyObject s = new PyString("foo");
        var t = new PyTuple();

        var ex = Assert.Throws<PythonException>(() => t.Concat(s));

        Assert.StartsWith("can only concatenate tuple", ex.Message);
        Assert.Equal(0, t.Length());
        Assert.Empty(t);
    }

    [Fact]
    public void TestPyTupleValidAppend()
    {
        var t0 = new PyTuple();
        var t = new PyTuple();
        t.Concat(t0);

        Assert.NotNull(t);
        Assert.IsAssignableFrom<PyTuple>(t);
    }

    [Fact]
    public void TestPyTupleStringConvert()
    {
        PyObject s = new PyString("foo");
        PyTuple t = PyTuple.AsTuple(s);

        Assert.NotNull(t);
        Assert.IsAssignableFrom<PyTuple>(t);
        Assert.Equal("f", t[0].ToString());
        Assert.Equal("o", t[1].ToString());
        Assert.Equal("o", t[2].ToString());
    }

    [Fact]
    public void TestPyTupleValidConvert()
    {
        var l = new PyList();
        PyTuple t = PyTuple.AsTuple(l);

        Assert.NotNull(t);
        Assert.IsAssignableFrom<PyTuple>(t);
    }

    [Fact]
    public void TestNewPyTupleFromPyTuple()
    {
        var t0 = new PyTuple();
        var t = new PyTuple(t0);

        Assert.NotNull(t);
        Assert.IsAssignableFrom<PyTuple>(t);
    }

    /// <remarks>
    /// TODO: Should this throw ArgumentError instead?
    /// </remarks>
    [Fact]
    public void TestInvalidAsTuple()
    {
        var i = new PyInt(5);
        PyTuple t = null;

        var ex = Assert.Throws<PythonException>(() => t = PyTuple.AsTuple(i));

        Assert.Equal("'int' object is not iterable", ex.Message);
        Assert.Null(t);
    }
}
