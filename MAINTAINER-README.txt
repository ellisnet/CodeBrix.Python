================================================================================
MAINTAINER-README: CodeBrix.Python
Notes for people and agents MAINTAINING this repository — not for package
consumers
================================================================================

PURPOSE AND SCOPE
=================
This repository produces exactly one NuGet package:

    CodeBrix.Python.MitLicenseForever
        Source project:  src/CodeBrix.Python/CodeBrix.Python.csproj
        Assembly:        CodeBrix.Python
        Root namespace:  CodeBrix.Python
        License:         MIT
        Consumer docs:   AGENT-README.txt (repo root)

Everything else in the repository (the test projects) is support material
and is never packaged.

If you are consuming the package rather than changing this repository, read
AGENT-README.txt instead - this file has nothing you need.


REPOSITORY LAYOUT
=================
    CodeBrix.Python.slnx            Solution. The Solution Items folder carries
                                    .gitignore, AGENT-README.txt,
                                    EXTRAS-README.txt, global.json,
                                    icon-codebrix-128.png, LICENSE,
                                    MAINTAINER-README.txt, README-INDEX.txt,
                                    README.md and THIRD-PARTY-NOTICES.txt; the
                                    Tests folder carries the test projects.
    global.json                     Selects the Microsoft.Testing.Platform test
                                    runner. Does NOT pin an SDK version. See
                                    BUILDING and TESTING below.
    AGENT-README.txt                Consumer documentation; SHIPS in the nupkg
    MAINTAINER-README.txt           This file
    EXTRAS-README.txt               Non-package content
    README-INDEX.txt                Map of the README files
    README.md                       GitHub / nuget.org front page; SHIPS
    LICENSE                         MIT
    THIRD-PARTY-NOTICES.txt         Upstream attribution; SHIPS
    icon-codebrix-128.png           Package icon; SHIPS
    src/CodeBrix.Python/            The library
    tests/CodeBrix.Python.Tests/            C# embedding tests (xUnit v3)
    tests/CodeBrix.Python.TestSupport/      C# assembly imported BY Python
    tests/CodeBrix.Python.PythonTests/      Python pytest suite + host
    tests/CodeBrix.Python.VenvTests/        virtual-environment activation, in
                                            its own process
    tests/CodeBrix.Python.ExitProbe/        console application the tests run as
                                            a child process to watch what
                                            happens at process exit

The library source folders mirror the upstream project's src/runtime layout, so
that a future upstream diff stays readable:

    Codecs/               conversion codecs (namespace CodeBrix.Python.Codecs)
    CollectionWrappers/   IList/ICollection/IEnumerable views over Python objects
    Mixins/               base-type providers + collections.py / dlr.py
    Native/               interop structs, references, type offsets, loaders
    PythonTypes/          PyObject and the typed wrappers
    Resources/            clr.py and interop.py (embedded)
    Serialization/        JsonFormatter
    StateSerialization/   RuntimeData and the stash/restore types
    Types/                the managed side of the Python type objects
    Util/                 helpers, encodings, environment discovery

IMPORTANT: those are FOLDERS, not namespaces. Almost everything in them
declares `namespace CodeBrix.Python`. The only sub-namespaces that exist are
CodeBrix.Python.Codecs, .CollectionWrappers, .Mixins, .Native, .Platform,
.Reflection, .Serialization, .Slots and .StateSerialization - and of those only
Codecs and Serialization contain public types.

