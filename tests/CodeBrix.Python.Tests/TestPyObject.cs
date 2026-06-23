using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using SilverAssertions;
using SilverAssertions.Collections;
using SilverAssertions.Numeric;
using SilverAssertions.Primitives;
using SilverAssertions.Specialized;
using CodeBrix.Python;

namespace CodeBrix.Python.Tests; //was previously: Python.EmbeddingTest;

public class TestPyObject
{
    [Fact]
    public void TestGetDynamicMemberNames()
    {
        List<string> expectedMemberNames = new List<string>
        {
            "add",
            "getNumber",
            "member1",
            "member2"
        };

        PyDict locals = new PyDict();

        PythonEngine.Exec(@"
class MemberNamesTest(object):
    def __init__(self):
        self.member1 = 123
        self.member2 = 'Test string'

    def getNumber(self):
        return 123

    def add(self, x, y):
        return x + y

a = MemberNamesTest()
", null, locals);

        PyObject a = locals.GetItem("a");

        IEnumerable<string> memberNames = a.GetDynamicMemberNames();

        foreach (string expectedName in expectedMemberNames)
        {
            Assert.Contains(expectedName, memberNames);
        }
    }

    [Fact]
    public void InvokeNull()
    {
        var list = PythonEngine.Eval("list");
        Assert.Throws<ArgumentNullException>(() => list.Invoke(new PyObject[] {null}));
    }

    [Fact]
    public void AsManagedObjectInvalidCast()
    {
        var list = PythonEngine.Eval("list");
        Assert.Throws<InvalidCastException>(() => list.AsManagedObject(typeof(int)));
    }

    [Fact]
    public void UnaryMinus_ThrowsOnBadType()
    {
        dynamic list = new PyList();
        var error = Assert.Throws<PythonException>(() => list = -list);
        Assert.Equal("TypeError", error.Type.Name);
    }

    [Fact]
    [Obsolete]
    public void GetAttrDefault_IgnoresAttributeErrorOnly()
    {
        var ob = new PyObjectTestMethods().ToPython();
        using var fallback = new PyList();
        var attrErrResult = ob.GetAttr(nameof(PyObjectTestMethods.RaisesAttributeError), fallback);
        Assert.True(PythonReferenceComparer.Instance.Equals(fallback, attrErrResult));

        var typeErrResult = Assert.Throws<PythonException>(
            () => ob.GetAttr(nameof(PyObjectTestMethods.RaisesTypeError), fallback)
        );
        Assert.Equal(Exceptions.TypeError, typeErrResult.Type);
    }

    // regression test from https://github.com/pythonnet/pythonnet/issues/1642
    [Fact]
    public void InheritedMethodsAutoacquireGIL()
    {
        PythonEngine.Exec("from System import String\nString.Format('{0},{1}', 1, 2)");
    }
}

public class PyObjectTestMethods
{
    public string RaisesAttributeError => throw new PythonException(new PyType(Exceptions.AttributeError), value: null, traceback: null);
    public string RaisesTypeError => throw new PythonException(new PyType(Exceptions.TypeError), value: null, traceback: null);
}
