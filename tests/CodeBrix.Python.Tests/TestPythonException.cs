using System;
using Xunit;
using SilverAssertions;
using SilverAssertions.Collections;
using SilverAssertions.Numeric;
using SilverAssertions.Primitives;
using SilverAssertions.Specialized;
using CodeBrix.Python;

namespace CodeBrix.Python.Tests; //was previously: Python.EmbeddingTest;

public class TestPythonException
{
    [Fact]
    public void TestMessage()
    {
        var list = new PyList();
        PyObject foo = null;

        var ex = Assert.Throws<PythonException>(() => foo = list[0]);

        Assert.Equal("list index out of range", ex.Message);
        Assert.Null(foo);
    }

    [Fact]
    public void TestType()
    {
        var list = new PyList();
        PyObject foo = null;

        var ex = Assert.Throws<PythonException>(() => foo = list[0]);

        Assert.Equal("IndexError", ex.Type.Name);
        Assert.Null(foo);
    }

    [Fact]
    public void TestNoError()
    {
        // There is no PyErr to fetch
        Assert.Throws<InvalidOperationException>(() => PythonException.FetchCurrentRaw());
        var currentError = PythonException.FetchCurrentOrNullRaw();
        Assert.Null(currentError);
    }

    [Fact]
    public void TestNestedExceptions()
    {
        try
        {
            PythonEngine.Exec(@"
try:
  raise Exception('inner')
except Exception as ex:
  raise Exception('outer') from ex
");
        }
        catch (PythonException ex)
        {
            Assert.IsAssignableFrom<PythonException>(ex.InnerException);
            Assert.Equal("inner", ex.InnerException.Message);
        }
    }

    [Fact]
    public void InnerIsEmptyWithNoCause()
    {
        var list = new PyList();
        PyObject foo = null;

        var ex = Assert.Throws<PythonException>(() => foo = list[0]);

        Assert.Null(ex.InnerException);
    }

    [Fact]
    public void TestPythonExceptionFormat()
    {
        try
        {
            PythonEngine.Exec("raise ValueError('Error!')");
            Assert.Fail("Exception should have been raised");
        }
        catch (PythonException ex)
        {
            // Console.WriteLine($"Format: {ex.Format()}");
            // Console.WriteLine($"Stacktrace: {ex.StackTrace}");
            var formatted = ex.Format();
            Assert.Contains("Traceback", formatted);
            Assert.Contains("(most recent call last):", formatted);
            Assert.Contains("ValueError: Error!", formatted);

            // Check that the stacktrace is properly formatted
            Assert.False(ex.StackTrace.StartsWith("["));
            Assert.DoesNotContain("\\n", ex.StackTrace);
        }
    }

    [Fact]
    public void TestPythonExceptionFormatNoTraceback()
    {
        try
        {
            var module = PyModule.Import("really____unknown___module");
            Assert.Fail("Unknown module should not be loaded");
        }
        catch (PythonException ex)
        {
            // ImportError/ModuleNotFoundError do not have a traceback when not running in a script
            Assert.Equal(ex.StackTrace, ex.Format());
        }
    }

    [Fact]
    public void TestPythonExceptionFormatNormalized()
    {
        try
        {
            PythonEngine.Exec("a=b\n");
            Assert.Fail("Exception should have been raised");
        }
        catch (PythonException ex)
        {
            Assert.Equal("Traceback (most recent call last):\n  File \"<string>\", line 1, in <module>\nNameError: name 'b' is not defined\n", ex.Format());
        }
    }

    [Fact]
    public void TestPythonException_PyErr_NormalizeException()
    {
        using (var scope = Py.CreateScope())
        {
            scope.Exec(@"
class TestException(NameError):
    def __init__(self, val):
        super().__init__(val)
        x = int(val)");
            Assert.True(scope.TryGet("TestException", out PyObject type));

            PyObject str = "dummy string".ToPython();
            var typePtr = new NewReference(type.Reference);
            var strPtr = new NewReference(str.Reference);
            var tbPtr = new NewReference(Runtime.None.Reference);
            Runtime.PyErr_NormalizeException(ref typePtr, ref strPtr, ref tbPtr);

            using var typeObj = typePtr.MoveToPyObject();
            using var strObj = strPtr.MoveToPyObject();
            using var tbObj = tbPtr.MoveToPyObject();
            // the type returned from PyErr_NormalizeException should not be the same type since a new
            // exception was raised by initializing the exception
            Assert.False(PythonReferenceComparer.Instance.Equals(type, typeObj));
            // the message should now be the string from the throw exception during normalization
            Assert.Equal("invalid literal for int() with base 10: 'dummy string'", strObj.ToString());
        }
    }

    [Fact]
    public void TestPythonException_Normalize_ThrowsWhenErrorSet()
    {
        Exceptions.SetError(Exceptions.TypeError, "Error!");
        var pythonException = PythonException.FetchCurrentRaw();
        Exceptions.SetError(Exceptions.TypeError, "Another error");
        Assert.Throws<InvalidOperationException>(() => pythonException.Normalize());
        Exceptions.Clear();
    }
}