Embedded resources (declared in the library csproj) are functionally required
at interpreter startup, not optional content:

    Resources/clr.py     -> LogicalName "clr.py"
    Resources/interop.py -> LogicalName "interop.py"
    Mixins/*.py          -> default logical names (CodeBrix.Python.Mixins.*.py)

`PythonEngine.Initialize` reads them out of the assembly and evaluates them into
the `clr._extras` module tree. Renaming or dropping any of them breaks the
`clr` module at run time with no compile-time signal.


BUILDING
========
    dotnet restore CodeBrix.Python.slnx
    dotnet build   CodeBrix.Python.slnx

Target framework is net10.0 only, everywhere. There is no netstandard target
and none should be added.

global.json at the repo root does NOT pin an SDK version, so the newest
installed .NET 10 SDK is still used. It exists solely to select the test
runner:

    { "test": { "runner": "Microsoft.Testing.Platform" } }

Because that setting lives in global.json rather than in the csprojs, it
applies to every `dotnet test` run anywhere in the repository, including CI.
Keep the file committed - see TESTING for what breaks without it.

The library csproj carries documented, situational exceptions to the CodeBrix
family conventions. They exist to keep the port faithful; do not "clean them
up" without reading the comment block in
src/CodeBrix.Python/CodeBrix.Python.csproj first:

  -> <Nullable>annotations</Nullable> (NOT "enable"). The `?` annotations are
     honored - so the public API signatures stay byte-identical to the upstream
     project - but nullable FLOW warnings are off, because on net10 they are
     almost entirely BCL-annotation drift versus the upstream netstandard2.0
     target (the code is nullable-warning-clean there), not real defects. Same
     motivation as CodeBrix.Platform.OpenGL's Nullable exception.
  -> <GenerateDocumentationFile>true</> with CS1591 suppressed: the interop
     surface is large and largely undocumented upstream, and retrofitting XML
     doc comments on every member is out of scope. Same exception as
     CodeBrix.AssemblyTools and CodeBrix.Platform.OpenGL.
  -> <AllowUnsafeBlocks>true</> - required by the native CPython interop.
  -> <NoWarn> additionally suppresses the legacy serialization and
     Code-Access-Security family: SYSLIB0011, SYSLIB0050, SYSLIB0051,
     SYSLIB0003, CS0672. The upstream public RuntimeData / IFormatter /
     ISerializable surface is kept for API parity; those APIs are
     obsolete-but-inert on net10 (RuntimeData falls back to NoopFormatter at run
     time). The one genuinely replaceable call was FIXED at source -
     FormatterServices.GetUninitializedObject became
     RuntimeHelpers.GetUninitializedObject - rather than suppressed.
  -> <NoWarn> also carries CS9088 for one native-interop wrapper
     (Runtime.PyType_FromSpecWithBases) that takes a `scoped in NativeTypeSpec`
     and forwards it to a function pointer returning a NewReference. The
     analyzer conservatively treats the result as aliasing the scoped struct;
     the native call does not retain the pointer. The `scoped in` annotation is
     the correct ref-safety contract and the warning is a benign false positive
     for this P/Invoke shape.
  -> net10's stricter ref-safety is satisfied AT SOURCE with `scoped`
     annotations on the borrowed-reference helpers - not suppressed.

Otherwise the usual family rules apply: net10.0 only, no global usings, no
ImplicitUsings, file-scoped namespaces. Every ported file carries a
`//was previously: Python.Runtime...;` provenance comment on its namespace
line; keep it when you touch a file, and add one to any new file adapted from
upstream.

src/CodeBrix.Python/InternalsVisibleTo.cs grants internals to
CodeBrix.Python.Tests and CodeBrix.Python.TestSupport. Several tests use
internal members (for example PyObjectConversions.Reset() and the whole of
PythonEnvironment), so that file is load-bearing. CodeBrix.Python.VenvTests
deliberately needs no entry there: it exercises the public API only.


TESTING
=======
    dotnet test CodeBrix.Python.slnx

As of 2026-09-17 - commit 05567e9, Debian 13, .NET SDK 10.0.401, CPython 3.13.5,
xunit.v3 4.0.1 and Microsoft.NET.Test.Sdk 18.10.1 - that reports 360 passed,
0 failed, 0 skipped across the test projects. Each test executable
can also be run directly, which is the quickest way to see one project's
summary line and to confirm it exits on its own:

    dotnet tests/CodeBrix.Python.Tests/bin/Release/net10.0/CodeBrix.Python.Tests.dll
    dotnet tests/CodeBrix.Python.PythonTests/bin/Release/net10.0/CodeBrix.Python.PythonTests.dll
    dotnet tests/CodeBrix.Python.VenvTests/bin/Release/net10.0/CodeBrix.Python.VenvTests.dll

THE TEST RUNNER IS Microsoft.Testing.Platform (MTP), selected by global.json at
the repo root. Do not delete that file. Every test project uses the xunit.v3 4.x
dialect, which runs under MTP; without global.json, `dotnet test` falls back to
the older VSTest bridge, where an xunit.v3 4.x assembly can report zero tests
found instead of failing loudly. You can tell which one ran: MTP output ends in
a "Test run summary:" block, while the VSTest bridge invokes MSBuild with
`--target:VSTest`.

No test project references coverlet.collector; it was removed
deliberately, so `dotnet test` collects NO code coverage here. Adding a
coverage collector back is a decision, not a cleanup - do not re-add it as a
side effect of another change.

The test projects:

  tests/CodeBrix.Python.Tests
      The embedding tests, converted from the upstream NUnit suite to xUnit v3
      + SilverAssertions. They embed a real CPython interpreter.
      As of 2026-09-17 (commit 05567e9, on CPython 3.13): 344 passing,
      0 failing, 0 skipped.
      Deterministic across runs. The count shows 0 skipped because the upstream
      [Explicit] / environment-gated cases are fenced behind compile-time
      symbols that are intentionally left UNDEFINED
      (ENABLE_GLOBAL_STATE_MUTATION_TESTS, ENABLE_FINALIZER_CHECK_TESTS,
      ENABLE_OLDER_PYTHON_TESTS), so they are not compiled in at all rather
      than reported as Skipped. Each such site carries a comment saying how to
      exercise it.

  tests/CodeBrix.Python.TestSupport
      The C# support assembly that Python imports during the tests (upstream
      Python.Test). Not packable.

  tests/CodeBrix.Python.PythonTests
      Carries the upstream Python pytest suite (the .py files under pytests/,
      with their .NET namespace references renamed) plus conftest and fixtures.
      What runs: two smoke tests confirming that pytest imports inside the
      embedded interpreter and that pytest.approx works; the RunPythonTest
      theory, which runs pytest.main IN-PROCESS inside the embedded interpreter
      over cases from the carried-over .py suite (clr.AddReference assembly
      discovery included); and two reflection-only fences that the assembly
      fixture and the parallelization setting are still declared. As of
      2026-09-17 (commit 05567e9, on CPython 3.13): 7 passing, 0 failing,
      0 skipped, and the test executable exits on its own. The .py suite can also be run directly
      with pytest against the built assemblies.

Test parallelization is DISABLED assembly-wide and engine init/shutdown happens
once, in an xUnit assembly fixture. EVERY C# test project that starts an
interpreter carries its own GlobalTestsSetup.cs declaring
`[assembly: AssemblyFixture(typeof(GlobalTestsSetup))]` and
`[assembly: Parallelization(Mode = ParallelMode.None)]`, because both settings
are per test assembly. Both files need `using Xunit.Sdk;` (for ParallelMode) and
`using Xunit.v3;` (for ParallelizationAttribute) alongside `using Xunit;`. The
embedded interpreter is single-threaded under the GIL, so this is not
negotiable.

The PythonTests fixture follows the engine-ownership pattern the consumer
documentation prescribes: initialize once on first use under a static lock, set
a "shut down" flag and call PythonEngine.Shutdown() exactly once from Dispose(),
and throw from the initializer if anything asks for the engine after that. Every
test scopes its work in `using var pyState = Py.GIL();` plus
`using var pyScope = Py.CreateScope();` and disposes every PyObject it creates,
so nothing reaches the CLR finalizer after the shutdown.

Do NOT remove that shutdown. Without it the test executable never exits: the
library registers an AppDomain.ProcessExit handler that calls
PythonEngine.Shutdown(), the CLR raises ProcessExit on its own shutdown thread,
and Shutdown()'s first act is `using (Py.GIL())` - which blocks forever, because
the thread that ran PythonEngine.Initialize() still holds the GIL (Py_InitializeEx
acquires it and nothing releases it) and is itself waiting for the exit handlers
to finish. Calling Shutdown() explicitly unsubscribes the handler and avoids the
deadlock entirely.

That same fixture also registers the Inheritance tests' base-type providers.
Upstream did this per-class via [OneTimeSetUp]; under one shared interpreter
the registration must precede the first use of the affected types, so it is
hoisted to assembly scope for deterministic ordering.

  tests/CodeBrix.Python.VenvTests
      End-to-end proof that PythonEngine.VirtualEnvironment activates a Python
      virtual environment. It has to be its own assembly, because an
      interpreter can only be started once per process and the other two C#
      test hosts have already started theirs.
      Its assembly fixture creates a THROWAWAY environment under the system
      temp folder with `<base interpreter> -m venv --without-pip` (no network,
      nothing installed), drops a one-line marker module into the
      environment's site-packages, assigns PythonEngine.VirtualEnvironment and
      initializes. The tests then assert sys.prefix, sys.base_prefix,
      sys.executable, that the marker module imports, that the property getter
      reports the environment and that libpython was resolved from the
      environment's own pyvenv.cfg. Dispose() shuts the engine down exactly
      once and deletes the environment.
      Deliberately, nothing here presets PYTHONNET_PYDLL: resolving libpython
      from pyvenv.cfg is the thing being tested.
      How it finds the base interpreter, per OS:
        Linux    `python3` on PATH - the same rule PlatformPythonDll uses.
        Windows  python.exe in the folder holding the python3XX.dll named by
                 the "PythonDllPath" key of appsettings.json.
        macOS    ../bin/python3 relative to the libpython named by the
                 "PythonMacOsPath" key of appsettings.json.
      If none is found the fixture fails with a message naming the key to set.
      As of 2026-09-17 (commit 05567e9, on CPython 3.13): 9 passing, 0 failing,
      0 skipped, and the test executable exits on its own.
      CAVEAT worth knowing when reading failures: a virtual environment records
      the base interpreter it was made from. When that interpreter moves to a
      new minor version the environment stops working until it is recreated.
      This project recreates its environment on every run, so it is immune -
      but a developer's own long-lived environment is not.

  tests/CodeBrix.Python.ExitProbe
      NOT a test project: a console application with no test framework, built as
      part of the solution and copied into the "exitprobe" sub-folder of the
      CodeBrix.Python.Tests output by that project's CopyExitProbeToOutput
      target. `dotnet test` correctly ignores it.
      It exists because the only place the process-exit behaviour is visible is
      a process that is exiting. Program.cs takes a mode - default, bounded,
      skip, shutdown or allowthreads - starts an interpreter, touches sys,
      applies that mode's escape (if any) and returns from Main, having created
      the ready file named by its second argument.
      ProcessExitShutdownModeTests in CodeBrix.Python.Tests runs it once per
      mode, passing the libpython its own PlatformPythonDll discovery found
      through PYTHONNET_PYDLL, and asserts that "default" does NOT exit inside
      its window while the other four exit with code 0. The hung child is killed
      afterwards, so the suite itself cannot hang.
      Run it by hand with PYTHONNET_PYDLL set, and under a timeout - "default"
      hangs on purpose. It does no libpython discovery of its own.

WRITING TESTS IN CodeBrix.Python.Tests: DO NOT AWAIT ANYTHING THAT SUSPENDS.
The interpreter belongs to the thread that called PythonEngine.Initialize(),
which holds the GIL for the whole run and never releases it. xUnit drives a test
collection as one async flow, so an `await` on an operation that has not already
completed resumes that flow on a thread pool thread - and every later test in
the collection then runs there too, without the GIL. The next one to touch the
Python C API takes the process down with a SIGSEGV, in a test that has nothing
to do with the one that awaited. (TestInterrupt's `await`s are safe because the
task is already complete when they run, so the continuation is synchronous.)
Wait by polling instead, as ProcessExitShutdownModeTests does; its poll uses the
test's CancellationToken as the wait handle, so a cancelled run still stops
promptly.

RequiresEngineThreadAttribute enforces that rule. It is a
Xunit.v3.BeforeAfterTestAttribute whose Before() fails the test when the current
managed thread is not Runtime.MainManagedThreadId, with a message naming both
threads and explaining that an awaited continuation moved the run off the
thread that holds the GIL. It is applied ASSEMBLY-WIDE, from an
`[assembly: RequiresEngineThread]` in its own file, so a new test class is
covered without anyone remembering to decorate it: xunit.v3 declares
BeforeAfterTestAttribute with AttributeTargets.Assembly and honours it there.
That was verified rather than assumed - forcing the check to fail turned every
test in the assembly red, on the xunit.v3 version this repository references.
Without the guard the run dies with a native fault in an unrelated later test;
with it, the failure names the test that was about to run and says why.
RequiresEngineThreadAttributeTests covers the decision itself through the
IsEngineThread / DescribeWrongThread helpers, because actually leaving the
thread is the thing that crashes the process and so cannot be tested for real.

HOW THE TESTS FIND CPYTHON
--------------------------
tests/CodeBrix.Python.Tests/PlatformPythonDll.cs (copied byte-for-byte into
every other test project; keep the copies in sync) sets the PYTHONNET_PYDLL
environment variable FOR THE CURRENT PROCESS ONLY, before
PythonEngine.Initialize() runs. Nobody has to configure a
persistent machine or user environment variable. An already-set PYTHONNET_PYDLL
always wins on every platform.

  Linux   Auto-discovery. It runs `python3 -c ...` and asks CPython's own
          sysconfig for LIBDIR plus INSTSONAME (falling back to LDLIBRARY),
          taking whichever file actually exists. This adapts to the distro,
          the CPython minor version and the CPU architecture with nothing
          hard-coded. `python3` must be on PATH and a shared libpython must be
          installed.
  Windows Reads the "PythonDllPath" key from appsettings.json, copied next to
          the test assembly (a python3XX.dll path).
  macOS   Reads the "PythonMacOsPath" key from the same appsettings.json (a
          libpython3.XX.dylib path).

appsettings.json lives beside each test csproj and is copied to the output
directory (CopyToOutputDirectory="PreserveNewest"). Edit the value for your
machine when running on Windows or macOS; the checked-in values are examples.
CodeBrix.Python.VenvTests uses the SAME two keys, but only to locate the base
interpreter executable next to the configured library - it never lets the value
reach the engine, because that project exists to prove the virtual environment
resolves libpython by itself.
A missing/wrong path produces a clear InvalidOperationException naming the key
and the file.

The CPython runtime used by the tests must also have these Python packages
importable: numpy and pytest.


FIRST RUN ON WINDOWS / MACOS
----------------------------
Everything above was verified on Linux. The suite is written to be
OS-independent, but no Windows or macOS machine has ever run it, so this is the
checklist for the first person who does. Work down it in order; each step says
what proves it worked.

0. macOS ONLY - PROVE WHERE LIBPYTHON LIVES, FIRST.
   Every macOS assumption in this repository rests on one claim: that a base
   interpreter's libpython sits in `<home>/../lib`, where <home> is the folder
   the environment's pyvenv.cfg names. That claim was reached by reading the
   layouts of the python.org installer, Homebrew and the relocatable standalone
   builds - it has never been measured. Measure it before anything else:

       python3 -c "import sysconfig; print(sysconfig.get_config_var('LIBDIR'))"
       python3 -c "import sys; print(sys.base_prefix)"

   LIBDIR is where that interpreter's own build says its library lives. It
   should be `<base_prefix>/lib`, and the interpreter itself should be in
   `<base_prefix>/bin` - which is what makes ../lib correct. Confirm a
   libpython3.XX.dylib really is in LIBDIR (`ls "$(python3 -c ...)"`). If it is
   not, PythonEnvironment.GetLibrarySearchDirectories() needs a macOS entry for
   that layout, and CodeBrix.Python.VenvTests is the test that will say so.

1. CONFIGURE THE LIBPYTHON PATH.
   Edit the "PythonDllPath" (Windows) or "PythonMacOsPath" (macOS) value in
   appsettings.json in EACH of these projects - the checked-in values are
   examples and will not match your machine:
       tests/CodeBrix.Python.Tests/appsettings.json
       tests/CodeBrix.Python.PythonTests/appsettings.json
       tests/CodeBrix.Python.VenvTests/appsettings.json
   Windows: the python3XX.dll beside python.exe in the CPython installation,
   e.g. C:\Program Files\Python313\python313.dll. CodeBrix.Python.VenvTests
   derives the base interpreter as python.exe IN THAT SAME FOLDER, so point the
   key at a full installation, not at a copy of the DLL somewhere else. A
   Microsoft Store installation does not have that layout; use the python.org
   installer.
   macOS: the libpython3.XX.dylib from step 0. VenvTests derives the base
   interpreter as ../bin/python3 relative to it, which is the framework layout.

2. CLEAR THE ENVIRONMENT.
   Unset all of PYTHONHOME, PYTHONNET_PYDLL, PYTHONNET_PYEXE, PYTHONNET_VENV and
   VIRTUAL_ENV for the shell that runs the tests, and do not run them from an
   activated virtual environment.
   -> PYTHONHOME overrides an environment's prefix, which makes every
      CodeBrix.Python.VenvTests case fail, and makes TestPythonEngineProperties'
      GetPythonHomeDefault compare against it.
   -> PYTHONNET_PYDLL is honoured ahead of everything else, which would hide
      exactly what VenvTests exists to prove.

3. INSTALL THE PYTHON PACKAGES: numpy and pytest, importable by THAT
   interpreter (`python -m pip install numpy pytest` on Windows,
   `python3 -m pip install numpy pytest` on macOS).

4. BUILD. `dotnet build CodeBrix.Python.slnx -c Release --no-incremental`
   must end in 0 Warning(s) / 0 Error(s). Then confirm the exit probe was
   copied: tests/CodeBrix.Python.Tests/bin/Release/net10.0/exitprobe/ must hold
   CodeBrix.Python.ExitProbe.dll and its launcher
   (CodeBrix.Python.ExitProbe.exe on Windows, CodeBrix.Python.ExitProbe
   elsewhere). Without it every process-exit test fails with a message saying
   to build the solution rather than the project.

5. RUN EACH EXECUTABLE, UNDER A TIMEOUT, and check both the summary line and
   that the process ended by itself:
       dotnet tests/CodeBrix.Python.Tests/bin/Release/net10.0/CodeBrix.Python.Tests.dll
       dotnet tests/CodeBrix.Python.PythonTests/bin/Release/net10.0/CodeBrix.Python.PythonTests.dll
       dotnet tests/CodeBrix.Python.VenvTests/bin/Release/net10.0/CodeBrix.Python.VenvTests.dll
   What proves each one worked:
     CodeBrix.Python.Tests       "Failed: 0, Skipped: 0" and a count at or above
                                 the Linux baseline above. It spawns five child
                                 processes on the way (the exit probe); the run
                                 takes a few seconds longer than the others
                                 because one of them is watched for three
                                 seconds and then killed.
     CodeBrix.Python.PythonTests "Failed: 0" - this is the project that proves
                                 pytest runs INSIDE the embedded interpreter and
                                 that clr.AddReference finds
                                 CodeBrix.Python.TestSupport.
     CodeBrix.Python.VenvTests   "Failed: 0" - this is the project that proves a
                                 virtual environment activates on this OS: the
                                 temp environment it creates becomes sys.prefix,
                                 its launcher becomes sys.executable, its
                                 site-packages is importable, and libpython was
                                 resolved from its pyvenv.cfg alone.
   EVERY ONE of them must exit by itself. A run that reports its results and
   then hangs means something stopped calling PythonEngine.Shutdown() - see the
   ownership note above.
   Then run `dotnet test CodeBrix.Python.slnx` once, for the aggregate.

6. IF SOMETHING FAILS, these are the things that are OS-specific by
   construction, and therefore the places to look first:
     - libpython naming and location: PythonEnvironment.GetDefaultDllName and
       GetLibrarySearchDirectories. Both have tests that assert the running
       platform's answer (GetDefaultDllName_uses_the_shared_library_naming_of_
       the_running_platform, GetLibrarySearchDirectories_probes_the_multiarch_
       folder_on_linux), so a wrong answer fails loudly rather than silently.
     - The environment launcher: Scripts\python.exe on Windows,
       bin/python elsewhere (ProgramNameFromPath, with its own test).
     - Path comparison: macOS reaches the temp folder through symbolic links
       into /private, so sys.prefix comes back with a /private prefix that .NET
       never produces. PythonEnvironment.SamePath and the VenvTests helper both
       normalize it away; SamePath_ignores_the_macOS_private_prefix_where_macOS_
       has_one is the fence.
     - Child processes: CodeBrix.Python.VenvTests runs `<base> -m venv
       --without-pip`, and CodeBrix.Python.Tests runs the exit probe. Both drain
       the child's output while it runs rather than after, so neither can
       deadlock on a full pipe.
     - Deleting the throwaway environment on Windows: the fixture retries a few
       times and then gives up silently. A leftover folder under the temp
       directory is not a failure.

7. KNOWN ITEMS THAT CANNOT BE CHECKED FROM LINUX AT ALL, and what to expect:
     - The macOS ../lib claim: step 0.
     - PlatformPythonDll does NOT auto-discover libpython on Windows or macOS -
       only on Linux, by asking python3 for its own sysconfig. On the other two
       the appsettings key is mandatory. Whether the same sysconfig trick works
       on macOS is unknown: LIBDIR is right, but the INSTSONAME / LDLIBRARY
       values a framework build reports are framework-relative, so the Linux
       code path would look in the wrong place. Do not reuse it there without
       measuring.
     - TestNativeTypeOffset.LoadNativeTypeOffsetClass only asserts anything when
       sys.abiflags exists AND contains a flag other than "m" or "t" - a debug
       CPython build. Windows has no sys.abiflags at all, so the test is inert
       there. With a debug build it will fail, because the NativeTypeOffset type
       it looks for is generated by the upstream project's setup.py and does not
       exist in a package-shaped port. Use a release CPython.
     - Free-threaded (Py_GIL_DISABLED) interpreters: supported in code, never
       exercised anywhere. CPython 3.14t or later is needed to try.
     - Line endings: no test compares against a newline read from a file. Every
       newline in an expected value is a C# escape, so a repository checked out
       with CRLF changes nothing.


PACKAGING AND PUBLISHING
========================
There is no pack script. The library csproj sets
<GeneratePackageOnBuild>true</>, so every build of
src/CodeBrix.Python/CodeBrix.Python.csproj produces a .nupkg.

Package metadata lives entirely in that csproj: PackageId
CodeBrix.Python.MitLicenseForever, PackageLicenseExpression MIT,
PackageRequireLicenseAcceptance true, icon icon-codebrix-128.png, readme
README.md, and a Copyright line that credits both the upstream contributors and
the CodeBrix author. The package has NO NuGet dependencies.

Files packed at the package root:

    icon-codebrix-128.png
    README.md
    AGENT-README.txt          <- the consumer documentation
    THIRD-PARTY-NOTICES.txt

MAINTAINER-README.txt, EXTRAS-README.txt and README-INDEX.txt are repository
documentation and are NOT packed. If the set of packed files ever changes, it
changes in the <ItemGroup> of None Include items in the library csproj.

VERSIONING
----------
Date-stamped, auto-incrementing, computed from UTC "now" at build time in the
form 1.<x>.<y>.<z>:

    1   major     always 1 for this library
    x   minor     whole years since _VersionBaseYear (base 2026: 2026=0, ...)
    y   build     day of year, 1-based, UTC (Jan 1 = 1)
    z   revision  minute of day, UTC, 0..1439

The value is strictly increasing over time: z grows within a day, y outranks it
at a day boundary, x outranks both at a UTC year boundary. Consequences worth
knowing:

  -> Every build produces a NEW version, and with GeneratePackageOnBuild that
     means a fresh .nupkg on every build.
  -> Two builds in the SAME UTC minute produce the SAME version, so never
     publish two packages from within one minute.
  -> This is date-stamp versioning, not SemVer. Major is pinned and minor
     encodes the year, so neither signals API compatibility.
  -> To re-baseline the minor number, change _VersionBaseYear in the csproj.


PROVENANCE AND VENDORED SOURCES
===============================
The whole library is a faithful port of Python.NET (pythonnet) 3.1.0 (git tag
v3.1.0, commit 3d76836e80284aae8abcac92e1676d7d83178ec1), MIT licensed, plus
the CPython 3.15 support cherry-picked from upstream master commit
d2d27164cdcb62ea022a51ffc3ed78f338f01803 ("Python 3.15", PR #2729) and the
Python 3.14 free-threaded support from upstream master commit
7b08d70cc8d0c021abe14c05cb6a6515d653ff61 ("Python 3.14 free-threaded support",
PR #2721) - unreleased upstream work toward 3.2.0. See "UPSTREAM TRACKING" below for exactly what was
taken and what was deliberately not. The
authoritative, detailed record - what was incorporated, what was changed, what
was deliberately left out, and the upstream license text - is
THIRD-PARTY-NOTICES.txt. Read it before any upstream-tracking work. In summary:

  -> The entire upstream src/runtime project (the Python.Runtime assembly)
     became src/CodeBrix.Python, including clr.py, interop.py and the
     Mixins/*.py files, which are carried verbatim.
  -> Namespaces Python.Runtime.* -> CodeBrix.Python.*; assembly Python.Runtime
     -> CodeBrix.Python. The Python-facing module name (`clr`) is UNCHANGED, so
     the Python-side API is identical and upstream Python code runs unmodified.
  -> Retargeted netstandard2.0 -> net10.0. The netstandard-era polyfill package
     references (System.Reflection.Emit, Lost.Compat.NullabilityAttributes,
     Microsoft.CSharp) were dropped because those APIs are built into .NET 10.
     The one source-level polyfill of the same kind, Util/InitOnly.cs
     (IsExternalInit), is neutralized in place - commented out, not deleted -
     because .NET 10 declares that type itself.
  -> Strong-name signing (pythonnet.snk) removed, per family convention;
     InternalsVisibleTo entries retargeted to the renamed, unsigned test
     assemblies.
  -> Properties/AssemblyInfo.cs attributes moved into MSBuild properties.
  -> Block-scoped namespaces converted to file-scoped; nullable-reference-type
     annotations PRESERVED (the interop code depends on them).
  -> Upstream tests/domain_tests (the AppDomain reload suite) was NOT
     incorporated - AppDomain unload/reload is a .NET Framework concept that
     does not apply to a net10-only library.
  -> The formatter-based serialization API and the CAS attributes were kept for
     parity; see the NoWarn notes under BUILDING.

When updating against a newer upstream release, work file by file, preserve the
`//was previously:` comments, and update THIRD-PARTY-NOTICES.txt (version, tag,
commit and the modification list) in the same change.


UPSTREAM TRACKING
=================
Upstream master as of 2026-08-30 (d2d2716) is four commits past the v3.1.0 tag
this port is based on, and only one of them touches ported source:

    d2d2716  Python 3.15 (#2729)         -> TAKEN, see below
    107e9a5  Update dependencies (#2739) -> N/A (uv.lock / NUnit; this repo's
                                            suite is xUnit v3 + SilverAssertions
                                            and carries no uv tooling)
    10c5531  Migrate to slnx (#2733)     -> N/A (already on CodeBrix.Python.slnx)
    fa7b3b7  Back to dev                 -> N/A (version.txt / CHANGELOG stub)

No upstream bug fix, memory-leak fix or behavioural correction is outstanding.
Everything of that kind - the MethodBinding/OverloadMapper leak fix (#2719), the
DLR get/set work, the IDisposable context-manager protocol (#2568), the missing
__all__ on re-import fix (#2717) - landed BEFORE v3.1.0 and is already here.

What was taken from d2d2716:
  -> Native/TypeOffset315.cs added. Derived from this repo's TypeOffset314.cs
     (which is byte-identical to upstream's, modulo the namespace rename,
     file-scoped conversion and the provenance comment); the only content delta
     is the new 3.15 slot `tp_iteritem`, inserted after the commented-out
     `tp_versions_used`. The upstream diff LOOKS large only because git matched
     their new file against their deleted TypeOffset310.cs as a rename.
  -> The _PyObject_Dump P/Invoke wiring was removed from Runtime.Delegates.cs
     (assignment + property) and Runtime.cs (wrapper). It is a private CPython
     symbol that 3.15 no longer exports, it had NO call sites here, and - unlike
     the PyObject_GC_IsTracked lookup right above it - its lookup was not inside
     a try/catch, so a missing symbol would let MissingMethodException escape
     Runtime.Delegates initialization and fail PythonEngine.Initialize() outright.
  -> PythonEngine.MaxSupportedVersion raised 3.14 -> 3.15.

Upstream master as of 2026-09-17 added exactly one further commit on top of
d2d2716, and it touches ported source:

    7b08d70  Python 3.14 free-threaded support (#2721) -> TAKEN, see below

What was taken from 7b08d70 (full sha
7b08d70cc8d0c021abe14c05cb6a6515d653ff61), applied hunk by hunk to the matching
src/CodeBrix.Python files:
  -> Thread-safety corrections that matter on EVERY build, not just
     free-threaded ones: ConcurrentDictionary for ClassManager.cache,
     TypeManager.cache and _slotsHolders, Interop.delegateTypes and
     allocatedThunks, DelegateManager.cache, ModuleObject.cache/allNames, and
     the CLRObject.reflectedObjects / ExtensionType.loadedExtensions
     borrowed-reference registries; a two-cache design in
     ReflectedClrType.GetOrCreate (published cache + in-progress cache) behind
     ClassManager._cacheCreateLock; locks around every Reflection.Emit path
     (ClassDerived.CreateDerivedTypeImpl, GetModuleBuilder,
     DelegateManager.BuildDispatcher); a lock around GenericUtil.mapping with a
     snapshot-then-lookup in GenericByName; a lock around
     PythonEngine.ShutdownHandlers with unlocked invocation; volatile
     PythonEngine.initialized, Runtime._isInitialized / _typesInitialized /
     run, Finalizer.started and MethodBinder.init; a locked _pyRefs with
     snapshot-then-dispose in ResetPyMembers; a snapshot-swap
     AssemblyManager.pypath; Interlocked claim of the per-object GCHandle slot
     in ManagedType.TryFreeGCHandle and ClassDerivedObject.tp_dealloc;
     GC.KeepAlive across the strong->weak demotion in NewObjectToPython; an
     Interlocked dispose guard plus ThrowIfDisposed in PyBuffer; a thread-safe
     lazy MethodBinder.GetMethods with precomputed precedence (MethodSorter is
     gone, as upstream deleted it); a per-thread ClassBase tp_clear re-entrancy
     guard; and the fix for the double PyFinalize when one emitted subclass
     derives from another.
  -> Guards for a CPython that is already finalizing: Finalizer.ThrottledCollect
     and PyObject's finalizer short-circuit on Runtime._Py_IsFinalizing(), and
     the Refcount > 0 Debug.Asserts in Finalizer.AddFinalizedObject and
     Runtime.XDecref are skipped on free-threaded builds.
  -> Free-threaded build support: Native/ABI.cs gained DetectFreeThreaded (via
     sys._is_gil_enabled), IsFreeThreaded, a settable ObjectHeadOffset of 12/16
     for the larger FT header, a renamed ProbeRefCountOffset that is skipped on
     FT, and a NotSupportedException for free-threaded interpreters below
     CPython 3.14; Runtime.Delegates try-loads Py_REFCNT (a real symbol only on
     3.14+) and Runtime.Refcount prefers it.
  -> PythonEnvironment.FindLibPythonInHome now probes the free-threaded library
     names too (libpython3.Xt.so / python3XXt.dll), because pyvenv.cfg's
     version key does not record which build made the venv.
  -> Tests: the upstream embed_tests hunks (TestFinalizer's polling
     FullGCCollect, TestInterrupt's FT-tolerant return code,
     TestNativeTypeOffset's "t" abiflag, TestPyBuffer.ConcurrentDispose), the
     threadtest.cs module lock in TestSupport, and the new pytest cases in
     pytests/test_thread.py and pytests/test_subclass.py.

What was NOT taken from 7b08d70:
  -> .github/workflows/main.yml - this repository has no GitHub workflow files.
  -> doc/source/threading.rst and doc/source/index.rst - this repository does
     not carry the upstream Sphinx documentation tree. The consumer-facing
     content of threading.rst was folded into AGENT-README.txt (THE GIL AND
     THREADING, OBJECT LIFETIME AND THE FINALIZER, COMMON PITFALLS) instead,
     rewritten for this package's names.
  -> The upstream free-threaded pytest cases keep their upstream skip markers
     verbatim: the high-contention ones are FT-only because GIL builds hit a
     pre-existing crash upstream. That is deliberate; do not "fix" it here.

The free-threaded code paths cannot be exercised on this laptop - the installed
CPython is a GIL build - so IsFreeThreaded is false in every local test run.
What IS exercised locally is the whole thread-safety half of the commit.

DELIBERATE DIVERGENCE - do not "fix" this back toward upstream:
  -> PythonEngine.ProcessExitShutdown (a public static ProcessExitShutdownMode),
     PythonEngine.ProcessExitShutdownTimeout (a public static TimeSpan) and the
     public enum ProcessExitShutdownMode have no upstream counterpart. They are
     ADDITIONS ONLY: the default mode, Wait, runs exactly the code the
     AppDomain.ProcessExit handler has always run, on the same thread, with no
     timeout. Nothing about the default path changed, and OnProcessExit was
     neither renamed nor removed.
     Why they exist: Py_InitializeEx leaves the GIL held by the initializing
     thread and nothing releases it; the CLR raises ProcessExit on its own
     shutdown thread while the initializing thread waits for the handlers; and
     Shutdown() needs the GIL. A process that never calls Shutdown() therefore
     hangs at exit, forever, with no .NET-side timeout on the handler. Upstream
     has the same defect and no way out of it. WaitWithTimeout runs the shutdown
     on a background thread and abandons it after the configured wait (rethrowing
     its exception when it did finish, so the bounded path reports what the
     blocking path would report); Skip returns immediately.
     The fence lives in tests/CodeBrix.Python.ExitProbe plus
     ProcessExitShutdownModeTests in CodeBrix.Python.Tests: the "default" probe
     is asserted NOT to exit, so anyone who changes the default path will see
     that test fail rather than discover it in a consumer's application.
  -> PythonEngine.VirtualEnvironment (a public static string?) has no upstream
     counterpart. Upstream can only be told about a virtual environment through
     PYTHONNET_VENV / VIRTUAL_ENV, which Runtime's static initializer reads
     exactly once, so there is no code-level way to say "use this environment"
     and no way at all to say it after the Runtime type has first been touched.
     The property writes Runtime.PythonEnvironment directly, which is what makes
     it work at any point before Initialize(). A libpython that was named
     outright - Runtime.PythonDLL from code, or PYTHONNET_PYDLL - survives the
     assignment; one merely derived from an environment-variable environment does
     not. That distinction is what PythonEnvironment.LibPythonIsExplicit records,
     and it is the only reason Runtime.PythonDLL's setter has a second line.
  -> Runtime.Initialize calls PythonEnvironment.VerifyActivated once the
     interpreter is up, and throws InvalidOperationException when a configured
     environment did not become sys.prefix. Upstream checks nothing, so a
     mis-activated environment runs out of the wrong prefix in silence. The call
     sits after InitPyMembers/ABI.Initialize because that is the first point
     where a Python string can be read back, and still ahead of PythonEngine
     registering its process-exit handler, so a failure cannot deadlock the exit.
  -> PythonEnvironment probes the Linux multiarch folder ../lib/<gnu-tuple>
     alongside ../lib and ../lib64. Without it PYTHONNET_VENV alone cannot work
     on Debian, Ubuntu or any other multiarch distribution, because those put
     libpython in /usr/lib/<gnu-tuple> and nothing in /usr/lib.
  -> PythonEnvironment.ParseVersion replaces two bare Version.TryParse calls.
     pyvenv.cfg's `version` key ("3.13.5", written by CPython's venv module)
     parses either way, but the `version_info` key ("3.13.5.final.0", written by
     the virtualenv tool) does not - upstream silently ends up with no version
     and therefore no libpython for every virtualenv-created environment.
  -> PythonEngine.MinSupportedVersion was raised 3.7 -> 3.10, and
     Native/TypeOffset310.cs was KEPT.
     The old 3.7 was never honest: the real floor is set by the
     Native/TypeOffset3XX.cs tables ABI.Initialize resolves by reflection, and
     the lowest table present is 310, so 3.7-3.9 died in ABI.Initialize with
     NotSupportedException instead of being cleanly rejected.
     Upstream master also reports 3.10 - but it DELETED TypeOffset310.cs when it
     regenerated that file as TypeOffset315.cs, so upstream cannot actually serve
     CPython 3.10 from a plain assembly (only via a setup.py-generated
     NativeTypeOffset, which a NuGet consumer never has). Their pyproject.toml
     now says requires-python ">=3.11" and their CI matrix tests 3.11-3.15, so
     their MinSupportedVersion = 3.10 reads as an oversight.
     Keeping TypeOffset310.cs makes this port's declared range - CPython 3.10
     through 3.15 - actually true, and strictly better than upstream master.


CODING CONVENTIONS
==================
  -> net10.0 only. No netstandard, no multi-targeting.
  -> No global usings, no ImplicitUsings. Usings go ABOVE the file-scoped
     namespace line, in one contiguous block: System.* first, then the rest,
     alphabetical within each group, `using static` and using-alias directives
     last. Aliases and `using static` have to be written FULLY QUALIFIED up
     there (`using static CodeBrix.Python.Runtime;`), because a using directive
     in the compilation unit is not resolved through its sibling directives the
     way one inside a namespace body is.
  -> File-scoped namespaces, each carrying its `//was previously:` provenance
     comment.
  -> Nullable is `annotations`, not `enable` - see BUILDING for why.
  -> Do NOT rename public types, members or namespaces relative to the upstream
     shape (beyond the Python.Runtime -> CodeBrix.Python mapping). API fidelity
     is the point of this port; a rename that reads better is still a
     regression.
  -> Refer to pythonnet as "the upstream project" in prose; do not introduce
     other projects' branding into this repository.
  -> Comment out rather than delete when neutralizing upstream code, so a
     future upstream diff still lines up.
  -> Test files follow the family convention: <Class>Tests.cs where they are
     new, snake_case test method names, //Arrange //Act //Assert bodies. Many
     files here keep their upstream names on purpose (Codecs.cs, Modules.cs,
     pyimport.cs, ...) so they can be diffed against upstream; leave those
     alone.


NOTES
=====
  -> The consumer-facing rating of AGENT-README.txt matters: it is the file
     that ships in the nupkg and the file AI agents read. When you add or
     change public API, update AGENT-README.txt in the same change, and verify
     every signature you write against the source.
  -> AGENT-README.txt must not contain version numbers (they go stale). The
     one exception is the upstream provenance sentence.
  -> PythonEngine.MinSupportedVersion / MaxSupportedVersion are the single
     source of truth for the supported CPython range. Update those (and the
     Native/TypeOffset3XX.cs tables) when adding support for a new CPython
     release - not the documentation.
  -> Native/TypeOffset3XX.cs files encode per-CPython-version C struct layouts.
     A new CPython minor version needs a new file plus registration; there is a
     test (TestNativeTypeOffset.cs) that validates the offsets against the
     running interpreter.
  -> Do not add NuGet dependencies to the library. It is dependency-free by
     design, and a transitive dependency would surface in every consuming app.
================================================================================
