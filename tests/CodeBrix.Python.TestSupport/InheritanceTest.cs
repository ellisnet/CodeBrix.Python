using System;

namespace CodeBrix.Python.TestSupport; //was previously: Python.Test;

public class BaseClass
{
    public bool IsBase() => true;
}

public class DerivedClass : BaseClass
{
    public new bool IsBase() => false;
}
