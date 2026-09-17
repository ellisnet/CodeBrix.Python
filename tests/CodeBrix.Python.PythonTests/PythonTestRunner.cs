using System;
using System.IO;
using Xunit;

namespace CodeBrix.Python.Tests.PythonSuite; //was previously: Python.PythonTestsRunner;

/// <summary>
/// Exercises the embedded-interpreter + pytest path. The lightweight tests here require the
/// <c>pytest</c> package to be importable (so a missing pytest is reported, prompting installation)
/// but do NOT run a pytest session, so they complete quickly. <see cref="RunPythonTest"/> goes the
/// whole way and runs <c>pytest.main</c> over the carried-over <c>pytests/</c> suite inside the
/// embedded interpreter.
/// The interpreter itself is owned by <see cref="GlobalTestsSetup"/>: initialized once, shut down
/// exactly once at the end of the run. Every PyObject created below is disposed inside its own
/// scope, so nothing is left for the .NET finalizer to release after that shutdown.
/// </summary>
public class PythonTestRunner
{
    /// <summary>
    /// Imports the <c>pytest</c> module, failing with a helpful, platform-specific install message
    /// if it is not available. The caller must already hold the GIL.
    /// </summary>
    static PyObject ImportPytest()
    {
        try
        {
            return Py.Import("pytest");
        }
        catch (PythonException ex)
        {
            Assert.Fail(
                "This test requires the 'pytest' package to be importable by the embedded " +
                "Python interpreter, but importing it failed. Install pytest into the Python " +
                "runtime these tests use (the libpython located via PYTHONNET_PYDLL / " +
                "appsettings.json). " +
                MissingPythonPackage.InstallHint("pytest") +
                " Underlying import error: " + ex.Message);
            throw; // unreachable: Assert.Fail throws.
        }
    }

    // -----------------------------------------------------------------------------------------
    // Stable tests: they DO require the pytest module (so its absence is reported and prompts an
    // install), but they only touch pytest's API directly — no pytest session is run, so they
    // finish fast and never hang.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void Pytest_ModuleImportsAndReportsVersion()
    {
        GlobalTestsSetup.EnsureEngine();

        using var pyState = Py.GIL();
        using PyObject pytest = ImportPytest(); // helpful failure if pytest is missing
        using var pyScope = Py.CreateScope();
        pyScope.Exec("import pytest");
        using PyObject versionObject = pyScope.Eval("pytest.__version__");
        string version = versionObject.As<string>();
        Assert.False(string.IsNullOrWhiteSpace(version), "pytest.__version__ should be a non-empty string");
    }

    [Fact]
    public void Pytest_ApproxComparesFloatsApproximately()
    {
        GlobalTestsSetup.EnsureEngine();

        using var pyState = Py.GIL();
        using PyObject pytest = ImportPytest(); // helpful failure if pytest is missing
        using var pyScope = Py.CreateScope();
        pyScope.Exec("import pytest");
        using PyObject closeObject = pyScope.Eval("(0.1 + 0.2) == pytest.approx(0.3)");
        using PyObject farObject = pyScope.Eval("0.5 == pytest.approx(0.3)");
        Assert.True(closeObject.As<bool>(), "pytest.approx should treat 0.1 + 0.2 as approximately 0.3");
        Assert.False(farObject.As<bool>(), "pytest.approx(0.3) should not equal 0.5");
    }

    // Runs pytest IN-PROCESS inside the embedded interpreter (pytest.main over the carried-over
    // .py suite, with clr.AddReference assembly discovery). The same .py suite can also be run
    // directly with `pytest` against the built assemblies.

    /// <summary>
    /// Runs a single test from one of the carried-over Python test files.
    /// </summary>
    /// <param name="testFile">The .py file name without extension.</param>
    /// <param name="testName">The pytest test function name.</param>
    [Theory]
    [InlineData("test_indexer", "test_boolean_indexer")]
    [InlineData("test_delegate", "test_bool_delegate")]
    [InlineData("test_subclass", "test_implement_interface_and_class")]
    public void RunPythonTest(string testFile, string testName)
    {
        GlobalTestsSetup.EnsureEngine();
        string testsDir = Path.Combine(AppContext.BaseDirectory, "pytests");

        using var pyState = Py.GIL();
        using dynamic os = Py.Import("os");
        // pytest wants to run from the folder holding the .py files, but the working directory
        // belongs to the whole process: leaving it moved would change what every later relative
        // path means, and on Windows it would also keep that folder from being deleted.
        string previousDirectory = (string)os.getcwd();
        os.chdir(testsDir);
        try
        {
            using dynamic sys = Py.Import("sys");
            sys.path.insert(0, testsDir);

            using dynamic pytest = ImportPytest();
            using var args = new PyList();
            args.Append(new PyString($"{testFile}.py::{testName}"));
            args.Append(new PyString("-q"));
            args.Append(new PyString("--no-header"));
            using PyObject resultObject = pytest.main(args);
            Assert.Equal(0, resultObject.As<int>());
        }
        finally
        {
            os.chdir(previousDirectory);
        }
    }
}
