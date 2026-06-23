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

// CodeBrix port: the upstream NUnit [OneTimeSetUp] registered the base-type
// providers per class; under the shared embedded interpreter they must be
// registered before any affected CLR type is materialized into the process-wide
// Python type cache, so registration is hoisted to the assembly fixture
// (GlobalTestsSetup) and the providers are shared via these static fields.
public class Inheritance
{
    internal static ExtraBaseTypeProvider SharedExtraBaseTypeProvider;
    internal static NoEffectBaseTypeProvider SharedNoEffectBaseTypeProvider;

    readonly ExtraBaseTypeProvider ExtraBaseTypeProvider = SharedExtraBaseTypeProvider;
    readonly NoEffectBaseTypeProvider NoEffectBaseTypeProvider = SharedNoEffectBaseTypeProvider;

    [Fact]
    public void ExtraBase_PassesInstanceCheck()
    {
        var inherited = new Inherited();
        bool properlyInherited = PyIsInstance(inherited, ExtraBaseTypeProvider.ExtraBase);
        Assert.True(properlyInherited);
    }

    static dynamic PyIsInstance => PythonEngine.Eval("isinstance");

    [Fact]
    public void InheritingWithExtraBase_CreatesNewClass()
    {
        PyObject a = ExtraBaseTypeProvider.ExtraBase;
        var inherited = new Inherited();
        PyObject inheritedClass = inherited.ToPython().GetAttr("__class__");
        Assert.False(PythonReferenceComparer.Instance.Equals(a, inheritedClass));
    }

    [Fact]
    public void InheritedFromInheritedClassIsSelf()
    {
        using var scope = Py.CreateScope();
        scope.Exec($"from {typeof(Inherited).Namespace} import {nameof(Inherited)}");
        scope.Exec($"class B({nameof(Inherited)}): pass");
        PyObject b = scope.Eval("B");
        PyObject bInstance = b.Invoke();
        PyObject bInstanceClass = bInstance.GetAttr("__class__");
        Assert.True(PythonReferenceComparer.Instance.Equals(b, bInstanceClass));
    }

    // https://github.com/pythonnet/pythonnet/issues/1420
    [Fact]
    public void CallBaseMethodFromContainerInNestedClass()
    {
        using var nested = new ContainerClass.InnerClass().ToPython();
        nested.InvokeMethod(nameof(ContainerClass.BaseMethod));
    }

    [Fact]
    public void Grandchild_PassesExtraBaseInstanceCheck()
    {
        using var scope = Py.CreateScope();
        scope.Exec($"from {typeof(Inherited).Namespace} import {nameof(Inherited)}");
        scope.Exec($"class B({nameof(Inherited)}): pass");
        PyObject b = scope.Eval("B");
        PyObject bInst = b.Invoke();
        bool properlyInherited = PyIsInstance(bInst, ExtraBaseTypeProvider.ExtraBase);
        Assert.True(properlyInherited);
    }

    [Fact]
    public void CallInheritedClrMethod_WithExtraPythonBase()
    {
        var instance = new Inherited().ToPython();
        string result = instance.InvokeMethod(nameof(PythonWrapperBase.WrapperBaseMethod)).As<string>();
        Assert.Equal(nameof(PythonWrapperBase.WrapperBaseMethod), result);
    }

    [Fact]
    public void CallExtraBaseMethod()
    {
        var instance = new Inherited();
        using var scope = Py.CreateScope();
        scope.Set(nameof(instance), instance);
        int actual = instance.ToPython().InvokeMethod("callVirt").As<int>();
        Assert.Equal(Inherited.OverridenVirtValue, actual);
    }

    [Fact]
    public void SetAdHocAttributes_WhenExtraBasePresent()
    {
        var instance = new Inherited();
        using var scope = Py.CreateScope();
        scope.Set(nameof(instance), instance);
        scope.Exec($"super({nameof(instance)}.__class__, {nameof(instance)}).set_x_to_42()");
        int actual = scope.Eval<int>($"{nameof(instance)}.{nameof(Inherited.XProp)}");
        Assert.Equal(Inherited.X, actual);
    }

    // https://github.com/pythonnet/pythonnet/issues/1476
    [Fact]
    public void BaseClearIsCalled()
    {
        using var scope = Py.CreateScope();
        scope.Set("exn", new Exception("42"));
        var msg = scope.Eval("exn.args[0]");
        Assert.Equal(2, msg.Refcount);
        scope.Set("exn", null);
        Assert.Equal(1, msg.Refcount);
    }

    // https://github.com/pythonnet/pythonnet/issues/1455
    [Fact]
    public void PropertyAccessorOverridden()
    {
        using var derived = new PropertyAccessorDerived().ToPython();
        derived.SetAttr(nameof(PropertyAccessorDerived.VirtualProp), "hi".ToPython());
        Assert.Equal("HI", derived.GetAttr(nameof(PropertyAccessorDerived.VirtualProp)).As<string>());
    }
}

public class ExtraBaseTypeProvider(PyType ExtraBase) : IPythonBaseTypeProvider, IDisposable
{
    public PyType ExtraBase { get; } = ExtraBase;

    public void Dispose()
    {
        ExtraBase.Dispose();
    }

    public IEnumerable<PyType> GetBaseTypes(Type type, IList<PyType> existingBases)
    {
        if (type == typeof(InheritanceTestBaseClassWrapper))
        {
            return [PyType.Get(type.BaseType), ExtraBase];
        }
        return existingBases;
    }
}

public class NoEffectBaseTypeProvider : IPythonBaseTypeProvider
{
    public IEnumerable<PyType> GetBaseTypes(Type type, IList<PyType> existingBases)
        => existingBases;
}

public class PythonWrapperBase
{
    public string WrapperBaseMethod() => nameof(WrapperBaseMethod);
}

public class InheritanceTestBaseClassWrapper : PythonWrapperBase
{
    public const string ClassName = "InheritanceTestBaseClass";
    public const string ClassSourceCode = "class " + ClassName +
@":
  def virt(self):
    return 42
  def set_x_to_42(self):
    self.XProp = 42
  def callVirt(self):
    return self.virt()
  def __getattr__(self, name):
    return '__getattr__:' + name
  def __setattr__(self, name, value):
    value[name] = name
" + ClassName + " = " + ClassName + "\n";
}

public class Inherited : InheritanceTestBaseClassWrapper
{
    public const int OverridenVirtValue = -42;
    public const int X = 42;
    readonly Dictionary<string, object> extras = new Dictionary<string, object>();
    public int virt() => OverridenVirtValue;
    public int XProp
    {
        get
        {
            using (var scope = Py.CreateScope())
            {
                scope.Set("this", this);
                try
                {
                    return scope.Eval<int>($"super(this.__class__, this).{nameof(XProp)}");
                }
                catch (PythonException ex) when (PythonReferenceComparer.Instance.Equals(ex.Type, Exceptions.AttributeError))
                {
                    if (this.extras.TryGetValue(nameof(this.XProp), out object value))
                        return (int)value;
                    throw;
                }
            }
        }
        set => this.extras[nameof(this.XProp)] = value;
    }
}

public class PropertyAccessorBase
{
    public virtual string VirtualProp { get; set; }
}

public class PropertyAccessorIntermediate: PropertyAccessorBase { }

public class PropertyAccessorDerived: PropertyAccessorIntermediate
{
    public override string VirtualProp { set => base.VirtualProp = value.ToUpperInvariant(); }
}

public class ContainerClass
{
    public void BaseMethod() { }

    public class InnerClass: ContainerClass
    {

    }
}
