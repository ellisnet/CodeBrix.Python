# CodeBrix.Python

A cross-platform Python ↔ .NET language-interoperability library for .NET 10 and later. It lets you embed and drive a CPython interpreter from C#, marshal objects across the Python/CLR boundary, and (from the Python side, via the embedded `clr` module) load and use .NET assemblies.
CodeBrix.Python is provided as a .NET 10 library and associated `CodeBrix.Python.MitLicenseForever` NuGet package.

CodeBrix.Python supports applications and assemblies that target Microsoft .NET version 10.0 and later.
Microsoft .NET version 10.0 is a Long-Term Supported (LTS) version of .NET, and was released on Nov 11, 2025; and will be actively supported by Microsoft until Nov 14, 2028.
Please update your C#/.NET code and projects to the latest LTS version of Microsoft .NET.

## Installation

```
dotnet add package CodeBrix.Python.MitLicenseForever
```

Note that the NuGet package ID and the namespace are different - there is no package named plain `CodeBrix.Python`:

* NuGet package ID: `CodeBrix.Python.MitLicenseForever`
* Assembly and primary namespace: `CodeBrix.Python` - i.e. `using CodeBrix.Python;`

XML documentation (IntelliSense) ships alongside the assembly.

The package has no NuGet dependencies; it calls into the CPython shared library directly.

## CodeBrix.Python supports:

* Embedding CPython in a .NET application and executing Python code from C#.
* Importing Python modules and calling Python functions, classes, and objects from .NET.
* Marshalling values and objects between the CPython and CLR type systems.
* Running inside a Python virtual environment named from code - `PythonEngine.VirtualEnvironment = "/path/to/venv"` before `Initialize()` - so the packages installed there are importable, with a startup check that reports it clearly if the environment did not take effect.
* Choosing what happens to the interpreter when the process exits without an explicit `PythonEngine.Shutdown()` - keep the default blocking shutdown, bound it with a timeout, or skip it.
* Hosting the CLR from Python through the embedded `clr` module (load .NET assemblies, call .NET APIs from Python).
* A complete Python/CLR interop API surface - engine lifecycle and the GIL, typed Python object wrappers, scopes, the buffer protocol, custom conversion codecs, .NET types subclassed from Python, and interpreter state serialization - under the `CodeBrix.Python` namespace.

## Requirements: a CPython runtime

CodeBrix.Python requires a CPython runtime (libpython) to be available at run time - `python3XX.dll` on Windows, `libpython3.XX.dylib` on macOS, `libpython3.XX.so` on Linux. It must be a shared-library build; a static-only CPython cannot be embedded, and the process bitness has to match the CPython build.

Rather than hard-coding a supported version range that will go stale, ask the library: `PythonEngine.MinSupportedVersion`, `PythonEngine.MaxSupportedVersion` and `PythonEngine.IsSupportedVersion(version)`.

Point the library at the runtime either by assigning `Runtime.PythonDLL` before `PythonEngine.Initialize()`, or by setting the `PYTHONNET_PYDLL` environment variable for the process. To run inside a virtual environment, assign `PythonEngine.VirtualEnvironment` instead and libpython is resolved from the environment's own `pyvenv.cfg`. Third-party Python packages such as numpy are not bundled - install them into the CPython installation or virtual environment being embedded.

## Sample Code

### Run Python from .NET

```csharp
using System;
using CodeBrix.Python;

// Either assign the libpython path here, before Initialize()...
Runtime.PythonDLL = "/path/to/libpython3.XX.so";  // or .dll / .dylib
// ...or set the PYTHONNET_PYDLL environment variable for the process instead.

PythonEngine.Initialize();
try
{
    using (Py.GIL())
    {
        using PyObject sys = Py.Import("sys");
        Console.WriteLine(sys.GetAttr("version").As<string>());
    }
}
finally
{
    PythonEngine.Shutdown();
}
```

### Evaluate Python expressions in a scope

```csharp
using System;
using CodeBrix.Python;

// PythonEngine.Initialize() has already been called; see above.
using (Py.GIL())
{
    using PyModule scope = Py.CreateScope();
    scope.Exec("import math");
    double result = scope.Eval("math.sqrt(2)").As<double>();
    Console.WriteLine(result);
}
```

## Documentation

The NuGet package includes `AGENT-README.txt`, a complete API reference and usage guide written for AI coding agents - point your agent at that file when it is writing code against this library.

Additional sample code and usage examples are available in the `CodeBrix.Python.Tests` project:
https://github.com/ellisnet/CodeBrix.Python/tree/main/tests/CodeBrix.Python.Tests

## License

CodeBrix.Python is licensed under the MIT License - see the
[LICENSE](https://github.com/ellisnet/CodeBrix.Python/blob/main/LICENSE) file.

For licensing and provenance information about the open source code included in
this package, see [THIRD-PARTY-NOTICES.txt](https://github.com/ellisnet/CodeBrix.Python/blob/main/THIRD-PARTY-NOTICES.txt).
