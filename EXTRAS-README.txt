================================================================================
EXTRAS-README: CodeBrix.Python
Samples, tools and other content in this repository that is not part of a NuGet
package
================================================================================

This repository ships no samples, demo applications or build tools. The only
non-package content is the test material under tests/, described below. None of
it is redistributed in the CodeBrix.Python.MitLicenseForever package.

If you want runnable, verified usage of the library, the test projects ARE the
examples - see the "WORKING EXAMPLES ON GITHUB" section of AGENT-README.txt for
a feature-to-file map.


tests/CodeBrix.Python.Tests
===========================
Path:  tests/CodeBrix.Python.Tests/CodeBrix.Python.Tests.csproj
What:  The C# embedding test suite (xUnit v3 + SilverAssertions), converted
       from the upstream project's NUnit suite. Every test embeds a real
       CPython interpreter.
Run:   dotnet test tests/CodeBrix.Python.Tests/CodeBrix.Python.Tests.csproj
       (or `dotnet test CodeBrix.Python.slnx` for everything)
Needs: A CPython shared library reachable by the test process, plus the numpy,
       pytest and find_libpython packages importable by that interpreter. On
       Linux the libpython path is auto-discovered from `python3`; on Windows
       and macOS it is read from the appsettings.json copied beside the test
       assembly. See MAINTAINER-README.txt, "HOW THE TESTS FIND CPYTHON".
Shows: Engine lifecycle, the GIL, Exec/Eval/Compile, scopes and PyModule,
       every typed Python wrapper, the buffer protocol, codecs and codec
       groups, conversions, base-type providers, delegates/events/callbacks,
       exceptions, the finalizer, interrupts, introspection, numpy interop and
       state serialization.
Note:  Some real test cases are NOT compiled into this project. Three
       compile-time symbols fence them out and are intentionally left
       UNDEFINED - ENABLE_GLOBAL_STATE_MUTATION_TESTS,
       ENABLE_FINALIZER_CHECK_TESTS and ENABLE_OLDER_PYTHON_TESTS - so those
       cases are absent from the run rather than reported as Skipped. Each
       fenced site carries a comment saying how to exercise it; see
       MAINTAINER-README.txt, "TESTING".
Note:  tests/CodeBrix.Python.Tests/PlatformPythonDll.cs is a self-contained,
       copyable helper for locating libpython on Windows, macOS and Linux
       before PythonEngine.Initialize() runs. It is useful outside the tests.

Optional test data: tests/CodeBrix.Python.Tests/fixtures/PyImportTest contains
the Python package the import tests load from disk; the .py files are copied to
the output directory by the csproj.


tests/CodeBrix.Python.TestSupport
=================================
Path:  tests/CodeBrix.Python.TestSupport/CodeBrix.Python.TestSupport.csproj
What:  A C# class library (IsPackable=false) whose types exist to be imported
       and driven FROM Python during the tests. It is the port of the upstream
       Python.Test assembly.
Run:   Not run directly; it is a project reference of the two test projects and
       is loaded from Python with clr.AddReference("CodeBrix.Python.TestSupport").
Shows: How to write .NET types that Python consumes well - arrays, callbacks,
       classes and constructors, conversions, delegates, DynamicObject
       interop, enums, events, fields, generics, indexers, interfaces,
       methods, module-level members, mapping length, properties, repr,
       subclassable base types and interfaces, threading, and [DocString] /
       [PyExport] usage.


tests/CodeBrix.Python.PythonTests
=================================
Path:  tests/CodeBrix.Python.PythonTests/CodeBrix.Python.PythonTests.csproj
What:  The upstream Python-side pytest suite (pytests/*.py, with their .NET
       namespace references renamed to CodeBrix.Python.TestSupport), its
       conftest.py and fixtures, plus a host that runs pytest in-process.
Run:   dotnet test tests/CodeBrix.Python.PythonTests/CodeBrix.Python.PythonTests.csproj
       What runs is two smoke tests in PythonTestRunner.cs, confirming that
       pytest imports inside the embedded interpreter and that pytest.approx
       works. The full IN-PROCESS pytest runner in the same file (the
       RunPythonTest theory) is fenced behind the compile-time symbol
       ENABLE_INPROCESS_PYTEST_TESTS, which is intentionally left UNDEFINED, so
       it is not compiled in at all rather than reported as Skipped: running
       pytest inside the embedded interpreter with clr.AddReference assembly
       discovery is not yet stable under the xUnit host. The .py suite can be
       run directly with pytest against the built assemblies. Getting the
       in-process runner green is a known follow-up.
Needs: The same CPython runtime the other test projects need, with pytest
       importable. See MAINTAINER-README.txt, "HOW THE TESTS FIND CPYTHON".
Shows: The Python side of the library: `import clr`, clr.AddReference,
       importing .NET namespaces as Python modules, subclassing .NET classes
       and implementing .NET interfaces from Python (__namespace__),
       clr.clrmethod / clr.clrproperty, docstrings, codecs, collection mixins,
       delegates, events, generics, indexers, enums, exceptions, threading and
       sys.argv handling.
================================================================================
