using System;
using System.Runtime.CompilerServices;
using System.Text;
using Xunit;
using SilverAssertions;
using SilverAssertions.Collections;
using SilverAssertions.Numeric;
using SilverAssertions.Primitives;
using SilverAssertions.Specialized;
using CodeBrix.Python;
using CodeBrix.Python.Codecs;

namespace CodeBrix.Python.Tests; //was previously: Python.EmbeddingTest;

public class TestPyBuffer : IDisposable
{
    public TestPyBuffer()
    {
        TupleCodec<ValueTuple>.Register();
    }

    public void Dispose()
    {
        PyObjectConversions.Reset();
    }

    [Fact]
    public void TestBufferWrite()
    {
        string bufferTestString = "hello world! !$%&/()=?";
        string bufferTestString2 = "h llo world! !$%&/()=?";

        using var _ = Py.GIL();

        using var pythonArray = ByteArrayFromAsciiString(bufferTestString);

        using (PyBuffer buf = pythonArray.GetBuffer(PyBUF.WRITABLE))
        {
            byte[] managedArray = { (byte)' ' };
            buf.Write(managedArray, 0, managedArray.Length, 1);
        }

        string result = pythonArray.InvokeMethod("decode", "utf-8".ToPython()).As<string>();
        Assert.True(result == bufferTestString2);
    }

    [Fact]
    public void TestBufferRead()
    {
        string bufferTestString = "hello world! !$%&/()=?";

        using var _ = Py.GIL();

        using var pythonArray = ByteArrayFromAsciiString(bufferTestString);
        byte[] managedArray = new byte[bufferTestString.Length];

        using (PyBuffer buf = pythonArray.GetBuffer())
        {
            managedArray[0] = (byte)' ';
            buf.Read(managedArray, 1, managedArray.Length - 1, 1);
        }

        string result = new UTF8Encoding().GetString(managedArray);
        Assert.True(result == " " + bufferTestString.Substring(1));
    }

    [Fact]
    public void ArrayHasBuffer()
    {
        var array = new[,] {{1, 2}, {3,4}};
        var memoryView = PythonEngine.Eval("memoryview");
        var mem = memoryView.Invoke(array.ToPython());
        Assert.Equal(1, mem[(0, 0).ToPython()].As<int>());
        Assert.Equal(array[1, 0], mem[(1, 0).ToPython()].As<int>());
    }

    [Fact]
    public void RefCount()
    {
        using var _ = Py.GIL();
        using var arr = ByteArrayFromAsciiString("hello world! !$%&/()=?");

        Assert.Equal(1, arr.Refcount);

        using (PyBuffer buf = arr.GetBuffer())
        {
            Assert.Equal(2, arr.Refcount);
        }

        Assert.Equal(1, arr.Refcount);
    }

    [Fact]
    public void Finalization()
    {
        if (Type.GetType("Mono.Runtime") is not null)
        {
            Assert.Skip("test unreliable in Mono");
            return;
        }

        using var _ = Py.GIL();
        using var arr = ByteArrayFromAsciiString("hello world! !$%&/()=?");

        Assert.Equal(1, arr.Refcount);

        MakeBufAndLeak(arr);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        Finalizer.Instance.Collect();

        Assert.Equal(1, arr.Refcount);
    }

    [Fact]
    public void MultidimensionalNumPyArray()
    {
        var ndarray = np.arange(24).reshape(1,2,3,4).T;
        PyObject ndim = ndarray.ndim;
        PyObject shape = ndarray.shape;
        PyObject strides = ndarray.strides;
        PyObject contiguous = ndarray.flags["C_CONTIGUOUS"];

        using PyBuffer buf = ndarray.GetBuffer(PyBUF.STRIDED);

        Assert.Multiple(() =>
        {
            Assert.Equal(ndim.As<int>(), buf.Dimensions);
            Assert.Equal(shape.As<long[]>(), buf.Shape);
            Assert.Equal(strides.As<long[]>(), buf.Strides);
            Assert.Equal(contiguous.As<bool>(), buf.IsContiguous(BufferOrderStyle.C));
        });
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static void MakeBufAndLeak(PyObject bufProvider)
    {
        PyBuffer buf = bufProvider.GetBuffer();
    }

    static PyObject ByteArrayFromAsciiString(string str)
    {
        using var scope = Py.CreateScope();
        return Runtime.PyByteArray_FromStringAndSize(str).MoveToPyObject();
    }

    dynamic np
    {
        get
        {
            try
            {
                return Py.Import("numpy");
            }
            catch (PythonException)
            {
                Assert.Skip("Numpy or dependency not installed");
                return null;
            }
        }
    }
}
