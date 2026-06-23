using System;
using System.Threading;
using Xunit;
using SilverAssertions;
using SilverAssertions.Collections;
using SilverAssertions.Numeric;
using SilverAssertions.Primitives;
using SilverAssertions.Specialized;
using CodeBrix.Python;

namespace CodeBrix.Python.Tests; //was previously: Python.EmbeddingTest;

public class Modules : IDisposable
{
    private PyModule ps;
    public Modules()
    {
        using (Py.GIL())
        {
            ps = Py.CreateScope("test");
        }
    }
    public void Dispose()
    {
        using (Py.GIL())
        {
            ps.Dispose();
            ps = null;
        }
    }

    /// <summary>
    /// Eval a Python expression and obtain its return value.
    /// </summary>
    [Fact]
    public void TestEval()
    {
        using (Py.GIL())
        {
            ps.Set("a", 1);
            var result = ps.Eval<int>("a + 2");
            Assert.Equal(3, result);
        }
    }

    /// <summary>
    /// Exec Python statements and obtain the variables created.
    /// </summary>
    [Fact]
    public void TestExec()
    {
        using (Py.GIL())
        {
            ps.Set("bb", 100); //declare a global variable
            ps.Set("cc", 10); //declare a local variable
            ps.Exec("aa = bb + cc + 3");
            var result = ps.Get<int>("aa");
            Assert.Equal(113, result);
        }
    }

    /// <summary>
    /// Compile an expression into an ast object;
    /// Execute the ast and obtain its return value.
    /// </summary>
    [Fact]
    public void TestCompileExpression()
    {
        using (Py.GIL())
        {
            ps.Set("bb", 100); //declare a global variable
            ps.Set("cc", 10); //declare a local variable
            PyObject script = PythonEngine.Compile("bb + cc + 3", "", RunFlagType.Eval);
            var result = ps.Execute<int>(script);
            Assert.Equal(113, result);
        }
    }

    /// <summary>
    /// Compile Python statements into an ast object;
    /// Execute the ast;
    /// Obtain the local variables created.
    /// </summary>
    [Fact]
    public void TestCompileStatements()
    {
        using (Py.GIL())
        {
            ps.Set("bb", 100); //declare a global variable
            ps.Set("cc", 10); //declare a local variable
            PyObject script = PythonEngine.Compile("aa = bb + cc + 3", "", RunFlagType.File);
            ps.Execute(script);
            var result = ps.Get<int>("aa");
            Assert.Equal(113, result);
        }
    }

    /// <summary>
    /// Create a function in the scope, then the function can read variables in the scope.
    /// It cannot write the variables unless it uses the 'global' keyword.
    /// </summary>
    [Fact]
    public void TestScopeFunction()
    {
        using (Py.GIL())
        {
            ps.Set("bb", 100);
            ps.Set("cc", 10);
            ps.Exec(
                "def func1():\n" +
                "    bb = cc + 10\n");
            dynamic func1 = ps.Get("func1");
            func1(); //call the function, it can be called any times
            var result = ps.Get<int>("bb");
            Assert.Equal(100, result);

            ps.Set("bb", 100);
            ps.Set("cc", 10);
            ps.Exec(
                "def func2():\n" +
                "    global bb\n" +
                "    bb = cc + 10\n");
            dynamic func2 = ps.Get("func2");
            func2();
            result = ps.Get<int>("bb");
            Assert.Equal(20, result);
        }
    }

    /// <summary>
    /// Create a class in the scope, the class can read variables in the scope.
    /// Its methods can write the variables with the help of 'global' keyword.
    /// </summary>
    [Fact]
    public void TestScopeClass()
    {
        using (Py.GIL())
        {
            dynamic _ps = ps;
            _ps.bb = 100;
            ps.Exec(
                "class Class1():\n" +
                "    def __init__(self, value):\n" +
                "        self.value = value\n" +
                "    def call(self, arg):\n" +
                "        return self.value + bb + arg\n" + //use scope variables
                "    def update(self, arg):\n" +
                "        global bb\n" +
                "        bb = self.value + arg\n"  //update scope variable
            );
            dynamic obj1 = _ps.Class1(20);
            var result = obj1.call(10).As<int>();
            Assert.Equal(130, result);

            obj1.update(10);
            result = ps.Get<int>("bb");
            Assert.Equal(30, result);
        }
    }

