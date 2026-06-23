using System;
using Xunit;
using SilverAssertions;
using SilverAssertions.Collections;
using SilverAssertions.Numeric;
using SilverAssertions.Primitives;
using SilverAssertions.Specialized;
using CodeBrix.Python;

namespace CodeBrix.Python.Tests; //was previously: Python.EmbeddingTest;

public class RunStringTest
{
    [Fact]
    public void TestRunSimpleString()
    {
        int aa = PythonEngine.RunSimpleString("import sys");
        Assert.Equal(0, aa);

        int bb = PythonEngine.RunSimpleString("import 1234");
        Assert.Equal(-1, bb);
    }

    [Fact]
    public void TestEval()
    {
        dynamic sys = Py.Import("sys");
        sys.attr1 = 100;
        var locals = new PyDict();
        locals.SetItem("sys", sys);
        locals.SetItem("a", new PyInt(10));

        object b = PythonEngine.Eval("sys.attr1 + a + 1", null, locals)
            .AsManagedObject(typeof(int));
        Assert.Equal(111, b);
    }

    [Fact]
    public void TestExec()
    {
        dynamic sys = Py.Import("sys");
        sys.attr1 = 100;
        var locals = new PyDict();
        locals.SetItem("sys", sys);
        locals.SetItem("a", new PyInt(10));

        PythonEngine.Exec("c = sys.attr1 + a + 1", null, locals);
        object c = locals.GetItem("c").AsManagedObject(typeof(int));
        Assert.Equal(111, c);
    }

    [Fact]
    public void TestExec2()
    {
        string code = @"
class Test1():
   pass

class Test2():
   def __init__(self):
       Test1()

Test2()";
        PythonEngine.Exec(code);
    }
}
