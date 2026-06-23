using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Xunit;
using SilverAssertions;
using SilverAssertions.Collections;
using SilverAssertions.Numeric;
using SilverAssertions.Primitives;
using SilverAssertions.Specialized;
using CodeBrix.Python;

using PyRuntime = CodeBrix.Python.Runtime;

namespace CodeBrix.Python.Tests; //was previously: Python.EmbeddingTest;

public class TestConverter
{
    static readonly Type[] _numTypes = new Type[]
    {
            typeof(short),
            typeof(ushort),
            typeof(int),
            typeof(uint),
            typeof(long),
            typeof(ulong)
    };

    [Theory]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    [InlineData(float.MinValue)]
    [InlineData(float.MaxValue)]
    [InlineData(float.NaN)]
    [InlineData(float.Epsilon)]
    public void TestConvertSingleToManaged(float testValue)
    {
        var pyFloat = new PyFloat(testValue);

        object convertedValue;
        var converted = Converter.ToManaged(pyFloat, typeof(float), out convertedValue, false);

        Assert.True(converted);
        Assert.True(((float)convertedValue).Equals(testValue));
    }

    [Theory]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(double.MinValue)]
    [InlineData(double.MaxValue)]
    [InlineData(double.NaN)]
    [InlineData(double.Epsilon)]
    public void TestConvertDoubleToManaged(double testValue)
    {
        var pyFloat = new PyFloat(testValue);

        object convertedValue;
        var converted = Converter.ToManaged(pyFloat, typeof(double), out convertedValue, false);

        Assert.True(converted);
        Assert.True(((double)convertedValue).Equals(testValue));
    }

    [Fact]
    public void CovertTypeError()
    {
        Type[] floatTypes = new Type[]
        {
            typeof(float),
            typeof(double)
        };
        using (var s = new PyString("abc"))
        {
            foreach (var type in _numTypes.Union(floatTypes))
            {
                object value;
                try
                {
                    bool res = Converter.ToManaged(s, type, out value, true);
                    Assert.False(res);
                    var bo = Exceptions.ExceptionMatches(Exceptions.TypeError);
                    Assert.True(Exceptions.ExceptionMatches(Exceptions.TypeError)
                        || Exceptions.ExceptionMatches(Exceptions.ValueError));
                }
                finally
                {
                    Exceptions.Clear();
                }
            }
        }
    }

    [Fact]
    public void ConvertOverflow()
    {
        using (var num = new PyInt(ulong.MaxValue))
        {
            using var largeNum = PyRuntime.PyNumber_Add(num, num);
            try
            {
                object value;
                foreach (var type in _numTypes)
                {
                    bool res = Converter.ToManaged(largeNum.BorrowOrThrow(), type, out value, true);
                    Assert.False(res);
                    Assert.True(Exceptions.ExceptionMatches(Exceptions.OverflowError));
                    Exceptions.Clear();
                }
            }
            finally
            {
                Exceptions.Clear();
            }
        }
    }

    [Fact]
    public void NoImplicitConversionToBool()
    {
        var pyObj = new PyList(items: new[] { 1.ToPython(), 2.ToPython() }).ToPython();
        Assert.Throws<InvalidCastException>(() => pyObj.As<bool>());
    }

    [Fact]
    public void ToNullable()
    {
        const int Const = 42;
        var i = new PyInt(Const);
        var ni = i.As<int?>();
        Assert.Equal(Const, ni);
    }

    [Fact]
    public void BigIntExplicit()
    {
        BigInteger val = 42;
        var i = new PyInt(val);
        var ni = i.As<BigInteger>();
        Assert.Equal(val, ni);
        var nullable = i.As<BigInteger?>();
        Assert.Equal(val, nullable);
    }

    [Fact]
    public void PyIntImplicit()
    {
        var i = new PyInt(1);
        var ni = (PyObject)i.As<object>();
        Assert.True(PythonReferenceComparer.Instance.Equals(i, ni));
    }

    [Fact]
    public void ToPyList()
    {
        var list = new PyList();
        list.Append("hello".ToPython());
        list.Append("world".ToPython());
        var back = list.ToPython().As<PyList>();
        Assert.Equal(list.Length(), back.Length());
    }

    [Fact]
    public void RawListProxy()
    {
        var list = new List<string> {"hello", "world"};
        var listProxy = PyObject.FromManagedObject(list);
        var clrObject = (CLRObject)ManagedType.GetManagedObject(listProxy);
        Assert.Same(list, clrObject.inst);
    }

    [Fact]
    public void RawPyObjectProxy()
    {
        var pyObject = "hello world!".ToPython();
        var pyObjectProxy = PyObject.FromManagedObject(pyObject);
        var clrObject = (CLRObject)ManagedType.GetManagedObject(pyObjectProxy);
        Assert.Same(pyObject, clrObject.inst);

#pragma warning disable CS0612 // Type or member is obsolete
        const string handlePropertyName = nameof(PyObject.Handle);
#pragma warning restore CS0612 // Type or member is obsolete
        var proxiedHandle = pyObjectProxy.GetAttr(handlePropertyName).As<IntPtr>();
        Assert.Equal(pyObject.DangerousGetAddressOrNull(), proxiedHandle);
    }

    [Fact]
    public void GenericToPython()
    {
        int i = 42;
        var pyObject = i.ToPythonAs<IConvertible>();
        var type = pyObject.GetPythonType();
        Assert.Equal(nameof(IConvertible), type.Name);
    }

    // regression for https://github.com/pythonnet/pythonnet/issues/451
    [Fact]
    public void CanGetListFromDerivedClass()
    {
        using var scope = Py.CreateScope();
        scope.Import(typeof(GetListImpl).Namespace, asname: "test");
        scope.Exec(@"
class PyGetListImpl(test.GetListImpl):
    pass
    ");
        var pyImpl = scope.Get("PyGetListImpl");
        dynamic inst = pyImpl.Invoke();
        List<string> result = inst.GetList();
        (result).Should().Equal(new[] { "testing" });
    }
}

public interface IGetList
{
    List<string> GetList();
}

public class GetListImpl : IGetList
{
    public List<string> GetList() => new() { "testing" };
}