    /// <summary>
    /// Create a class in the scope, the class can read variables in the scope.
    /// Its methods can write the variables with the help of 'global' keyword.
    /// </summary>
    [Fact]
    public void TestCreateVirtualPackageStructure()
    {
        using (Py.GIL())
        {
            using var _p1 = PyModule.FromString("test", "");
            // Sub-module
            using var _p2 = PyModule.FromString("test.scope",
                "class Class1():\n" +
                "    def __init__(self, value):\n" +
                "        self.value = value\n" +
                "    def call(self, arg):\n" +
                "        return self.value + bb + arg\n" + // use scope variables
                "    def update(self, arg):\n" +
                "        global bb\n" +
                "        bb = self.value + arg\n",  // update scope variable
                "test"
            );

            dynamic ps2 = Py.Import("test.scope");
            ps2.bb = 100;

            dynamic obj1 = ps2.Class1(20);
            var result = obj1.call(10).As<int>();
            Assert.Equal(130, result);

            obj1.update(10);
            result = ps2.Get<int>("bb");
            Assert.Equal(30, result);
        }
    }

    /// <summary>
    /// Test setting the file attribute via a FromString parameter
    /// </summary>
    [Fact]
    public void TestCreateModuleWithFilename()
    {
        using var _gil = Py.GIL();

        using var mod = PyModule.FromString("mod", "");
        using var modWithoutName = PyModule.FromString("mod_without_name", "", " ");
        using var modNullName = PyModule.FromString("mod_null_name", "", null);

        using var modWithName = PyModule.FromString("mod_with_name", "", "some_filename");

        Assert.Equal("none", mod.Get<string>("__file__"));
        Assert.Equal("none", modWithoutName.Get<string>("__file__"));
        Assert.Equal("none", modNullName.Get<string>("__file__"));
        Assert.Equal("some_filename", modWithName.Get<string>("__file__"));
    }

    /// <summary>
    /// Import a python module into the session.
    /// Equivalent to the Python "import" statement.
    /// </summary>
    [Fact]
    public void TestImportModule()
    {
        using (Py.GIL())
        {
            dynamic sys = ps.Import("sys");
            Assert.True(ps.Contains("sys"));

            ps.Exec("sys.attr1 = 2");
            var value1 = ps.Eval<int>("sys.attr1");
            var value2 = sys.attr1.As<int>();
            Assert.Equal(2, value1);
            Assert.Equal(2, value2);

            //import as
            ps.Import("sys", "sys1");
            Assert.True(ps.Contains("sys1"));
        }
    }

    /// <summary>
    /// Create a scope and import variables from a scope,
    /// exec Python statements in the scope then discard it.
    /// </summary>
    [Fact]
    public void TestImportScope()
    {
        using (Py.GIL())
        {
            ps.Set("bb", 100);
            ps.Set("cc", 10);

            using (var scope = Py.CreateScope())
            {
                scope.Import(ps, "ps");
                scope.Exec("aa = ps.bb + ps.cc + 3");
                var result = scope.Get<int>("aa");
                Assert.Equal(113, result);
            }

            Assert.False(ps.Contains("aa"));
        }
    }

    /// <summary>
    /// Create a scope and import variables from a scope,
    /// exec Python statements in the scope then discard it.
    /// </summary>
    [Fact]
    public void TestImportAllFromScope()
    {
        using (Py.GIL())
        {
            ps.Set("bb", 100);
            ps.Set("cc", 10);

            using (var scope = ps.NewScope())
            {
                scope.Exec("aa = bb + cc + 3");
                var result = scope.Get<int>("aa");
                Assert.Equal(113, result);
            }

            Assert.False(ps.Contains("aa"));
        }
    }

