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

public class CallableObject : IDisposable
{
    IPythonBaseTypeProvider BaseTypeProvider;

    public CallableObject()
    {
        using var locals = new PyDict();
        PythonEngine.Exec(CallViaInheritance.BaseClassSource, locals: locals);
        BaseTypeProvider = new CustomBaseTypeProvider(new PyType(locals[CallViaInheritance.BaseClassName]));
        PythonEngine.InteropConfiguration.PythonBaseTypeProviders.Add(BaseTypeProvider);
    }

    public void Dispose()
    {
        PythonEngine.InteropConfiguration.PythonBaseTypeProviders.Remove(BaseTypeProvider);
    }

    [Fact]
    public void CallMethodMakesObjectCallable()
    {
        var doubler = new DerivedDoubler();
        dynamic applyObjectTo21 = PythonEngine.Eval("lambda o: o(21)");
        Assert.Equal(doubler.__call__(21), (int)applyObjectTo21(doubler.ToPython()));
    }

    [Fact]
    public void CallMethodCanBeInheritedFromPython()
    {
        var callViaInheritance = new CallViaInheritance();
        dynamic applyObjectTo14 = PythonEngine.Eval("lambda o: o(14)");
        Assert.Equal(callViaInheritance.Call(14), (int)applyObjectTo14(callViaInheritance.ToPython()));
    }

    [Fact]
    public void CanOverwriteCall()
    {
        var callViaInheritance = new CallViaInheritance();
        using var scope = Py.CreateScope();
        scope.Set("o", callViaInheritance);
        scope.Exec("orig_call = o.Call");
        scope.Exec("o.Call = lambda a: orig_call(a*7)");
        int result = scope.Eval<int>("o.Call(5)");
        Assert.Equal(105, result);
    }

    class Doubler
    {
        public int __call__(int arg) => 2 * arg;
    }

    class DerivedDoubler : Doubler { }

    class CallViaInheritance
    {
        public const string BaseClassName = "Forwarder";
        public static readonly string BaseClassSource = $@"
class MyCallableBase:
  def __call__(self, val):
    return self.Call(val)

class {BaseClassName}(MyCallableBase): pass
";
        public int Call(int arg) => 3 * arg;
    }

    class CustomBaseTypeProvider(PyType BaseClass) : IPythonBaseTypeProvider
    {
        public IEnumerable<PyType> GetBaseTypes(Type type, IList<PyType> existingBases)
        {
            Assert.True(BaseClass.Refcount > 0);
            return type != typeof(CallViaInheritance)
                ? existingBases
                : [BaseClass];
        }
    }
}
