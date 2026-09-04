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

Everything else in the repository (the three test projects) is support material
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
                                    Tests folder carries the three test
                                    projects.
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
internal members (for example PyObjectConversions.Reset()), so that file is
load-bearing.


TESTING
=======
    dotnet test CodeBrix.Python.slnx

THE TEST RUNNER IS Microsoft.Testing.Platform (MTP), selected by global.json at
the repo root. Do not delete that file. Both test projects use the xunit.v3 4.x
dialect, which runs under MTP; without global.json, `dotnet test` falls back to
the older VSTest bridge, where an xunit.v3 4.x assembly can report zero tests
found instead of failing loudly. You can tell which one ran: MTP output ends in
a "Test run summary:" block, while the VSTest bridge invokes MSBuild with
`--target:VSTest`.

Neither test project references coverlet.collector; it was removed
deliberately, so `dotnet test` collects NO code coverage here. Adding a
coverage collector back is a decision, not a cleanup - do not re-add it as a
side effect of another change.

Three projects:

  tests/CodeBrix.Python.Tests
      The embedding tests, converted from the upstream NUnit suite to xUnit v3
      + SilverAssertions. They embed a real CPython interpreter.
      As of 2026-08-30 (on CPython 3.13): 263 passing, 0 failing, 0 skipped.
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
      Its full IN-PROCESS pytest runner (the RunPythonTest theory) is fenced
      behind a compile-time symbol that is intentionally left UNDEFINED:
      running pytest inside the embedded interpreter with clr.AddReference
      assembly discovery is not yet stable under the xUnit host. What DOES run
      is two smoke tests (2 passing) confirming that pytest imports inside the
      embedded interpreter and that pytest.approx works. The .py suite can
      still be run directly with pytest against the built assemblies. Wiring
      the in-process runner to green is a known follow-up.

Test parallelization is DISABLED assembly-wide and engine init/shutdown happens
once, in an xUnit assembly fixture:
tests/CodeBrix.Python.Tests/GlobalTestsSetup.cs declares
`[assembly: AssemblyFixture(typeof(GlobalTestsSetup))]` and
`[assembly: Parallelization(Mode = ParallelMode.None)]`. The same
Parallelization attribute is declared independently in
tests/CodeBrix.Python.PythonTests/PythonTestRunner.cs, because the setting is
per test assembly. Both files need `using Xunit.Sdk;` (for ParallelMode) and
`using Xunit.v3;` (for ParallelizationAttribute) alongside `using Xunit;`. The
embedded interpreter is single-threaded under the GIL, so this is not
negotiable.

That same fixture also registers the Inheritance tests' base-type providers.
Upstream did this per-class via [OneTimeSetUp]; under one shared interpreter
the registration must precede the first use of the affected types, so it is
hoisted to assembly scope for deterministic ordering.

HOW THE TESTS FIND CPYTHON
--------------------------
tests/CodeBrix.Python.Tests/PlatformPythonDll.cs (duplicated in the PythonTests
project) sets the PYTHONNET_PYDLL environment variable FOR THE CURRENT PROCESS
ONLY, before PythonEngine.Initialize() runs. Nobody has to configure a
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
A missing/wrong path produces a clear InvalidOperationException naming the key
and the file.

The CPython runtime used by the tests must also have these Python packages
importable: numpy, pytest and find_libpython.


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
d2d27164cdcb62ea022a51ffc3ed78f338f01803 ("Python 3.15", PR #2729) - unreleased
upstream work toward 3.2.0. See "UPSTREAM TRACKING" below for exactly what was
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

DELIBERATE DIVERGENCE - do not "fix" this back toward upstream:
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
  -> No global usings, no ImplicitUsings. Usings at the top of each file.
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