    /// <summary>
    /// Create a scope and import variables from a scope,
    /// call the function imported.
    /// </summary>
    [Fact]
    public void TestImportScopeFunction()
    {
        using (Py.GIL())
        {
            ps.Set("bb", 100);
            ps.Set("cc", 10);
            ps.Exec(
                "def func1():\n" +
                "    return cc + bb\n");

            using (var scope = ps.NewScope())
            {
                //'func1' is imported from the origion scope
                scope.Exec(
                    "def func2():\n" +
                    "    return func1() - cc - bb\n");
                dynamic func2 = scope.Get("func2");

                var result1 = func2().As<int>();
                Assert.Equal(0, result1);

                scope.Set("cc", 20);//it has no effect on the globals of 'func1'
                var result2 = func2().As<int>();
                Assert.Equal(-10, result2);
                scope.Set("cc", 10); //rollback

                ps.Set("cc", 20);
                var result3 = func2().As<int>();
                Assert.Equal(10, result3);
                ps.Set("cc", 10); //rollback
            }
        }
    }

    /// <summary>
    /// Use the locals() and globals() method just like in python module
    /// </summary>
    [Fact]
    public void TestVariables()
    {
        using (Py.GIL())
        {
            (ps.Variables() as dynamic)["ee"] = new PyInt(200);
            var a0 = ps.Get<int>("ee");
            Assert.Equal(200, a0);

            ps.Exec("locals()['ee'] = 210");
            var a1 = ps.Get<int>("ee");
            Assert.Equal(210, a1);

            ps.Exec("globals()['ee'] = 220");
            var a2 = ps.Get<int>("ee");
            Assert.Equal(220, a2);

            using (var item = ps.Variables())
            {
                item["ee"] = new PyInt(230);
            }
            var a3 = ps.Get<int>("ee");
            Assert.Equal(230, a3);
        }
    }

    /// <summary>
    /// Share a pyscope by multiple threads.
    /// </summary>
    [Fact]
    public void TestThread()
    {
        //After the proposal here https://github.com/pythonnet/pythonnet/pull/419 complished,
        //the BeginAllowThreads statement blow and the last EndAllowThreads statement
        //should be removed.
        dynamic _ps = ps;
        var ts = PythonEngine.BeginAllowThreads();
        try
        {
            using (Py.GIL())
            {
                _ps.res = 0;
                _ps.bb = 100;
                _ps.th_cnt = 0;
                //add function to the scope
                //can be call many times, more efficient than ast
                ps.Exec(
                    "import threading\n"+
                    "lock = threading.Lock()\n"+
                    "def update():\n" +
                    "  global res, th_cnt\n" +
                    "  with lock:\n" +
                    "    res += bb + 1\n" +
                    "    th_cnt += 1\n"
                );
            }
            int th_cnt = 100;
            for (int i = 0; i < th_cnt; i++)
            {
                System.Threading.Thread th = new System.Threading.Thread(() =>
                {
                    using (Py.GIL())
                    {
                        //ps.GetVariable<dynamic>("update")(); //call the scope function dynamicly
                        _ps.update();
                    }
                });
                th.Start();
            }
            //equivalent to Thread.Join, make the main thread join the GIL competition
            int cnt = 0;
            while (cnt != th_cnt)
            {
                using (Py.GIL())
                {
                    cnt = ps.Get<int>("th_cnt");
                }
                Thread.Yield();
            }
            using (Py.GIL())
            {
                var result = ps.Get<int>("res");
                Assert.Equal(101 * th_cnt, result);
            }
        }
        finally
        {
            PythonEngine.EndAllowThreads(ts);
        }
    }

    [Fact]
    public void TestCreate()
    {
        using var scope = Py.CreateScope();

        Assert.False(PyModule.SysModules.HasKey("testmod"));

        PyModule testmod = new PyModule("testmod");

        testmod.SetAttr("testattr1", "True".ToPython());

        PyModule.SysModules.SetItem("testmod", testmod);

        using PyObject code = PythonEngine.Compile(
            "import testmod\n" +
            "x = testmod.testattr1"
            );
        scope.Execute(code);

        Assert.True(scope.TryGet("x", out dynamic x));
        Assert.Equal("True", x.ToString());
    }

    [Fact]
    public void ImportClrNamespace()
    {
        Py.Import(GetType().Namespace);
    }
}
