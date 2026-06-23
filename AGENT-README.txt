================================================================================
AGENT-README: CodeBrix.Python
A Comprehensive Guide for AI Coding Agents
================================================================================

OVERVIEW
--------------------------------------------------------------------------------
CodeBrix.Python is a cross-platform Python <-> .NET language-interoperability
library: a faithful port of Python.NET (pythonnet) 3.1.0, re-namespaced to
CodeBrix.Python.* and retargeted to run exclusively on .NET 10 and later. It
embeds a CPython interpreter in a .NET process, marshals objects across the
Python/CLR boundary, and (via the embedded `clr` module) lets Python load and
call .NET assemblies.

It is functionally identical to pythonnet 3.1.0; only the .NET namespace and
assembly name changed (Python.Runtime -> CodeBrix.Python). The Python-facing
module name (`clr`) is unchanged, so the Python API is identical.

RUNTIME REQUIREMENT: a CPython shared library (libpython) must be available at
run time. Supported CPython versions match pythonnet 3.1.0 (3.10 - 3.14).


INSTALLATION
--------------------------------------------------------------------------------
NuGet package:   CodeBrix.Python.MitLicenseForever
Command:         dotnet add package CodeBrix.Python.MitLicenseForever

The PACKAGE id carries the ".MitLicenseForever" suffix; the NAMESPACE is simply
"CodeBrix.Python" (no suffix).

Target framework: .NET 10.0 or higher. A CPython 3.10-3.14 runtime must be
discoverable (e.g. via Runtime.PythonDLL / the PYTHONNET_PYDLL environment
variable, or platform auto-discovery).


KEY NAMESPACES
--------------------------------------------------------------------------------
  using CodeBrix.Python;                       // PythonEngine, Py, PyObject, ...
  using CodeBrix.Python.Codecs;                // encoders/decoders

NOTE: every namespace that was `Python.Runtime[.X]` upstream is now
`CodeBrix.Python[.X]`. Sub-namespaces include Codecs, CollectionWrappers,
Native, PythonTypes, StateSerialization, Types, Util, Mixins, Slots,
Reflection, Platform. (The low-level interop entry point that upstream exposed
as the `Python.Runtime.Runtime` class is now the `internal` `CodeBrix.Python.Runtime`
class; consumers use the public surface below, not that class directly.)


CORE API REFERENCE
--------------------------------------------------------------------------------
The public surface is identical to pythonnet 3.1.0. Primary entry points:

  - PythonEngine        : Initialize() / Shutdown(); interpreter lifecycle,
                          BeginAllowThreads, version info, PythonPath, PythonHome.
  - Py                  : Py.GIL() (acquire the Global Interpreter Lock),
                          Py.Import(name), Py.kw, Py.None, scope helpers.
  - PyObject            : the universal wrapper around a Python object; dynamic
                          dispatch, indexing, invocation, conversion to CLR types.
  - PyModule            : module / scope object; Exec/Eval Python source,
                          Get/Set/Import; created via Py.CreateScope().
  - Py types            : PyInt, PyFloat, PyString, PyList, PyTuple, PyDict,
                          PySequence, PyIter, PyType, PyNumber, etc.
  - PythonException     : CLR exception wrapping a Python exception.
  - RuntimeData         : interpreter state serialization hooks.
  - PyObjectConversions / IPyObjectEncoder / IPyObjectDecoder : register custom
                          encoders/decoders for CLR<->Python type conversion.

Typical embedding flow: PythonEngine.Initialize(); using (Py.GIL()) { ...
Py.Import / PyObject calls ... }; PythonEngine.Shutdown();

Error model: Python errors surface as CodeBrix.Python.PythonException; GIL
misuse and lifecycle errors throw standard CLR exceptions.


CODING CONVENTIONS (CodeBrix family)
--------------------------------------------------------------------------------
This library follows CodeBrix conventions with the DOCUMENTED situational
exceptions below, all required for a faithful net10 port of pythonnet (each is
explained in full in the library .csproj header):

  - <Nullable>annotations</Nullable> (NOT "enable"). The `?` annotations are
    honored - so the public API signatures stay byte-identical to pythonnet
    (same motivation as CodeBrix.Platform.OpenGL's Nullable exception) - but
    nullable FLOW warnings are off, because on net10 they are almost entirely
    BCL-annotation drift versus the upstream netstandard2.0 target (the code is
    nullable-warning-clean on netstandard2.0), not real defects.
  - <GenerateDocumentationFile> is on; CS1591 is suppressed (large undocumented
    interop surface), like CodeBrix.AssemblyTools / CodeBrix.Platform.OpenGL.
  - <AllowUnsafeBlocks>true</> is required by the native interop.
  - <NoWarn> additionally suppresses the legacy serialization + Code-Access-
    Security family - SYSLIB0011, SYSLIB0050, SYSLIB0051, SYSLIB0003, CS0672 -
    because pythonnet's public RuntimeData/IFormatter/ISerializable surface is
    kept for parity (these APIs are obsolete-but-inert on net10; the one
    replaceable call was fixed to RuntimeHelpers.GetUninitializedObject), plus
    one benign CS9088 false-positive on a native-interop `scoped in` wrapper.
  - net10's stricter ref-safety is satisfied at source with `scoped`
    annotations on the borrowed-reference helpers (not suppressed).

Otherwise: net10.0 only; no global usings / ImplicitUsings; file-scoped
namespaces. Ported files carry a `//was previously: Python.Runtime...;`
provenance comment on the namespace line.


ARCHITECTURE
--------------------------------------------------------------------------------
src/CodeBrix.Python/ mirrors the upstream src/runtime layout: Codecs/,
CollectionWrappers/, Mixins/, Native/, PythonTypes/, Resources/ (embedded
clr.py, interop.py), StateSerialization/, Types/, Util/. The Resources and
Mixins Python scripts are embedded into the assembly (LogicalName clr.py /
interop.py) and loaded at interpreter startup - they are functionally required.


TESTING
--------------------------------------------------------------------------------
Three test projects under tests/ (run with `dotnet test CodeBrix.Python.slnx`):

  - CodeBrix.Python.Tests        : the embedding tests, converted from the
                                   upstream NUnit suite to xUnit v3 +
                                   SilverAssertions. They embed CPython.
                                   Current status: 233 passing, 0 failing,
                                   7 skipped (upstream [Explicit]/version-gated
                                   tests). Deterministic across runs. Test
                                   parallelization is disabled (the embedded
                                   interpreter is single-threaded under the GIL),
                                   and engine init/shutdown plus the Inheritance
                                   base-type providers are registered once via an
                                   xUnit assembly fixture (GlobalTestsSetup).
  - CodeBrix.Python.TestSupport  : the C# support assembly that Python imports
                                   during tests (upstream Python.Test).
  - CodeBrix.Python.PythonTests  : carries the upstream Python pytest suite
                                   (the .py files under pytests/, namespace-
                                   renamed) plus conftest and fixtures. Its
                                   in-process pytest runner is currently SKIPPED:
                                   running pytest inside the embedded interpreter
                                   with clr.AddReference assembly discovery is not
                                   yet stable under the xUnit host. The .py suite
                                   can be run directly with pytest against the
                                   built package; wiring the in-process runner to
                                   green is a known follow-up.

Running the tests requires a CPython 3.10-3.14 runtime discoverable by the test
process (set PYTHONNET_PYDLL to the libpython path, e.g.
/usr/lib/x86_64-linux-gnu/libpython3.13.so.1.0, or rely on auto-discovery),
plus the Python packages numpy, pytest, and find_libpython importable by that
interpreter.
================================================================================
