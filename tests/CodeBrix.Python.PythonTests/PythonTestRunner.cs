using System;
using System.IO;
using CodeBrix.Python;
using Xunit;

// The embedded CPython interpreter is single-threaded under the GIL.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace CodeBrix.Python.Tests.PythonSuite; //was previously: Python.PythonTestsRunner;

/// <summary>
/// Runs representative tests from the carried-over upstream pythonnet Python
/// test suite (the .py files under <c>pytests/</c>) in-process via pytest, to
/// validate the Python-hosting-the-CLR path end to end. The full suite can also
/// be run directly with <c>pytest</c> against the same files.
/// </summary>
public class PythonTestRunner
{
    static readonly object Gate = new();
    static bool engineReady;

    static void EnsureEngine()
    {
        lock (Gate)
        {
            if (engineReady)
            {
                return;
            }
            string dll = Environment.GetEnvironmentVariable("PYTHONNET_PYDLL");
            if (!string.IsNullOrEmpty(dll))
            {
                Runtime.PythonDLL = dll;
            }
            if (!PythonEngine.IsInitialized)
            {
                PythonEngine.Initialize();
            }
            engineReady = true;
        }
    }

    /// <summary>
    /// Runs a single test from one of the carried-over Python test files.
    /// </summary>
    /// <param name="testFile">The .py file name without extension.</param>
    /// <param name="testName">The pytest test function name.</param>
    [Theory(Skip = "Runs pytest in-process inside the embedded interpreter; that integration " +
                   "(pytest + clr.AddReference assembly discovery) is not yet stable under the xUnit " +
                   "test host. The carried-over .py suite under pytests/ can be run directly with pytest.")]
    [InlineData("test_indexer", "test_boolean_indexer")]
    [InlineData("test_delegate", "test_bool_delegate")]
    [InlineData("test_subclass", "test_implement_interface_and_class")]
    public void RunPythonTest(string testFile, string testName)
    {
        EnsureEngine();
        string testsDir = Path.Combine(AppContext.BaseDirectory, "pytests");

        using (Py.GIL())
        {
            dynamic os = Py.Import("os");
            os.chdir(testsDir);
            dynamic sys = Py.Import("sys");
            sys.path.insert(0, testsDir);

            dynamic pytest = Py.Import("pytest");
            using var args = new PyList();
            args.Append(new PyString($"{testFile}.py::{testName}"));
            args.Append(new PyString("-q"));
            args.Append(new PyString("--no-header"));
            int result = pytest.main(args).As<int>();
            Assert.Equal(0, result);
        }
    }
}
