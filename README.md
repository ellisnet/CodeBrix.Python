# CodeBrix.Python

A cross-platform Python ↔ .NET language-interoperability library for .NET 10 and later. CodeBrix.Python is a faithful port of [Python.NET (pythonnet)](https://github.com/pythonnet/pythonnet) 3.1.0, re-namespaced to `CodeBrix.Python.*` and retargeted to run exclusively on modern .NET. It lets you embed and drive a CPython interpreter from C#, marshal objects across the Python/CLR boundary, and (from the Python side, via the embedded `clr` module) load and use .NET assemblies.
CodeBrix.Python is provided as a .NET 10 library and associated `CodeBrix.Python.MitLicenseForever` NuGet package.

CodeBrix.Python supports applications and assemblies that target Microsoft .NET version 10.0 and later.
Microsoft .NET version 10.0 is a Long-Term Supported (LTS) version of .NET, and was released on Nov 11, 2025; and will be actively supported by Microsoft until Nov 14, 2028.
Please update your C#/.NET code and projects to the latest LTS version of Microsoft .NET.

CodeBrix.Python requires a CPython runtime (libpython) to be available at run time. It supports the same CPython versions as pythonnet 3.1.0 (CPython 3.10 through 3.14).

## CodeBrix.Python supports:

* Embedding CPython in a .NET application and executing Python code from C#.
* Importing Python modules and calling Python functions, classes, and objects from .NET.
* Marshalling values and objects between the CPython and CLR type systems.
* Hosting the CLR from Python through the embedded `clr` module (load .NET assemblies, call .NET APIs from Python).
* The complete public API surface of pythonnet 3.1.0, under the `CodeBrix.Python` namespace.

## Sample Code

### Run Python from .NET

```csharp
using CodeBrix.Python;

PythonEngine.Initialize();
using (Py.GIL())
{
    dynamic np = Py.Import("sys");
    Console.WriteLine(np.version);
}
PythonEngine.Shutdown();
```

## License

The project is licensed under the MIT License. see: https://en.wikipedia.org/wiki/MIT_License
