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

// CodeBrix port: kept block-scoped because PublicEnumerator (below) must live in
// the global namespace (regression test for pythonnet#1601). NUnit
// [OneTimeSetUp]/[OneTimeTearDown] converted to constructor / IDisposable.
namespace CodeBrix.Python.Tests //was previously: Python.EmbeddingTest;
{
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

            string code = $@"
import clr
clr.AddReference('{path}')
";

            Assert.Throws<BadImageFormatException>(() => PythonEngine.Exec(code));
        }
    }
}

// regression for https://github.com/pythonnet/pythonnet/issues/1601
// initialize fails if a class derived from IEnumerable is in global namespace
public class PublicEnumerator : System.Collections.IEnumerable
{
    public System.Collections.IEnumerator GetEnumerator()
    {
        return null;
    }
}
