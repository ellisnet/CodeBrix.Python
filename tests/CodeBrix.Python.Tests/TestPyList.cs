using System;
using System.Collections.Generic;
using Xunit;
using SilverAssertions;
using SilverAssertions.Collections;
using SilverAssertions.Numeric;
using SilverAssertions.Primitives;
using SilverAssertions.Specialized;
using CodeBrix.Python;

namespace CodeBrix.Python.Tests; //was previously: Python.EmbeddingTest;

public class TestPyList
{
    [Fact]
    public void TestStringIsListType()
    {
        var s = new PyString("foo");
        Assert.False(PyList.IsListType(s));
    }

    [Fact]
    public void TestListIsListType()
    {
        var s = new PyList();
        Assert.True(PyList.IsListType(s));
    }

    [Fact]
    public void TestStringAsListType()
    {
        var i = new PyInt(5);
        PyList t = null;

        var ex = Assert.Throws<PythonException>(() => t = PyList.AsList(i));

        Assert.Equal("'int' object is not iterable", ex.Message);
        Assert.Null(t);
    }

    [Fact]
    public void TestListAsListType()
    {
        var l = new PyList();
        PyList t = PyList.AsList(l);

        Assert.NotNull(t);
        Assert.IsAssignableFrom<PyList>(t);
    }

    [Fact]
    public void TestEmptyCtor()
    {
        var s = new PyList();

        Assert.IsAssignableFrom<PyList>(s);
        Assert.Equal(0, s.Length());
    }

    [Fact]
    public void TestPyObjectArrayCtor()
    {
        var ai = new PyObject[] {new PyInt(3), new PyInt(2), new PyInt(1) };
        var s = new PyList(ai);

        Assert.IsAssignableFrom<PyList>(s);
        Assert.Equal(3, s.Length());
        Assert.Equal("3", s[0].ToString());
        Assert.Equal("2", s[1].ToString());
        Assert.Equal("1", s[2].ToString());
    }

    [Fact]
    public void TestPyObjectCtor()
    {
        var a = new PyList();
        var s = new PyList(a);

        Assert.IsAssignableFrom<PyList>(s);
        Assert.Equal(0, s.Length());
    }

    [Fact]
    public void TestBadPyObjectCtor()
    {
        var i = new PyInt(5);
        PyList t = null;

        var ex = Assert.Throws<ArgumentException>(() => t = new PyList(i));

        Assert.Equal("object is not a list", ex.Message);
        Assert.Null(t);
    }

    [Fact]
    public void TestAppend()
    {
        var ai = new PyObject[] { new PyInt(3), new PyInt(2), new PyInt(1) };
        var s = new PyList(ai);
        s.Append(new PyInt(4));

        Assert.Equal(4, s.Length());
        Assert.Equal("4", s[3].ToString());
    }

    [Fact]
    public void TestInsert()
    {
        var ai = new PyObject[] { new PyInt(3), new PyInt(2), new PyInt(1) };
        var s = new PyList(ai);
        s.Insert(0, new PyInt(4));

        Assert.Equal(4, s.Length());
        Assert.Equal("4", s[0].ToString());
    }

    [Fact]
    public void TestReverse()
    {
        var ai = new PyObject[] { new PyInt(3), new PyInt(1), new PyInt(2) };
        var s = new PyList(ai);

        s.Reverse();

        Assert.Equal(3, s.Length());
        Assert.Equal("2", s[0].ToString());
        Assert.Equal("1", s[1].ToString());
        Assert.Equal("3", s[2].ToString());
    }

    [Fact]
    public void TestSort()
    {
        var ai = new PyObject[] { new PyInt(3), new PyInt(1), new PyInt(2) };
        var s = new PyList(ai);

        s.Sort();

        Assert.Equal(3, s.Length());
        Assert.Equal("1", s[0].ToString());
        Assert.Equal("2", s[1].ToString());
        Assert.Equal("3", s[2].ToString());
    }

    [Fact]
    public void TestOnPyList()
    {
        var list = new PyList();

        list.Append(new PyString("foo"));
        list.Append(new PyString("bar"));
        list.Append(new PyString("baz"));
        var result = new List<string>();
        foreach (PyObject item in list)
        {
            result.Add(item.ToString());
        }

        Assert.Equal(3, result.Count);
        Assert.Equal("foo", result[0]);
        Assert.Equal("bar", result[1]);
        Assert.Equal("baz", result[2]);
    }
}
