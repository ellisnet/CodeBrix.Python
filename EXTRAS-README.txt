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
Needs: A CPython shared library reachable by the test process, plus the numpy
       and pytest packages importable by that interpreter. On
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
       works; the RunPythonTest theory in the same file, which runs pytest.main
       IN-PROCESS over cases from the carried-over .py suite; and two
       reflection-only fences that the assembly fixture and the parallelization
       setting are still declared. The .py suite can also be run directly with
       pytest against the built assemblies.
Needs: The same CPython runtime the other test projects need, with pytest
       importable. See MAINTAINER-README.txt, "HOW THE TESTS FIND CPYTHON".
Shows: The Python side of the library: `import clr`, clr.AddReference,
       importing .NET namespaces as Python modules, subclassing .NET classes
       and implementing .NET interfaces from Python (__namespace__),
       clr.clrmethod / clr.clrproperty, docstrings, codecs, collection mixins,
       delegates, events, generics, indexers, enums, exceptions, threading and
       sys.argv handling.


tests/CodeBrix.Python.VenvTests
===============================
Path:  tests/CodeBrix.Python.VenvTests/CodeBrix.Python.VenvTests.csproj
What:  End-to-end proof that PythonEngine.VirtualEnvironment activates a Python
       virtual environment. It is a separate assembly because an interpreter can
       only be started once per process.
Run:   dotnet test tests/CodeBrix.Python.VenvTests/CodeBrix.Python.VenvTests.csproj
       (or `dotnet test CodeBrix.Python.slnx` for everything)
Needs: A CPython installation able to create virtual environments
       (`python3 -m venv`). Nothing is installed and no network is used: the
       environment is created with --without-pip, into the system temp folder,
       and deleted again at the end of the run. On Linux the base interpreter is
       `python3` on PATH; on Windows and macOS it is derived from the libpython
       path in appsettings.json. See MAINTAINER-README.txt, "TESTING".
Shows: Setting PythonEngine.VirtualEnvironment before Initialize, and what that
       buys: sys.prefix becomes the environment, sys.base_prefix stays on the
       base installation, sys.executable is the environment's launcher, the
       environment's site-packages is importable, and libpython is resolved from
       the environment's own pyvenv.cfg.


tests/CodeBrix.Python.ExitProbe
===============================
Path:  tests/CodeBrix.Python.ExitProbe/CodeBrix.Python.ExitProbe.csproj
What:  A tiny console application (IsPackable=false, no test framework) that
       starts an interpreter and returns from Main. What happens next belongs to
       the CLR's process-exit sequence, which no in-process test can observe, so
       CodeBrix.Python.Tests runs this as a child process - once per
       PythonEngine.ProcessExitShutdown mode - and watches whether it ends.
Run:   Built as part of the solution and copied into the exitprobe sub-folder of
       the CodeBrix.Python.Tests output, which is where the tests look for it.
       To run it by hand:
         PYTHONNET_PYDLL=<libpython> \
           dotnet tests/CodeBrix.Python.Tests/bin/Release/net10.0/exitprobe/CodeBrix.Python.ExitProbe.dll <mode>
       The modes are default, bounded, skip, shutdown and allowthreads. The
       "default" mode HANGS on purpose; run it under a timeout.
Needs: PYTHONNET_PYDLL naming a libpython. The probe does no discovery of its
       own - the test that spawns it passes the value it discovered.
Shows: The three ways a consumer can keep an exiting process from blocking on
       the interpreter shutdown - see AGENT-README.txt, "SHUTTING DOWN AT
       PROCESS EXIT".
================================================================================
