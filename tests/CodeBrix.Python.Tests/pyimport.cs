using System;
using System.IO;
using System.Runtime.InteropServices;
using Xunit;
using SilverAssertions;
using SilverAssertions.Collections;
using SilverAssertions.Numeric;
using SilverAssertions.Primitives;
using SilverAssertions.Specialized;
using CodeBrix.Python;

// CodeBrix port: NUnit [OneTimeSetUp]/[OneTimeTearDown] converted to constructor / IDisposable.
// The regression type this file used to carry - a public class in the GLOBAL namespace, which a
// file-scoped namespace cannot declare - now lives in publicenumerator.cs alongside this file.
namespace CodeBrix.Python.Tests; //was previously: Python.EmbeddingTest;

/// <summary>
/// Test Import unittests and regressions
/// </summary>
public class PyImportTest : IDisposable
{
    readonly string TestPath;

    public PyImportTest()
    {
        /* Append the tests directory to sys.path
         * using reflection to circumvent the private
         * modifiers placed on most Runtime methods. */
        TestPath = Path.Combine(AppContext.BaseDirectory, "fixtures");

        using var str = Runtime.PyString_FromString(TestPath);
        Assert.False(str.IsNull());
        BorrowedReference path = Runtime.PySys_GetObject("path");
        Assert.False(path.IsNull);
        Runtime.PyList_Append(path, str.Borrow());
    }

    public void Dispose()
    {
        using var _ = Py.GIL();
        Py.Import("sys").GetAttr("path").InvokeMethod("remove", new PyString(TestPath));
    }

    /// <summary>
    /// Test subdirectory import
    /// </summary>
    [Fact]
    public void TestDottedName()
    {
        var module = PyModule.Import("PyImportTest.test.one");
        Assert.NotNull(module);
    }

    /// <summary>
    /// Tests that sys.args is set. If it wasn't exception would be raised.
    /// </summary>
    [Fact]
    public void TestSysArgsImportException()
    {
        var module = PyModule.Import("PyImportTest.sysargv");
        Assert.NotNull(module);
    }

    /// <summary>
    /// Test Global Variable casting. GH#420
    /// </summary>
    [Fact]
    public void TestCastGlobalVar()
    {
        dynamic foo = Py.Import("PyImportTest.cast_global_var");
        Assert.Equal("1", foo.FOO.ToString());
        Assert.Equal("1", foo.test_foo().ToString());

        foo.FOO = 2;
        Assert.Equal("2", foo.FOO.ToString());
        Assert.Equal("2", foo.test_foo().ToString());
    }

    [Fact]
    public void BadAssembly()
    {
        string path = Runtime.PythonDLL;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            path = @"C:\Windows\System32\kernel32.dll";
        }

        Assert.True(File.Exists(path));

        // The path goes into a Python string literal, so a Windows path's backslashes have
        // to be escaped: CPython reads \W, \S and \k as invalid escape sequences and warns
        // about them today (a SyntaxError in a future release).
        string pythonPath = path.Replace("\\", "\\\\");

        string code = $@"
import clr
clr.AddReference('{pythonPath}')
";

        Assert.Throws<BadImageFormatException>(() => PythonEngine.Exec(code));
    }

    /// <summary>
    /// Fences the escaping BadAssembly does. A Windows path put into a Python string literal
    /// unescaped is read by CPython as a run of invalid escape sequences - a warning today,
    /// a SyntaxError in a future release - and the path it names is not the path that
    /// arrives. This runs on every operating system because the hazard is the literal, not
    /// the file system.
    /// </summary>
    [Fact]
    public void WindowsStylePathSurvivesAPythonStringLiteral()
    {
        const string original = @"C:\Windows\System32\kernel32.dll";
        string escaped = original.Replace("\\", "\\\\");

        using var scope = Py.CreateScope();
        scope.Exec($"value = '{escaped}'");

        Assert.Equal(original, scope.Get<string>("value"));
    }
}
