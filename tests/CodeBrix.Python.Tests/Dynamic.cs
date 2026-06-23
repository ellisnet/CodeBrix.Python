using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using SilverAssertions;
using SilverAssertions.Collections;
using SilverAssertions.Numeric;
using SilverAssertions.Primitives;
using SilverAssertions.Specialized;
using CodeBrix.Python;

namespace CodeBrix.Python.Tests; //was previously: Python.EmbeddingTest;

public class DynamicTest
{
    /// <summary>
    /// Set the attribute of a PyObject with a .NET object.
    /// </summary>
    [Fact]
    public void AssignObject()
    {
        var stream = new StringBuilder();
        dynamic sys = Py.Import("sys");
        sys.testattr = stream;
        // Check whether there are the same object.
        dynamic _stream = sys.testattr.AsManagedObject(typeof(StringBuilder));
        Assert.Equal(stream, _stream);

        PythonEngine.RunSimpleString(
            "import sys\n" +
            "sys.testattr.Append('Hello!')\n");
        Assert.Equal("Hello!", stream.ToString());
    }

    /// <summary>
    /// Set the attribute of a PyObject to null.
    /// </summary>
    [Fact]
    public void AssignNone()
    {
        dynamic sys = Py.Import("sys");
        sys.testattr = new StringBuilder();
        Assert.NotNull(sys.testattr);

        sys.testattr = null;
        Assert.Null(sys.testattr);
    }

    /// <summary>
    /// Check whether we can get the attr of a python object when the
    /// value of attr is a PyObject.
    /// </summary>
    [Fact]
    public void AssignPyObject()
    {
        dynamic sys = Py.Import("sys");
        dynamic io = Py.Import("io");
        sys.testattr = io.StringIO();
        dynamic bb = sys.testattr; // Get the PyObject
        bb.write("Hello!");
        Assert.Equal("Hello!", bb.getvalue().ToString());
    }

    /// <summary>
    /// Pass the .NET object in Python side.
    /// </summary>
    [Fact]
    public void PassObjectInPython()
    {
        var stream = new StringBuilder();
        dynamic sys = Py.Import("sys");
        sys.testattr1 = stream;

        // Pass the .NET object in Python side
        PythonEngine.RunSimpleString(
            "import sys\n" +
            "sys.testattr2 = sys.testattr1\n"
        );

        // Compare in Python
        PythonEngine.RunSimpleString(
            "import sys\n" +
            "sys.testattr3 = sys.testattr1 is sys.testattr2\n"
        );
        Assert.Equal("True", sys.testattr3.ToString());

        // Compare in .NET
        Assert.True(sys.testattr1.Equals(sys.testattr2));
    }

    /// <summary>
    /// Pass the PyObject in .NET side
    /// </summary>
    [Fact]
    public void PassPyObjectInNet()
    {
        var stream = new StringBuilder();
        dynamic sys = Py.Import("sys");
        sys.testattr1 = stream;
        sys.testattr2 = sys.testattr1;

        // Compare in Python
        PythonEngine.RunSimpleString(
            "import sys\n" +
            "sys.testattr3 = sys.testattr1 is sys.testattr2\n"
        );

        Assert.Equal("True", sys.testattr3.ToString());

        // Compare in .NET
        Assert.True(sys.testattr1.Equals(sys.testattr2));
    }

    // regression test for https://github.com/pythonnet/pythonnet/issues/1848
    [Fact]
    public void EnumEquality()
    {
        using var scope = Py.CreateScope();
        scope.Exec(@"
import enum

class MyEnum(enum.IntEnum):
    OK = 1
    ERROR = 2

def get_status():
    return MyEnum.OK 
"
);

        dynamic MyEnum = scope.Get("MyEnum");
        dynamic status = scope.Get("get_status").Invoke();
        Assert.True(status == MyEnum.OK);
    }

    // regression test for https://github.com/pythonnet/pythonnet/issues/1680
    [Fact]
    public void ForEach()
    {
        dynamic pyList = PythonEngine.Eval("[1,2,3]");
        var list = new List<int>();
        foreach (int item in pyList)
            list.Add(item);
    }
}
