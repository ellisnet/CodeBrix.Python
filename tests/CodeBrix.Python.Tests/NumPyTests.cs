using System;
using System.Collections.Generic;
using Xunit;
using SilverAssertions;
using SilverAssertions.Collections;
using SilverAssertions.Numeric;
using SilverAssertions.Primitives;
using SilverAssertions.Specialized;
using CodeBrix.Python;
using CodeBrix.Python.Codecs;

namespace CodeBrix.Python.Tests; //was previously: Python.EmbeddingTest;

public class NumPyTests : IDisposable
{
    public NumPyTests()
    {
        TupleCodec<ValueTuple>.Register();
    }

    public void Dispose()
    {
        PyObjectConversions.Reset();
    }

    [Fact]
    public void TestReadme()
    {
        Assert.Equal("1.0", np.cos(np.pi * 2).ToString());

        dynamic sin = np.sin;
        Assert.StartsWith("-0.95892", sin(5).ToString());

        double c = (double)(np.cos(5) + sin(5));
        Assert.Equal(-0.675262, c, 0.01);

        dynamic a = np.array(new List<float> { 1, 2, 3 });
        Assert.Equal("float64", a.dtype.ToString());

        dynamic b = np.array(new List<float> { 6, 5, 4 }, Py.kw("dtype", np.int32));
        Assert.Equal("int32", b.dtype.ToString());

        Assert.Equal("[ 6. 10. 12.]", (a * b).ToString().Replace("  ", " "));
    }

    [Fact]
    public void MultidimensionalNumPyArray()
    {
        var array = new[,] { { 1, 2 }, { 3, 4 } };
        var ndarray = np.InvokeMethod("asarray", array.ToPython());
        Assert.Equal((2, 2), ndarray.GetAttr("shape").As<(int, int)>());
        Assert.Equal(1, ndarray[(0, 0).ToPython()].InvokeMethod("__int__").As<int>());
        Assert.Equal(array[1, 0], ndarray[(1, 0).ToPython()].InvokeMethod("__int__").As<int>());
    }

    [Fact]
    public void Int64Array()
    {
        var array = new long[,] { { 1, 2 }, { 3, 4 } };
        var ndarray = np.InvokeMethod("asarray", array.ToPython());
        Assert.Equal((2, 2), ndarray.GetAttr("shape").As<(int, int)>());
        Assert.Equal(1, ndarray[(0, 0).ToPython()].InvokeMethod("__int__").As<long>());
        Assert.Equal(array[1, 0], ndarray[(1, 0).ToPython()].InvokeMethod("__int__").As<long>());
    }

    [Fact]
    public void VarArg()
    {
        dynamic zX = np.array(new[,] { { 1, 2, 3 }, { 4, 5, 6 }, { 8, 9, 0 } });
        dynamic grad = np.gradient(zX, 4.0, 5.0);
        dynamic grad2 = np.InvokeMethod("gradient", new PyObject[] {zX, new PyFloat(4.0), new PyFloat(5.0)});

        Assert.Equal(4.125, grad[0].sum().__float__().As<double>(), 0.001);
        Assert.Equal(-1.2, grad[1].sum().__float__().As<double>(), 0.001);
        Assert.Equal(4.125, grad2[0].sum().__float__().As<double>(), 0.001);
        Assert.Equal(-1.2, grad2[1].sum().__float__().As<double>(), 0.001);
    }

#pragma warning disable IDE1006
    dynamic np
    {
        get
        {
            try
            {
                return Py.Import("numpy");
            }
            catch (PythonException ex)
            {
                Assert.Fail(
                    "This test requires the 'numpy' package, but it could not be imported by the " +
                    "embedded Python interpreter. Install numpy into the Python runtime these tests " +
                    "use (the libpython located via PYTHONNET_PYDLL / appsettings.json). " +
                    MissingPythonPackage.InstallHint("numpy") +
                    " Underlying import error: " + ex.Message);
                return null;
            }
        }
    }

}
