using System;
using System.Collections.Generic;
using Xunit;
using SilverAssertions;
using SilverAssertions.Collections;
using SilverAssertions.Numeric;
using SilverAssertions.Primitives;
using SilverAssertions.Specialized;
using CodeBrix.Python;

namespace CodeBrix.Python.Tests; //was previously: Python.EmbeddingTest;

public class Inspect
{
    [Fact]
    public void InstancePropertiesVisibleOnClass()
    {
        var uri = new Uri("http://example.org").ToPython();
        var uriClass = uri.GetPythonType();
        var property = uriClass.GetAttr(nameof(Uri.AbsoluteUri));
        var pyProp = (PropertyObject)ManagedType.GetManagedObject(property.Reference);
        Assert.Equal(nameof(Uri.AbsoluteUri), pyProp.info.Value.Name);
    }

    [Fact]
    public void BoundMethodsAreInspectable()
    {
        using var scope = Py.CreateScope();
        try
        {
            scope.Import("inspect");
        }
        catch (PythonException)
        {
            Assert.Skip("Python build does not include inspect module");
            return;
        }

        var obj = new Class();
        scope.Set(nameof(obj), obj);
        using var spec = scope.Eval($"inspect.getfullargspec({nameof(obj)}.{nameof(Class.Method)})");
    }

    class Class
    {
        public void Method(int a, int b = 10) { }
        public void Method(int a, object b) { }
    }
}
