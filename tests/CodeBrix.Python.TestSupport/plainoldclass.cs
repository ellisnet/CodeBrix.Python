using System;

// Split out of methodtest.cs, where the upstream project declares it as a second namespace in
// the same file: a file-scoped namespace can only declare one. The namespace name is part of
// the test surface - the Python suite does 'from PlainOldNamespace import PlainOldClass'.
namespace PlainOldNamespace; //was previously: PlainOldNamespace;

public class PlainOldClass
{
    public PlainOldClass() { }

    public PlainOldClass(int param) { }

    private readonly byte[] payload = new byte[(int)Math.Pow(2, 20)]; //1 MB

    public void NonGenericMethod() { }

    public void GenericMethod<T>() { }

    public void OverloadedMethod() { }

    public void OverloadedMethod(int param) { }
}
