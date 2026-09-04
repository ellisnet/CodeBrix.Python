================================================================================
AGENT-README: CodeBrix.Python
A Guide for AI Coding Agents — CONSUMING the CodeBrix.Python.MitLicenseForever
NuGet package
================================================================================

OVERVIEW
========
CodeBrix.Python is a cross-platform Python <-> .NET language-interoperability
library. It embeds a CPython interpreter inside a .NET process, marshals values
and objects across the Python/CLR boundary, and - through the embedded Python
`clr` module - lets Python code load .NET assemblies and call .NET APIs.

Target framework: .NET 10 or later.

Provenance: this library is a faithful port of Python.NET (pythonnet),
including that project's newer CPython support. THIRD-PARTY-NOTICES.txt in the
package carries the exact upstream revisions and the licence text.
Every upstream `Python.Runtime[.X]` namespace became `CodeBrix.Python[.X]`, and
the assembly was renamed from Python.Runtime to CodeBrix.Python. Do NOT write
`using Python.Runtime;` and do NOT add an upstream pythonnet package reference
alongside this one - both assemblies define the Python-side `clr` module and
both try to own the single embedded interpreter. The Python-facing module name
(`clr`) is unchanged, so Python source written against the upstream project
runs unmodified against this package.

RUNTIME REQUIREMENT: a CPython shared library (libpython) must already exist on
the machine and be discoverable at run time. No CPython runtime is shipped
inside the NuGet package, and none is downloaded. See "INTERPRETER SETUP AND
LIFECYCLE" below for the three ways this library finds it.


INSTALLATION
============
NuGet package id:  CodeBrix.Python.MitLicenseForever
Install:           dotnet add package CodeBrix.Python.MitLicenseForever

License: MIT (SPDX: MIT). The package id carries the ".MitLicenseForever"
suffix; the .NET namespace is plain "CodeBrix.Python" - the suffix never
appears in code.

NuGet dependencies: none. The library has no transitive package references; it
P/Invokes into libpython directly.

Requirements:
  -> .NET 10.0 or later (net10.0 target; there is no netstandard target).
  -> A CPython shared library on the host: python3XX.dll on Windows,
     libpython3.XX.dylib on macOS, libpython3.XX.so[.1.0] on Linux. It must be
     a SHARED library build - a static-only CPython cannot be embedded.
  -> `AllowUnsafeBlocks` is NOT required in the consuming project; only this
     library itself compiles unsafe code.
  -> Process bitness must match the CPython build (x64 host, x64 libpython).

Supported CPython range: ask the library instead of hard-coding it.

    Version min  = PythonEngine.MinSupportedVersion;   // static Version
    Version max  = PythonEngine.MaxSupportedVersion;   // static Version
    bool    okay = PythonEngine.IsSupportedVersion(someVersion);

Third-party Python packages (numpy, pandas, ...) are NOT bundled. They must be
installed into the CPython installation or virtual environment that the
embedded interpreter is pointed at.


KEY NAMESPACES / USINGS
=======================
    using CodeBrix.Python;          // Py, PythonEngine, Runtime, PyObject,
                                    // PyModule, PyDict, PyList, PyTuple,
                                    // PyInt, PyFloat, PyString, PyIter,
                                    // PyIterable, PySequence, PyNumber,
                                    // PyType, PyBuffer, PyBUF,
                                    // BufferOrderStyle, PythonException,
                                    // Finalizer, PyObjectConversions,
                                    // IPyObjectEncoder, IPyObjectDecoder,
                                    // InteropConfiguration, RuntimeData, ...

    using CodeBrix.Python.Codecs;   // EncoderGroup, DecoderGroup,
                                    // EncoderGroupExtensions,
                                    // DecoderGroupExtensions, TupleCodec<T>,
                                    // ListDecoder, SequenceDecoder,
                                    // IterableDecoder, EnumPyIntCodec,
                                    // RawProxyEncoder

    using CodeBrix.Python.Serialization;  // JsonFormatter (rarely needed)

Almost all consumer work needs only the first using. Note that the codec
INTERFACES (IPyObjectEncoder, IPyObjectDecoder) and the registration entry
point (PyObjectConversions) live in `CodeBrix.Python`, while the concrete
built-in codecs live in `CodeBrix.Python.Codecs` - implementing a custom codec
usually needs BOTH usings.

Several other sub-namespaces DO exist but expose no public types at all, so you
never write a using for them: `CodeBrix.Python.Native` (internal interop
plumbing), `.CollectionWrappers`, `.Mixins`, `.StateSerialization`, `.Platform`,
`.Reflection` and `.Slots`. Their consumer-visible types live in
`CodeBrix.Python`. "PythonTypes", "Types" and "Util" are SOURCE FOLDERS, not
namespaces at all - their public types are likewise in `CodeBrix.Python`.


CORE API REFERENCE
==================

INTERPRETER SETUP AND LIFECYCLE
-------------------------------
Locating libpython. The library resolves the CPython shared library in this
order, and the FIRST one that yields a path wins:

  1. `Runtime.PythonDLL` set from code (the explicit, deterministic route).
  2. The `PYTHONNET_PYDLL` environment variable.
  3. Virtual-environment discovery: if `PYTHONNET_VENV` (preferred) or
     `VIRTUAL_ENV` names a directory containing `pyvenv.cfg`, the `home` and
     `version` keys in that file are used to locate libpython next to the base
     interpreter (../lib and, on 64-bit Linux, ../lib64), and the venv's
     `bin/python` (`Scripts\python.exe` on Windows) becomes the program name.
     `PYTHONNET_PYEXE` overrides the program name independently.

`Runtime` is a PUBLIC class and `Runtime.PythonDLL` is the property consumers
are expected to set. Its other members are low-level interop and should be
left alone.

    public static string? Runtime.PythonDLL { get; set; }
    public static int     Runtime.MainManagedThreadId { get; }
    public static PyObject Runtime.None { get; }
    public static bool    Runtime.TryCollectingGarbage(int runs)

`Runtime.PythonDLL` must be assigned BEFORE the engine is initialized; setting
it afterwards throws `InvalidOperationException` ("This property must be set
before runtime is initialized").

Interpreter configuration (all `PythonEngine` statics, all set before
`Initialize()`):

    public static string ProgramName { get; set; }   // Py_SetProgramName
    public static string PythonHome  { get; set; }   // Py_SetPythonHome
    public static string PythonPath  { get; set; }   // Py_SetPath
    public static void   SetNoSiteFlag()             // disables the site module
    public static bool   DebugGIL { get; set; }      // default false
    public static InteropConfiguration InteropConfiguration { get; set; }

Reading `PythonHome` requires an initialized engine (it throws
`InvalidOperationException` otherwise); the other getters delegate straight to
the C API. `InteropConfiguration` may only be replaced while the engine is NOT
running.

Read-only interpreter facts (valid after initialization):

    public static string Version    { get; }   // Py_GetVersion
    public static string BuildInfo  { get; }
    public static string Platform   { get; }
    public static string Copyright  { get; }
    public static string Compiler   { get; }
    public static bool   IsInitialized { get; }

Starting and stopping:

    public static void Initialize()
    public static void Initialize(bool setSysArgv = true, bool initSigs = false)
    public static void Initialize(IEnumerable<string> args,
                                  bool setSysArgv = true,
                                  bool initSigs = false)
    public static void Shutdown()

`Initialize` is idempotent - the second and later calls return immediately -
and it does NOT require the GIL. `setSysArgv:true` publishes `args` (or the
process command line, when the no-args overload is used) as `sys.argv`.
`initSigs:true` installs CPython's default signal handlers, which takes signal
handling away from your .NET application; leave it false unless you need
Python's Ctrl+C behaviour.

`PythonEngine` also implements `IDisposable` as a convenience wrapper:

    public PythonEngine()                        // calls Initialize()
    public PythonEngine(params string[] args)    // calls Initialize(args)
    public PythonEngine(IEnumerable<string> args)
    public void Dispose()                        // calls Shutdown()

Shutdown hooks:

    public delegate void ShutdownHandler();
    public static void AddShutdownHandler(ShutdownHandler handler)
    public static void RemoveShutdownHandler(ShutdownHandler handler)

Handlers run in reverse registration order, so a handler sees the same
resources that existed when it was added. Do not add or remove handlers from
inside a handler. `Shutdown()` throws `InvalidOperationException` if the Python
error indicator is still set when it is called.

Interpreter-thread control:

    public static ulong GetPythonThreadID()
    public static int   Interrupt(ulong pythonThreadID)  // KeyboardInterrupt

`Interrupt` returns the number of thread states modified - normally 1, and 0
when the thread id was not found.


THE GIL AND THREADING
---------------------
Every call that touches a Python object requires the CPython Global
Interpreter Lock. Acquire it with `Py.GIL()`, which returns a disposable:

    public static Py.GILState Py.GIL()

    using (Py.GIL())
    {
        // all Python work goes here
    }

`Py.GILState : IDisposable` releases the lock in `Dispose()`. Its finalizer
deliberately THROWS `InvalidOperationException` if the state is garbage
collected without being disposed - a dropped `using` becomes a hard failure,
not a silent leak.

Setting `PythonEngine.DebugGIL = true` (before calling `Py.GIL()`) makes
`Py.GIL()` return a `Py.DebugGILState` instead, which additionally throws when
`Dispose()` runs on a different thread than the one that acquired the lock.
Turn it on while diagnosing threading problems, off in production.

To run long CLR work (or block) without holding the interpreter hostage,
release and re-acquire the whole thread state:

    public static IntPtr BeginAllowThreads()          // Py_BEGIN_ALLOW_THREADS
    public static void   EndAllowThreads(IntPtr ts)   // Py_END_ALLOW_THREADS

    IntPtr ts = PythonEngine.BeginAllowThreads();
    try { DoExpensiveManagedWork(); }
    finally { PythonEngine.EndAllowThreads(ts); }

Rules that are not negotiable:
  -> Initialize the engine once, on one thread, before any other thread uses
     Python.
  -> Acquire and release the GIL on the SAME thread. Never pass a `GILState`
     between threads or store it in a field.
  -> Never `await` inside a `using (Py.GIL())` block: the continuation can
     resume on a different thread pool thread and the release will be wrong.
     Acquire the GIL fresh inside each synchronous segment instead.
  -> A `PyObject` may be created on one thread and used on another, but only
     while that other thread holds the GIL.


EXECUTING PYTHON CODE
---------------------
Three levels, from crudest to most controlled.

    public static int RunSimpleString(string code)

Runs `code` in the `__main__` module namespace. Returns 0 on success and -1 on
error; it does NOT throw a `PythonException` - it prints the traceback to
stderr like the CPython REPL. Use it for fire-and-forget bootstrapping only.

    public static void     Exec(string code, PyDict? globals = null,
                                             PyObject? locals = null)
    public static PyObject Eval(string code, PyDict? globals = null,
                                             PyObject? locals = null)
    public static PyObject Compile(string code, string filename = "",
                                   RunFlagType mode = RunFlagType.File)

`Exec` runs statements; `Eval` evaluates a single expression and returns its
value; both raise `PythonException` on a Python-level error. When `globals` is
null the current Python frame's globals are used if there is one, and otherwise
a throwaway dictionary seeded with `__builtins__` is created just for the call.
When `locals` is null it defaults to whatever `globals` resolved to. Pass a
`PyDict` explicitly when you want to read the results back:

    var locals = new PyDict();
    PythonEngine.Exec("c = a + b", null, locals);
    int c = locals.GetItem("c").As<int>();

`Compile` produces a reusable code object. `RunFlagType` is a public enum:

    public enum RunFlagType : int { Single = 256, File = 257, Eval = 258 }

Use `RunFlagType.Eval` to compile an expression (its value is returned when
executed) and `RunFlagType.File` for statements.

`PythonEngine.RunString(...)` still exists but is marked `[Obsolete]` and will
be removed - use `Exec`, `Eval` or `RunSimpleString`.

Importing:

    public static PyObject Py.Import(string name)      // fully-qualified name
    public static PyObject PyModule.Import(string name)

Both import (loading the module if needed) and return the module object.

    public static void Py.SetArgv()
    public static void Py.SetArgv(params string[] argv)
    public static void Py.SetArgv(IEnumerable<string> argv)

Rewrites `sys.argv` after initialization.

Python `with` blocks are driven from C# by:

    public static void Py.With(PyObject obj, Action<PyObject> body)
    public static void Py.With(PyObject obj, Action<dynamic> body)

which calls `__enter__` / `__exit__` correctly, including exception
suppression when `__exit__` returns truthy.


SCOPES AND MODULES - PyModule
-----------------------------
`PyModule : PyObject` is the scope abstraction: a real Python module object you
can populate, execute code in, and read variables out of. It is the cleanest
way to isolate one piece of Python work from another.

Creation:

    public static PyModule Py.CreateScope()
    public static PyModule Py.CreateScope(string name)
    public PyModule(string name = "")
    public static PyModule FromString(string name, string code)
    public static PyModule FromString(string name, string code, string file)
    public PyModule NewScope()
    public PyModule Reload()
    public static PyDict PyModule.SysModules { get; }

Executing inside the scope (all `locals` parameters default to null, meaning
"use the module's own variables as both globals and locals"):

    public PyModule Exec(string code, PyDict? locals = null)   // fluent
    public PyObject Eval(string code, PyDict? locals = null)
    public T?       Eval<T>(string code, PyDict? locals = null)
    public PyObject Execute(PyObject script, PyDict? locals = null)
    public T?       Execute<T>(PyObject script, PyDict? locals = null)

`Execute` takes a compiled code object from `PythonEngine.Compile`.

Reading and writing scope variables:

    public PyModule Set(string name, object? value)      // fluent
    public PyModule Remove(string name)                  // fluent
    public bool     Contains(string name)
    public PyObject Get(string name)                     // throws if missing
    public bool     TryGet(string name, out PyObject? value)
    public T        Get<T>(string name)
    public bool     TryGet<T>(string name, out T? value)
    public PyDict   Variables()
    public PyModule SetBuiltins(PyDict builtins)

`Set` converts the CLR value to Python automatically. `PyModule` also overrides
`TryGetMember` / `TrySetMember`, so a scope assigned to `dynamic` reads and
writes its variables as properties.

Importing into the scope:

    public PyObject Import(string name, string? asname = null)
    public void     Import(PyModule module, string asname)
    public void     Import(PyObject module, string? asname = null)
    public void     ImportAll(PyModule module)
    public void     ImportAll(PyObject module)
    public void     ImportAll(PyDict dict)

`ImportAll` is the equivalent of `from X import *`.


PYTHON OBJECTS - PyObject
-------------------------
`PyObject : DynamicObject, IDisposable, ISerializable` is the universal wrapper
around one Python object reference. Everything else in the type system derives
from it.

Construction and conversion:

    public static PyObject None { get; }
    public static PyObject FromManagedObject(object ob)
    public PyObject NewReference()                 // new wrapper, +1 refcount
    public object? AsManagedObject(Type t)
    public T As<T>()                          // (T)AsManagedObject(typeof(T))
    public PyType GetPythonType()
    public bool   TypeCheck(PyType typeOrClass)

Going the other way, the extension methods in `ConverterExtension` (namespace
`CodeBrix.Python`, no extra using needed) convert CLR values to Python:

    public static PyObject ToPython(this object? o)
    public static PyObject ToPythonAs<T>(this T? o)

`ToPython()` uses the runtime type of the value; `ToPythonAs<T>()` forces the
static type - use it when an interface or base type must drive overload
resolution or codec selection. Both map `null` to Python `None`.

Attributes:

    public bool     HasAttr(string name)
    public bool     HasAttr(PyObject name)
    public PyObject GetAttr(string name)
    public PyObject GetAttr(string name, PyObject _default)
    public PyObject GetAttr(PyObject name)
    public PyObject GetAttr(PyObject name, PyObject _default)
    public void     SetAttr(string name, PyObject value)
    public void     SetAttr(PyObject name, PyObject value)
    public void     DelAttr(string name)
    public void     DelAttr(PyObject name)

The two-argument `GetAttr` overloads return `_default` instead of raising
`AttributeError`.

Items, indexing and length (all `virtual`, so subclasses specialise them):

    public virtual PyObject GetItem(PyObject key)
    public virtual PyObject GetItem(string key)
    public virtual PyObject GetItem(int index)
    public virtual void     SetItem(PyObject key, PyObject value)
    public virtual void     SetItem(string key, PyObject value)
    public virtual void     SetItem(int index, PyObject value)
    public virtual void     DelItem(PyObject key)
    public virtual void     DelItem(string key)
    public virtual void     DelItem(int index)
    public virtual long     Length()
    public virtual PyObject this[string key]   { get; set; }
    public virtual PyObject this[PyObject key] { get; set; }
    public virtual PyObject this[int index]    { get; set; }

Calling:

    public PyObject Invoke(params PyObject[] args)
    public PyObject Invoke(PyTuple args)
    public PyObject Invoke(PyObject[] args, PyDict? kw)
    public PyObject Invoke(PyTuple args, PyDict? kw)
    public PyObject InvokeMethod(string name, params PyObject[] args)
    public PyObject InvokeMethod(string name, PyTuple args)
    public PyObject InvokeMethod(PyObject name, params PyObject[] args)
    public PyObject InvokeMethod(PyObject name, PyTuple args)
    public PyObject InvokeMethod(string name, PyObject[] args, PyDict? kw)
    public PyObject InvokeMethod(string name, PyTuple args, PyDict? kw)

Keyword arguments come from `Py.kw`, which builds a `Py.KeywordArguments`
(a `PyDict` subclass) from alternating name/value pairs:

    public static Py.KeywordArguments Py.kw(params object?[] kv)

    result = func.Invoke(new[] { 2.ToPython() }, Py.kw("a4", 8));

`kv` must have an even length (otherwise `ArgumentException`) and every key
must be a non-null string. Values are converted automatically.

Predicates, iteration and inspection:

    public bool     IsInstance(PyObject typeOrClass)
    public bool     IsSubclass(PyObject typeOrClass)
    public bool     IsCallable()
    public bool     IsIterable()
    public bool     IsTrue()
    public bool     IsNone()
    public PyIter   GetIterator()
    public PyList   Dir()
    public string?  Repr()
    public override string? ToString()
    public long     Refcount { get; }
    public PyBuffer GetBuffer(PyBUF flags = PyBUF.SIMPLE)
    public virtual bool Equals(PyObject? other)
    public static bool operator ==(PyObject? a, PyObject? b)
    public static bool operator !=(PyObject? a, PyObject? b)

`==` / `Equals` compare by Python VALUE (they call the Python comparison
protocol). To compare by object IDENTITY use the singleton comparer:

    PythonReferenceComparer.Instance.Equals(x, y)     // like `x is y`
    PythonReferenceComparer.Instance.GetHashCode(x)

`PythonReferenceComparer : IEqualityComparer<PyObject>` is also the right
comparer for a `Dictionary<PyObject, ...>` keyed on identity.

Dynamic dispatch. `PyObject` overrides `TryGetMember`, `TrySetMember`,
`TryInvokeMember`, `TryInvoke`, `TryConvert`, `TryBinaryOperation`,
`TryUnaryOperation` and `GetDynamicMemberNames`, so assigning to `dynamic`
gives natural Python syntax including operators and named arguments:

    dynamic sys = Py.Import("sys");
    sys.attr1 = 100;
    Console.WriteLine(sys.version);

Disposal:

    public void Dispose()

`Dispose()` decrements the Python reference count immediately. `PyObject` also
has a finalizer, so an undisposed wrapper is eventually collected by the
`Finalizer` queue (see below) - but disposal is deterministic and is what you
want for anything holding a scarce resource. `Dispose()` on an already-disposed
wrapper is a no-op; using a disposed wrapper throws `ObjectDisposedException`.
The `PyObject.Handle` property still exists but is `[Obsolete]` - do not use
raw handles.


TYPED PYTHON WRAPPERS
---------------------
The hierarchy is:
PyObject -> PyIterable -> PySequence -> PyList / PyTuple / PyString
PyObject -> PyIterable -> PyDict
PyObject -> PyNumber -> PyInt / PyFloat
PyObject -> PyIter / PyType / PyModule

Each wrapper has a `PyObject`-taking constructor that validates the type and a
static `Is...Type(PyObject)` test.

    public class PyIterable : PyObject, IEnumerable<PyObject>
        public PyIterable(PyObject o)
        public PyIter GetEnumerator()

    public class PyIter : PyObject, IEnumerator<PyObject>
        public PyIter(PyObject pyObject)
        public static PyIter GetIter(PyObject iterable)
        public bool     MoveNext()
        public void     Reset()          // always throws NotSupportedException
        public PyObject Current { get; }

    public class PySequence : PyIterable
        public static bool IsSequenceType(PyObject value)
        public PyObject GetSlice(int i1, int i2)
        public void     SetSlice(int i1, int i2, PyObject v)
        public void     DelSlice(int i1, int i2)
        public nint     Index(PyObject item)
        public int      Index32(PyObject item)
        public long     Index64(PyObject item)
        public bool     Contains(PyObject item)
        public PyObject Concat(PyObject other)
        public PyObject Repeat(int count)

    public class PyList : PySequence
        public PyList()
        public PyList(PyObject o)
        public PyList(PyObject[] items)
        public static bool   IsListType(PyObject value)
        public static PyList AsList(PyObject value)     // list(value)
        public void Append(PyObject item)
        public void Insert(int index, PyObject item)
        public void Reverse()
        public void Sort()

    public class PyTuple : PySequence
        public PyTuple()
        public PyTuple(PyObject o)
        public PyTuple(PyObject[] items)
        public static bool    IsTupleType(PyObject value)
        public static PyTuple AsTuple(PyObject value)   // tuple(value)

    public class PyString : PySequence, IComparable<string>, IEquatable<string>
        public PyString(string s)
        public PyString(PyObject o)
        public static bool IsStringType(PyObject value)

    public class PyDict : PyIterable
        public PyDict()
        public PyDict(PyObject o)
        public static bool IsDictType(PyObject value)
        public bool       HasKey(PyObject key)
        public bool       HasKey(string key)
        public PyIterable Keys()
        public PyIterable Values()
        public PyIterable Items()
        public PyDict     Copy()
        public void       Update(PyObject other)
        public void       Clear()

    public class PyNumber : PyObject
        public static bool IsNumberType(PyObject value)

    public partial class PyInt : PyNumber, IFormattable
        public PyInt(int|uint|long|ulong|short|ushort|byte|sbyte value)
        public PyInt(string value)          // parsed like int(str)
        public PyInt(BigInteger value)
        public PyInt(PyObject o)
        public static bool  IsIntType(PyObject value)
        public static PyInt AsInt(PyObject value)      // int(value)
        public short      ToInt16()
        public int        ToInt32()
        public long       ToInt64()
        public BigInteger ToBigInteger()

    public partial class PyFloat : PyNumber
        public PyFloat(double value)
        public PyFloat(string value)
        public PyFloat(PyObject o)
        public static bool    IsFloatType(PyObject value)
        public static PyFloat AsFloat(PyObject value)  // float(value)
        public double ToDouble()

    public class PyType : PyObject
        public PyType(PyObject o)
        public PyType(TypeSpec spec, PyTuple? bases = null)
        public string Name { get; }
        public bool   IsReady { get; }
        public static bool   IsType(PyObject value)
        public static PyType Get(Type clrType)   // Python type for a CLR type
        public new PyType NewReference()

`PyInt` and `PyFloat` implement `IComparable`/`IConvertible` members through
partial classes, so they can be compared and converted like their CLR
counterparts.

`TypeSpec` (with the nested `TypeSpec.Slot` struct, the `TypeSlotID` enum and
the `TypeFlags` flags enum) is the raw heap-type description used by
`new PyType(spec, bases)`. It is only needed when creating a Python type from
C# without executing a `class` statement:

    public class TypeSpec
        public TypeSpec(string name, int basicSize, IEnumerable<Slot> slots,
                        TypeFlags flags, int itemSize = 0)
        public string Name { get; }
        public int    BasicSize { get; }
        public int    ItemSize { get; }
        public TypeFlags Flags { get; }
        public IReadOnlyList<Slot> Slots { get; }
        public struct Slot
            public Slot(TypeSlotID id, IntPtr value)
            public TypeSlotID ID { get; }
            public IntPtr     Value { get; }


BUFFERS - PyBuffer / PyBUF / BufferOrderStyle
---------------------------------------------
`PyObject.GetBuffer(PyBUF flags = PyBUF.SIMPLE)` exposes the CPython buffer
protocol, which is how you move bulk bytes (numpy arrays, `bytearray`,
`memoryview`, .NET arrays proxied to Python) without per-element marshalling.

    public sealed class PyBuffer : IDisposable
        public PyObject Object     { get; }   // the exporting object
        public long     Length     { get; }
        public long     ItemSize   { get; }
        public int      Dimensions { get; }
        public bool     ReadOnly   { get; }
        public IntPtr   Buffer     { get; }   // raw pointer to the memory
        public string?  Format     { get; }   // struct-module format string
        public long[]?  Shape      { get; }
        public long[]?  Strides    { get; }
        public long[]?  SubOffsets { get; }
        public static long SizeFromFormat(string format)
        public bool   IsContiguous(BufferOrderStyle order)
        public IntPtr GetPointer(long[] indices)
        public void   FromContiguous(IntPtr buf, long len,
                                     BufferOrderStyle fort)
        public void   ToContiguous(IntPtr buf, BufferOrderStyle order)
        public void   Write(byte[] buffer, int sourceOffset, int count,
                            nint destinationOffset)
        public void   Read(byte[] buffer, int destinationOffset, int count,
                           nint sourceOffset)
        public void   Dispose()

    public enum BufferOrderStyle { C, Fortran, EitherOne }

    public enum PyBUF   // [Flags]-style combinations, per PEP 3118
        SIMPLE, WRITABLE, FORMATS, ND, STRIDES, C_CONTIGUOUS, F_CONTIGUOUS,
        ANY_CONTIGUOUS, INDIRECT, CONTIG, CONTIG_RO, STRIDED, STRIDED_RO,
        RECORDS, RECORDS_RO, FULL, FULL_RO

Request `PyBUF.WRITABLE` (or a combination containing it) before calling
`Write`; a read-only exporter fails the request rather than silently giving you
a read-only view. Always dispose the `PyBuffer` - the exporting object stays
pinned until you do.

    using var buf = pythonArray.GetBuffer(PyBUF.WRITABLE);
    byte[] managed = { (byte)' ' };
    buf.Write(managed, 0, managed.Length, destinationOffset: 1);


CONVERSION AND CODECS
---------------------
Built-in conversion already handles the obvious cases in both directions:
primitives, `string`, `bool`, arrays, delegates, and any CLR object (which
becomes a Python proxy). Codecs let you override or extend that mapping.

The two interfaces (namespace `CodeBrix.Python`):

    public interface IPyObjectEncoder                  // CLR -> Python
        bool      CanEncode(Type type);
        PyObject? TryEncode(object value);

    public interface IPyObjectDecoder                  // Python -> CLR
        bool CanDecode(PyType objectType, Type targetType);
        bool TryDecode<T>(PyObject pyObj, out T? value);

Registration (namespace `CodeBrix.Python`):

    public static class PyObjectConversions
        public static void RegisterEncoder(IPyObjectEncoder encoder)
        public static void RegisterDecoder(IPyObjectDecoder decoder)

Registration is process-wide and additive; the FIRST registered codec that
reports it can handle a conversion wins, so register the most specific codecs
first. Both methods throw `ArgumentNullException` on null. There is no public
unregister - scope codec registration to application startup.

Composites (namespace `CodeBrix.Python.Codecs`) let you build and pass around
ordered groups instead of registering one by one:

    public sealed class EncoderGroup
        : IPyObjectEncoder, IEnumerable<IPyObjectEncoder>, IDisposable
        public void Add(IPyObjectEncoder item)
        public void Clear()
        public bool CanEncode(Type type)
        public PyObject? TryEncode(object value)
        public IEnumerator<IPyObjectEncoder> GetEnumerator()
        public void Dispose()      // disposes IDisposable members

    public sealed class DecoderGroup
        : IPyObjectDecoder, IEnumerable<IPyObjectDecoder>, IDisposable
        public void Add(IPyObjectDecoder item)
        public void Clear()
        public bool CanDecode(PyType objectType, Type targetType)
        public bool TryDecode<T>(PyObject pyObj, out T? value)
        public IEnumerator<IPyObjectDecoder> GetEnumerator()
        public void Dispose()

Because both implement the single-codec interface, a group can be registered
exactly like one codec, and groups nest. Collection initializer syntax works:

    var group = new DecoderGroup { ListDecoder.Instance, new MyDecoder() };
    PyObjectConversions.RegisterDecoder(group);

Two extension methods flatten a codec-or-group uniformly:

    public static IPyObjectDecoder? DecoderGroupExtensions.GetDecoder(
        this IPyObjectDecoder decoder, PyType objectType, Type targetType)
    public static IEnumerable<IPyObjectEncoder>
        EncoderGroupExtensions.GetEncoders(this IPyObjectEncoder decoder,
                                           Type type)

Built-in codecs (namespace `CodeBrix.Python.Codecs`) - each is opt-in via its
static `Register()` and exposes a singleton `Instance`:

    public class ListDecoder : IPyObjectDecoder
        public static ListDecoder Instance { get; }
        public static void Register()
      -> decodes a Python `list` to `IList<T>` (a live view, not a copy).

    public class SequenceDecoder : IPyObjectDecoder
        public static SequenceDecoder Instance { get; }
        public static void Register()
      -> decodes any Python sequence to `ICollection<T>`.

    public class IterableDecoder : IPyObjectDecoder
        public static IterableDecoder Instance { get; }
        public static void Register()
      -> decodes any Python iterable to `IEnumerable<T>`.

    public sealed class TupleCodec<TTuple> : IPyObjectEncoder, IPyObjectDecoder
        public static TupleCodec<TTuple> Instance { get; }
        public static void Register()
      -> round-trips CLR tuples and Python tuples. Use
         `TupleCodec<ValueTuple>.Register()` for `ValueTuple<...>` and
         `TupleCodec<Tuple>.Register()` for the reference tuples.

    public sealed class EnumPyIntCodec : IPyObjectEncoder, IPyObjectDecoder
        public static EnumPyIntCodec Instance { get; }
      -> maps CLR enums to and from Python `int`.

    public class RawProxyEncoder : IPyObjectEncoder
        public PyObject TryEncode(object value)   // FromManagedObject(value)
        public virtual bool CanEncode(Type type)     // false; override it
      -> base class for "expose the CLR object as a raw proxy, skipping all
         conversions". It is deliberately subclassable FROM PYTHON: derive from
         `CodeBrix.Python.Codecs.RawProxyEncoder`, override `CanEncode`, and
         register the instance with `PyObjectConversions.RegisterEncoder`.

These decoders are lossless views, which is why they are deliberately narrow:
`ListDecoder` will not decode to `List<int>` (that would require copying).
Lossy conversions belong in your own codec.


EXPOSING .NET TO PYTHON
-----------------------
From the Python side, the embedded `clr` module is the door into the CLR. It is
always importable inside the embedded interpreter - no path setup required.

    import clr
    clr.AddReference("MyCompany.MyLibrary")     # by name, path, or full name
    from MyCompany.MyLibrary import Widget

    w = Widget()
    w.Name = "hello"
    print(w.Describe())

`clr` module functions (their C# implementations are internal, but these are
the Python-visible names and shapes):

    clr.AddReference(name)   -> Assembly; raises FileNotFoundException when the
                                assembly cannot be found. Accepts a simple name,
                                a full assembly name, or a file path.
    clr.FindAssembly(name)   -> str path, or None
    clr.ListAssemblies(verbose) -> list[str] of loaded assembly names
                                (full names when verbose is true)
    clr.GetClrType(t)        -> System.Type for a CLR type (like typeof in C#)
    clr.getPreload()         -> bool
    clr.setPreload(flag)     -> None; when preloading is on, all assemblies on
                                the path are scanned so their namespaces can be
                                imported without an explicit AddReference.

Two decorators are also injected into the `clr` module, for going the OTHER way
- exposing a Python class member to .NET with an explicit CLR signature:

    import clr
    from System import String, Int32

    class X(object):
        @clr.clrproperty(String)
        def test(self):
            return "x"

        @clr.clrmethod(Int32, [String])
        def compute(self, s):
            return len(s)

`clrproperty(type_)` takes the property type; `clrmethod(return_type,
arg_types, clrname=None)` takes the return type and a list of argument types,
plus an optional .NET-side name. A `clrproperty` without a setter raises
`AttributeError` on assignment.

Python classes can also DERIVE from .NET classes and implement .NET interfaces.
Set `__namespace__` on the Python class so the generated .NET type gets a
stable namespace:

    from MyCompany.MyLibrary import WidgetBase

    class PyWidget(WidgetBase):
        __namespace__ = "MyCompany.MyLibrary.Generated"

        def Describe(self):                 # overrides a virtual member
            return "described from Python"

The generated derived types are built and dispatched by these public types -
you do not call them yourself, but they are what a stack trace will show:

    public interface IPythonDerivedType          // marker on generated types
    public class PythonDerivedType
        public static T?   InvokeMethod<T>(IPythonDerivedType obj, ...)
        public static void InvokeMethodVoid(IPythonDerivedType obj, ...)
        public static T?   InvokeGetProperty<T>(IPythonDerivedType obj, string)
        public static void InvokeSetProperty<T>(IPythonDerivedType obj,
                                                string name, T value)
        public static void InvokeCtor(IPythonDerivedType obj, string, object[])
        public static void PyFinalize(IPythonDerivedType obj)

    public class Dispatcher                      // delegate-to-Python bridge
        public object? Dispatch(object?[] args)

Controlling what Python can see, from C#:

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Delegate
                    | AttributeTargets.Enum | AttributeTargets.Interface
                    | AttributeTargets.Struct | AttributeTargets.Assembly)]
    public class PyExportAttribute : Attribute
        public PyExportAttribute(bool export)

    [AttributeUsage(AttributeTargets.All)]
    public class DocStringAttribute : Attribute
        public DocStringAttribute(string docStr)
        public string DocString { get; }

`[PyExport(false)]` hides a public type (or an entire assembly) from Python;
`[DocString("...")]` supplies the `__doc__` string Python sees for a type or
member. Without `DocString`, a class with a constructor gets its constructor
signature as its docstring.

Changing what Python thinks the BASE types of a CLR type are is done with a
base-type provider, registered on the engine's interop configuration BEFORE the
affected CLR type is first materialized into Python:

    public interface IPythonBaseTypeProvider
        IEnumerable<PyType> GetBaseTypes(Type type,
                                        IList<PyType> existingBases);

    public sealed class DefaultBaseTypeProvider : IPythonBaseTypeProvider
        public static DefaultBaseTypeProvider Instance { get; }

    public sealed class InteropConfiguration : IDisposable
        public IList<IPythonBaseTypeProvider> PythonBaseTypeProviders { get; }
        public static InteropConfiguration MakeDefault()
        public void Dispose()

    PythonEngine.InteropConfiguration.PythonBaseTypeProviders.Add(myProvider);

`MakeDefault()` returns the standard chain: `DefaultBaseTypeProvider` plus the
collection and dynamic-object mixin providers that make CLR collections behave
like Python collections and `DynamicObject` instances behave like Python
objects. `DefaultBaseTypeProvider` requires the incoming `existingBases` list
to be empty and throws `ArgumentException` otherwise, so it must stay first.
`PythonEngine.Shutdown()` resets `InteropConfiguration` back to
`MakeDefault()` - re-register custom providers after every restart.


ERRORS
------
    public class PythonException : System.Exception
        public PythonException(PyType type, PyObject? value,
                               PyObject? traceback)
        public PythonException(PyType type, PyObject? value,
                               PyObject? traceback, Exception? innerException)
        public PythonException(PyType type, PyObject? value,
                               PyObject? traceback, string message,
                               Exception? innerException)
        public PyType   Type      { get; }    // e.g. Type.Name == "ValueError"
        public PyObject? Value    { get; }
        public PyObject? Traceback{ get; }
        public override string StackTrace { get; }  // Python + CLR frames
        public bool     IsNormalized { get; }
        public void     Normalize()
        public string   Format()      // like traceback.format_exception
        public PythonException Clone()
        public override void GetObjectData(SerializationInfo, StreamingContext)

Any Python-level error raised while executing Python code surfaces in C# as a
`PythonException`. Branch on `ex.Type.Name` (or, better, register a decoder so
a given Python exception type decodes into your own CLR exception type).
`Format()` produces exactly what the CPython console would print, including the
full traceback; it requires a running interpreter. `StackTrace` degrades
gracefully to "Python stack unavailable as runtime was shut down" once the
engine is gone.

Other exception types you may see:

    public class InternalPythonnetException : Exception
      -> an invariant inside the interop layer was violated; a bug report.

    public class FinalizationException : Exception
        public IntPtr   Handle { get; }
        public PyObject GetObject()
        public PyObject DebugGetObject()
      -> raised on the finalizer path when a Python object cannot be released.

    public class RuntimeShutdownException : FinalizationException
      -> a `PyObject` outlived the interpreter run that created it. This is the
         exception to expect when you re-initialize after `Shutdown()` and old
         wrappers are still alive; handle it in
         `Finalizer.Instance.ErrorHandler`
         (set `e.Handled = true`) if you deliberately allow such leaks.

`InvalidOperationException` is what you get for lifecycle misuse: using the
engine before `Initialize()`, setting `Runtime.PythonDLL` after it, replacing
`InteropConfiguration` while running, or releasing the GIL on the wrong thread
when `DebugGIL` is on.


OBJECT LIFETIME AND THE FINALIZER
---------------------------------
Undisposed `PyObject` wrappers are queued and released in batches, because the
release must happen under the GIL and the CLR finalizer thread does not hold
it.

    public class Finalizer
        public static Finalizer Instance { get; }
        public int  Threshold { get; set; }     // queued objects before a flush
        public bool Enable    { get; set; }     // default true
        public void Collect()                   // flush the queue now
        public event EventHandler<CollectArgs>? BeforeCollect;
        public event EventHandler<ErrorArgs>?   ErrorHandler;

        public class CollectArgs : EventArgs
            public int ObjectCount { get; set; }

        public class ErrorArgs : EventArgs
            public ErrorArgs(Exception error)
            public Exception Error   { get; }
            public bool      Handled { get; set; }

Set `Handled = true` in `ErrorHandler` to swallow a release failure (the
standard use is tolerating `RuntimeShutdownException` across an engine
restart). `Collect()` must be called from a thread that is allowed to run
Python code.


INTERPRETER STATE SERIALIZATION
-------------------------------
`RuntimeData` is the hook set used to stash and restore CLR-side interop state
across an interpreter restart. It is a niche facility; most applications never
touch it.

    public static class RuntimeData
        public readonly static Func<IFormatter> DefaultFormatterFactory
        public static Func<IFormatter> FormatterFactory { get; set; }
        public static Type? FormatterType { get; set; }  // an IFormatter type
        public static Action? PostStashHook { get; set; }
        public static Action? PreRestoreHook { get; set; }
        public static ICLRObjectStorer? WrappersStorer { get; set; }
        public static bool HasStashData()
        public static void ClearStash()
        public static void StashSerializationData(string key,
                                                  MemoryStream stream)
        public static MemoryStream GetSerializationData(string key)
        public static void FreeSerializationData(string key)

    public interface ICLRObjectStorer
        ICollection<CLRMappedItem> Store(CLRWrapperCollection wrappers,
                                         Dictionary<string, object?> storage);
        CLRWrapperCollection Restore(Dictionary<string, object?> storage);

    public class CLRMappedItem
        public CLRMappedItem(object instance)
        public object          Instance { get; }
        public List<PyObject>  PyRefs   { get; set; }
        public bool            Stored   { get; set; }

    public class CLRWrapperCollection : KeyedCollection<object, CLRMappedItem>
        public new bool TryGetValue(object key, out CLRMappedItem? value)

    public class NoopFormatter : IFormatter      // serializes nothing
    public sealed class JsonFormatter : IFormatter
                                   // in CodeBrix.Python.Serialization

The default factory probes whether the platform's binary formatter is usable
and silently falls back to `NoopFormatter` when it is not - which, on modern
.NET, it is not. Supply `FormatterFactory` (or `FormatterType`) with
`JsonFormatter` or your own `IFormatter` if you actually need state to survive.


REMAINING PUBLIC TYPES
----------------------
    public struct UnsafeReferenceWithRun     // [Obsolete], internal use only
    public static IntPtr PythonEngine.InitExt()  // [Obsolete], used when the
                                             // CLR is loaded BY CPython rather
                                             // than the other way round

Do not call either from application code.

Two more names turn up in a naive "public type" scan of the source but are NOT
reachable from a consuming assembly, because they are nested inside internal
types: `OperatorMethod.SlotDefinition` (the CIL-operator-to-Python-slot map)
and the `Interop` P/Invoke delegate family (B_N, BB_N, B_I32, ...). Ignore
them. Likewise `CodeBrix.Python.Native` and the other sub-namespaces listed
under KEY NAMESPACES / USINGS expose no public types at all - only
`CodeBrix.Python`, `CodeBrix.Python.Codecs` and
`CodeBrix.Python.Serialization` do.


COMPLETE EXAMPLES
=================

EXAMPLE A - initialize, import a stdlib module, call it, convert the result
--------------------------------------------------------------------------
    using System;
    using CodeBrix.Python;

    internal static class Program
    {
        private static void Main()
        {
            // Point the embedded interpreter at a CPython shared library.
            //   Windows: python3XX.dll
            //   macOS:   libpython3.XX.dylib
            //   Linux:   libpython3.XX.so.1.0
            // (XX is the CPython minor version installed on the machine.)
            // Skip this when PYTHONNET_PYDLL or a virtual environment is set.
            string libPython =
                Environment.GetEnvironmentVariable("MY_LIBPYTHON");
            if (!string.IsNullOrWhiteSpace(libPython))
            {
                Runtime.PythonDLL = libPython;
            }

            PythonEngine.Initialize();
            try
            {
                using (Py.GIL())
                {
                    Console.WriteLine("CPython " + PythonEngine.Version);

                    // Import a stdlib module and call a function on it.
                    using PyObject posixpath = Py.Import("posixpath");
                    using PyObject joined = posixpath.InvokeMethod(
                        "join",
                        "one".ToPython(),
                        "two".ToPython(),
                        "three.txt".ToPython());

                    // Convert the Python result to a CLR type.
                    string path = joined.As<string>();
                    Console.WriteLine(path);        // one/two/three.txt

                    // Same thing with dynamic, when readability matters more
                    // than the last few nanoseconds.
                    dynamic sys = Py.Import("sys");
                    Console.WriteLine((string)sys.platform);   // linux / win32
                }
            }
            finally
            {
                PythonEngine.Shutdown();
            }
        }
    }

EXAMPLE B - run a script FILE inside a scope and read variables back
--------------------------------------------------------------------
analysis.py, shipped beside the application:

    total = 0.0
    for s in samples:
        total += s
    mean = total / len(samples)
    if mean > 100:
        warning = "mean is unusually high for " + label

The C# host:

    using System;
    using System.IO;
    using System.Linq;
    using CodeBrix.Python;

    public static class ScriptHost
    {
        public static double RunAnalysis(string scriptPath, double[] samples)
        {
            using (Py.GIL())
            {
                // A scope is an isolated module namespace. Nothing set here
                // leaks into __main__ or into another scope.
                using PyModule scope = Py.CreateScope("analysis");

                using var pySamples = new PyList(
                    samples.Select(v => (PyObject)new PyFloat(v)).ToArray());

                scope.Set("samples", pySamples);
                scope.Set("label", "run-1");        // CLR value, auto-converted

                // Exec is fluent, so several statements can be chained.
                scope.Exec(File.ReadAllText(scriptPath));

                // Read results back out, strongly typed.
                double mean = scope.Get<double>("mean");

                // Optional variables: TryGet does not throw when absent.
                if (scope.TryGet<string>("warning", out string warning))
                {
                    Console.WriteLine("script warning: " + warning);
                }

                return mean;
            }
        }
    }

    // Re-running the same script many times? Compile it once instead:
    //     PyObject code = PythonEngine.Compile(source, scriptPath,
    //                                          RunFlagType.File);
    //     scope.Execute(code);

EXAMPLE C - a custom codec (CLR DateTime <-> Python datetime.datetime)
----------------------------------------------------------------------
    using System;
    using CodeBrix.Python;
    using CodeBrix.Python.Codecs;

    public sealed class DateTimeCodec
        : IPyObjectEncoder, IPyObjectDecoder, IDisposable
    {
        private readonly PyObject _pyDateTime;

        // Construct while holding the GIL - the constructor imports.
        public DateTimeCodec()
        {
            using PyObject module = Py.Import("datetime");
            _pyDateTime = module.GetAttr("datetime");
        }

        // ---- CLR -> Python -------------------------------------------
        public bool CanEncode(Type type) => type == typeof(DateTime);

        public PyObject TryEncode(object value)
        {
            var dt = (DateTime)value;
            return _pyDateTime.Invoke(
                dt.Year.ToPython(), dt.Month.ToPython(), dt.Day.ToPython(),
                dt.Hour.ToPython(), dt.Minute.ToPython(), dt.Second.ToPython());
        }

        // ---- Python -> CLR -------------------------------------------
        // Compare the SOURCE type by identity, never by name: two different
        // classes can share a __name__.
        public bool CanDecode(PyType objectType, Type targetType)
            => targetType == typeof(DateTime)
               && PythonReferenceComparer.Instance.Equals(
                      objectType, _pyDateTime);

        public bool TryDecode<T>(PyObject pyObj, out T value)
        {
            var dt = new DateTime(
                pyObj.GetAttr("year").As<int>(),
                pyObj.GetAttr("month").As<int>(),
                pyObj.GetAttr("day").As<int>(),
                pyObj.GetAttr("hour").As<int>(),
                pyObj.GetAttr("minute").As<int>(),
                pyObj.GetAttr("second").As<int>());
            value = (T)(object)dt;
            return true;
        }

        public void Dispose() => _pyDateTime.Dispose();
    }

Register it ONCE, at startup, before any DateTime crosses the boundary:

    PythonEngine.Initialize();
    using (Py.GIL())
    {
        var codec = new DateTimeCodec();
        PyObjectConversions.RegisterEncoder(codec);
        PyObjectConversions.RegisterDecoder(codec);

        // Built-in codecs are opt-in too:
        TupleCodec<ValueTuple>.Register();
        ListDecoder.Register();
    }

Grouping is equivalent and keeps ordering explicit:

    var decoders = new DecoderGroup
    {
        new DateTimeCodec(),      // most specific first
        ListDecoder.Instance,
        SequenceDecoder.Instance,
        IterableDecoder.Instance,
    };
    PyObjectConversions.RegisterDecoder(decoders);

EXAMPLE D - Python code calling (and subclassing) a .NET class
--------------------------------------------------------------
The .NET side - an ordinary library, no special base class needed:

    using CodeBrix.Python;

    namespace Contoso.Reporting;

    [DocString("A single line on a report.")]
    public class LineItem
    {
        public LineItem(string name, double amount)
        {
            Name = name;
            Amount = amount;
        }

        public string Name   { get; }
        public double Amount { get; }

        [DocString("Formats this item as one report row.")]
        public virtual string Render() => Name + ": " + Amount.ToString("0.00");
    }

    // Public in C#, but deliberately invisible from Python:
    [PyExport(false)]
    public class ReportInternals { }

The host, and the Python that uses it:

    PythonEngine.Initialize();
    try
    {
        using (Py.GIL())
        using (PyModule scope = Py.CreateScope("report"))
        {
            // A C# raw string literal ("""), so the Python keeps its own
            // indentation - a verbatim @"..." string would prefix every line
            // with the C# indentation and raise IndentationError.
            scope.Exec("""
                import clr
                clr.AddReference('Contoso.Reporting')   # assembly simple name
                from Contoso.Reporting import LineItem

                plain = LineItem('Widget', 9.99)
                plain_text = plain.Render()

                class DiscountedItem(LineItem):
                    # Gives the generated .NET type a stable namespace.
                    __namespace__ = 'Contoso.Reporting.Python'

                    def Render(self):
                        return 'DISCOUNTED ' + LineItem.Render(self)

                item = DiscountedItem('Widget', 9.99)
                text = item.Render()
                doc  = LineItem.Render.__doc__
                """);

            Console.WriteLine(scope.Get<string>("plain_text"));  // Widget: 9.99
            Console.WriteLine(scope.Get<string>("text"));
            Console.WriteLine(scope.Get<string>("doc"));

            // The Python subclass IS a LineItem on the .NET side, and calling
            // the virtual member from C# dispatches back into Python.
            using PyObject pyItem = scope.Get("item");
            LineItem managed = pyItem.As<LineItem>();
            Console.WriteLine(managed.Render());   // DISCOUNTED Widget: 9.99
        }
    }
    finally
    {
        PythonEngine.Shutdown();
    }

To expose a Python member to .NET with an explicit CLR signature, decorate it
on the Python side:

    import clr
    from System import String, Int32

    class Calculator(object):
        @clr.clrproperty(String)
        def Title(self):
            return "calc"

        @clr.clrmethod(Int32, [String])
        def Measure(self, text):
            return len(text)

    # From C#:  dynamic c = scope.Get("calc");  int n = (int)c.Measure("abcd");


MINIMUM VIABLE PROJECT
======================
MyPythonHost.csproj:

    <Project Sdk="Microsoft.NET.Sdk">
      <PropertyGroup>
        <OutputType>Exe</OutputType>
        <TargetFramework>net10.0</TargetFramework>
        <Nullable>disable</Nullable>
      </PropertyGroup>
      <ItemGroup>
        <PackageReference Include="CodeBrix.Python.MitLicenseForever" />
      </ItemGroup>
    </Project>

Program.cs:

    using System;
    using CodeBrix.Python;

    // Set PYTHONNET_PYDLL to the libpython path before running, or assign
    // Runtime.PythonDLL here.
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

Run:

    PYTHONNET_PYDLL=/path/to/libpython3.XX.so.1.0 dotnet run

That is the whole contract: reference the package, make libpython findable,
initialize once, do everything else under `Py.GIL()`, shut down once.


PERFORMANCE TIPS
================
  -> Hold the GIL once around a BATCH of Python work. Acquiring and releasing
     per call is the single most common cause of slow embedding code.

  -> Release the GIL around long-running managed work with
     `PythonEngine.BeginAllowThreads()` / `EndAllowThreads(ts)`, so other
     threads (and Python's own background threads) can run.

  -> Cache imported modules and callables in fields instead of re-importing.
     `Py.Import` goes through the full import machinery on first import and a
     `sys.modules` lookup afterwards, but the attribute lookups that follow are
     not free either. Dispose the cached objects in a shutdown handler.

  -> Compile once, execute many. `PythonEngine.Compile(code, filename, mode)`
     returns a reusable code object; `PyModule.Execute` / `Execute<T>` runs it
     without re-parsing. Re-`Exec`-ing the same source string in a loop
     re-compiles it every time.

  -> Prefer typed wrappers and `As<T>()` over `dynamic` in hot paths. `dynamic`
     goes through `DynamicObject` binders on every call site; `PyObject.Invoke`
     / `InvokeMethod` / `GetAttr` do not.

  -> Move bulk data through `PyBuffer`, not element by element. A loop of
     `GetItem(int)` / `SetItem(int, ...)` crosses the boundary once per element;
     `PyBuffer.Read` / `Write` / `ToContiguous` / `FromContiguous` move the
     whole block.

  -> Dispose `PyObject`s deterministically (`using`). Undisposed wrappers pile
     up in the `Finalizer` queue and are released in batches under the GIL;
     tune `Finalizer.Instance.Threshold`, or set
     `Finalizer.Instance.Enable = false` and call
     `Finalizer.Instance.Collect()` yourself at known-quiet points.

  -> Codec selection is cached, so register codecs at startup. Encoder lookups
     are cached per CLR type and decoder lookups per (Python type, CLR type)
     pair, from the FIRST conversion onward - a codec registered after that
     pair has already been converted will not be consulted.

  -> Register the most specific codecs first: the first codec that reports it
     can handle a conversion wins.

  -> Initialize the engine once per process and keep it alive. Startup costs
     interpreter creation plus loading the embedded `clr` support modules, and
     `Shutdown()` cannot be cleanly undone.

  -> Keep CPU-bound parallelism on the .NET side with the GIL released, or
     inside native Python extensions that release it themselves. Multiple .NET
     threads calling Python do not run Python in parallel.


COMMON PITFALLS TO AVOID
========================
 1. Forgetting the GIL. Every Python touch - including `PyObject.Dispose()`,
    `ToString()` and `Repr()` - needs `using (Py.GIL())`. Symptoms range from
    wrong results to a hard process crash.

 2. Dropping the `using` on `Py.GIL()`. `Py.GILState`'s finalizer THROWS
    `InvalidOperationException` when it is collected undisposed, so the failure
    surfaces far away from the cause. Always `using`.

 3. Releasing the GIL on a different thread than the one that acquired it. Set
    `PythonEngine.DebugGIL = true` during development to turn this into an
    immediate, precise exception.

 4. `await` inside a `using (Py.GIL())` block. The continuation may resume on
    another thread and release a lock it does not own. Split the method so each
    GIL block is fully synchronous.

 5. Setting `Runtime.PythonDLL` too late. It must be assigned BEFORE
    `PythonEngine.Initialize()`; afterwards the setter throws
    `InvalidOperationException`. The same applies to `PythonEngine.PythonHome`,
    `PythonPath`, `ProgramName` and `SetNoSiteFlag()`.

 6. Expecting libpython to be found automatically. Discovery only covers
    `PYTHONNET_PYDLL` and virtual environments named by `PYTHONNET_VENV` /
    `VIRTUAL_ENV` (which must contain a `pyvenv.cfg` with `home` and `version`
    keys). Outside those cases you MUST set `Runtime.PythonDLL`. A static-only
    or bitness-mismatched CPython cannot be loaded at all.

 7. Assuming `Shutdown()` can be undone. Its own contract is that "the Python
    runtime can no longer be used in the current process after calling the
    Shutdown method". Do not build a restart loop around it; keep one engine
    for the process lifetime. `Shutdown()` also resets
    `PythonEngine.InteropConfiguration` to the default, discarding any custom
    base-type providers.

 8. Letting `PyObject`s outlive the interpreter. Wrappers still alive at
    shutdown (or across a restart attempt) raise `RuntimeShutdownException` on
    the finalizer path. Dispose them, or subscribe
    `Finalizer.Instance.ErrorHandler` and set `e.Handled = true` for that
    exception type.

 9. Using a disposed `PyObject`. It throws `ObjectDisposedException`. Note that
    `PyObject.Dispose()` releases a REFERENCE, not the Python object - use
    `NewReference()` when you need an independently-owned wrapper.

10. Registering codecs too late. Encoder/decoder resolution is cached from the
    first conversion of a given type pair, so register everything at startup,
    before any Python code runs. There is no public unregister.

11. Comparing Python types by `Name`. `PyType.Name` is the raw `tp_name` and is
    not unique. Use `PythonReferenceComparer.Instance.Equals(...)` to compare
    Python objects by identity.

12. Confusing `==` with identity. `PyObject.operator ==` and `Equals` invoke
    the Python comparison protocol (value semantics). `PythonReferenceComparer`
    is the `is` operator.

13. Calling `PythonEngine.RunSimpleString` and expecting exceptions. It returns
    -1 and prints the traceback to stderr instead of throwing. Use `Exec` /
    `Eval` when you need a `PythonException`.

14. Using `PythonEngine.RunString`. It is `[Obsolete]` and slated for removal;
    use `Exec`, `Eval` or `RunSimpleString`.

15. Reading `Exec` results from the wrong dictionary. With `globals: null` the
    call falls back to the current frame's globals, or to a throwaway
    dictionary when there is no frame - either way you have no handle on where
    the results landed. Pass your own `PyDict` (or use a `PyModule` scope) when
    you want to read variables afterwards.

16. Forgetting `__namespace__` on a Python class that derives from a .NET type.
    Without it the generated .NET type lands in an unpredictable namespace, and
    re-defining the class (for example in a test) can collide.

17. Registering an `IPythonBaseTypeProvider` after the affected CLR type has
    already been materialized into Python. Providers must be added to
    `PythonEngine.InteropConfiguration.PythonBaseTypeProviders` before the type
    is first seen from Python. Also, `DefaultBaseTypeProvider` throws unless it
    is given an empty `existingBases` list, so it must stay first in the chain.

18. Expecting `RuntimeData` state serialization to work out of the box. The
    default formatter factory probes the platform binary formatter and silently
    falls back to `NoopFormatter`, which serializes nothing. Supply
    `JsonFormatter` or your own `IFormatter` through `FormatterFactory`.

19. Referencing an upstream pythonnet package alongside this one, or writing
    `using Python.Runtime;`. The namespaces here are `CodeBrix.Python[.X]`, and
    two interop assemblies cannot share one interpreter.

20. Assuming `[PyExport(false)]` hides a type from .NET. It only hides it from
    Python; the type remains public to CLR callers.


WHAT THIS PACKAGE DOES NOT DO
=============================
  -> It does NOT ship, install, or download a CPython runtime. libpython must
     already exist on the machine.

  -> It does NOT ship Python packages. numpy, pandas, pytest and anything else
     your Python code imports must be installed into the CPython installation
     or virtual environment that the embedded interpreter uses.

  -> It is NOT a reimplementation of Python on the CLR. There is no Python
     compiler here; the package drives a real CPython interpreter through its C
     API, so C extension modules work exactly as they do outside .NET.

  -> It does NOT give you more than one interpreter per process.
     `PythonEngine`, `Runtime`, `PyObjectConversions` and `Finalizer` are all
     process-global statics, and `Shutdown()` is documented as final for the
     process. Sub-interpreters are not exposed.

  -> It does NOT make Python run in parallel. The GIL serializes execution;
     `BeginAllowThreads` yields the lock but does not remove it.

  -> It does NOT bridge `async`/`await` to Python coroutines or `asyncio`. Run
     the event loop from Python code.

  -> It does NOT support AppDomain unload/reload scenarios; those are a .NET
     Framework concept and are out of scope for a .NET 10-only library.

  -> It does NOT provide a Python-package (wheel) distribution of the CLR
     loader. This package is the .NET-side embedding library only.

  -> It does NOT provide working binary state serialization on modern .NET. The
     formatter-based `RuntimeData` surface is retained for API parity and falls
     back to a no-op formatter unless you supply your own `IFormatter`.

  -> The assembly is NOT strong-named.


WORKING EXAMPLES ON GITHUB
==========================
The repository's test suites are the executable specification for everything
above. Browse them at:

  https://github.com/ellisnet/CodeBrix.Python/tree/main/tests

In the list below, `...` stands for
https://github.com/ellisnet/CodeBrix.Python

C# embedding tests (tests/CodeBrix.Python.Tests/):

  Engine lifecycle and properties
    .../blob/main/tests/CodeBrix.Python.Tests/TestPythonEngineProperties.cs
  Exec / Eval / RunSimpleString
    .../blob/main/tests/CodeBrix.Python.Tests/pyrunstring.cs
  Scopes, PyModule Set/Get/Exec/Eval/Compile+Execute
    .../blob/main/tests/CodeBrix.Python.Tests/Modules.cs
  Importing, including import failure modes
    .../blob/main/tests/CodeBrix.Python.Tests/pyimport.cs
    .../blob/main/tests/CodeBrix.Python.Tests/MissingPythonPackage.cs
  dynamic dispatch and operators
    .../blob/main/tests/CodeBrix.Python.Tests/Dynamic.cs
    .../blob/main/tests/CodeBrix.Python.Tests/TestOperator.cs
  Keyword arguments (Py.kw) and named arguments
    .../blob/main/tests/CodeBrix.Python.Tests/TestNamedArguments.cs
  Python `with` blocks from C# (Py.With)
    .../blob/main/tests/CodeBrix.Python.Tests/TestPyWith.cs
  PyObject surface
    .../blob/main/tests/CodeBrix.Python.Tests/TestPyObject.cs
  Typed wrappers
    .../blob/main/tests/CodeBrix.Python.Tests/TestPyInt.cs
    .../blob/main/tests/CodeBrix.Python.Tests/TestPyFloat.cs
    .../blob/main/tests/CodeBrix.Python.Tests/TestPyNumber.cs
    .../blob/main/tests/CodeBrix.Python.Tests/TestPyString.cs
    .../blob/main/tests/CodeBrix.Python.Tests/TestPyList.cs
    .../blob/main/tests/CodeBrix.Python.Tests/TestPyTuple.cs
    .../blob/main/tests/CodeBrix.Python.Tests/TestPySequence.cs
    .../blob/main/tests/CodeBrix.Python.Tests/TestPyIter.cs
    .../blob/main/tests/CodeBrix.Python.Tests/TestPyType.cs
  Buffer protocol (PyBuffer / PyBUF)
    .../blob/main/tests/CodeBrix.Python.Tests/TestPyBuffer.cs
  Codecs, codec groups and conversion
    .../blob/main/tests/CodeBrix.Python.Tests/Codecs.cs
    .../blob/main/tests/CodeBrix.Python.Tests/CodecGroups.cs
    .../blob/main/tests/CodeBrix.Python.Tests/TestConverter.cs
    .../blob/main/tests/CodeBrix.Python.Tests/TestCustomMarshal.cs
  Base-type providers and Python-derived .NET types
    .../blob/main/tests/CodeBrix.Python.Tests/Inheritance.cs
    .../blob/main/tests/CodeBrix.Python.Tests/TestInstanceWrapping.cs
    .../blob/main/tests/CodeBrix.Python.Tests/ClassManagerTests.cs
    .../blob/main/tests/CodeBrix.Python.Tests/ExtensionTypes.cs
  Delegates, callbacks and events
    .../blob/main/tests/CodeBrix.Python.Tests/TestCallbacks.cs
    .../blob/main/tests/CodeBrix.Python.Tests/CallableObject.cs
    .../blob/main/tests/CodeBrix.Python.Tests/Events.cs
  Exceptions
    .../blob/main/tests/CodeBrix.Python.Tests/TestPythonException.cs
  GIL, interrupts, finalizer and references
    .../blob/main/tests/CodeBrix.Python.Tests/TestGILState.cs
    .../blob/main/tests/CodeBrix.Python.Tests/TestInterrupt.cs
    .../blob/main/tests/CodeBrix.Python.Tests/TestFinalizer.cs
    .../blob/main/tests/CodeBrix.Python.Tests/References.cs
  Introspection and numpy interop
    .../blob/main/tests/CodeBrix.Python.Tests/Inspect.cs
    .../blob/main/tests/CodeBrix.Python.Tests/NumPyTests.cs
  State serialization
    .../blob/main/tests/CodeBrix.Python.Tests/Serialization/JsonFormatterTests.cs
    .../blob/main/tests/CodeBrix.Python.Tests/StateSerialization/MethodSerialization.cs
  Locating libpython on each OS (a copyable bootstrap helper)
    .../blob/main/tests/CodeBrix.Python.Tests/PlatformPythonDll.cs

.NET types written to be consumed FROM Python
(tests/CodeBrix.Python.TestSupport/) - the best models for your own
Python-facing .NET API, including `[DocString]` and subclassable types:

  https://github.com/ellisnet/CodeBrix.Python/tree/main/tests/CodeBrix.Python.TestSupport

Python-side examples (tests/CodeBrix.Python.PythonTests/pytests/) - `clr`
usage, `clr.clrmethod` / `clr.clrproperty`, subclassing .NET types, docstrings,
codecs, generics, indexers and events, all written in Python:

  https://github.com/ellisnet/CodeBrix.Python/tree/main/tests/CodeBrix.Python.PythonTests/pytests


QUICK REFERENCE CARD
====================
    Package ...... CodeBrix.Python.MitLicenseForever   (MIT, .NET 10+)
    Usings ....... using CodeBrix.Python;
                   using CodeBrix.Python.Codecs;       (custom codecs only)

    SETUP (all BEFORE Initialize)
      Runtime.PythonDLL = "<path to libpython>";
      PythonEngine.PythonHome / PythonPath / ProgramName = "...";
      PythonEngine.SetNoSiteFlag();
      PythonEngine.DebugGIL = true;                    // dev only
      env: PYTHONNET_PYDLL, PYTHONNET_PYEXE, PYTHONNET_VENV, VIRTUAL_ENV

    LIFECYCLE
      PythonEngine.Initialize();
      PythonEngine.Initialize(setSysArgv: true, initSigs: false);
      PythonEngine.Initialize(args, setSysArgv, initSigs);
      PythonEngine.IsInitialized
      PythonEngine.AddShutdownHandler(handler);
      PythonEngine.Shutdown();                         // final for the process

    GIL
      using (Py.GIL()) { ... }
      IntPtr ts = PythonEngine.BeginAllowThreads();
      PythonEngine.EndAllowThreads(ts);

    RUN CODE
      PythonEngine.Exec(code, globals, locals);
      PyObject v = PythonEngine.Eval(expr, globals, locals);
      int rc     = PythonEngine.RunSimpleString(code);  // 0 ok, -1 error
      PyObject c = PythonEngine.Compile(code, file, RunFlagType.File);

    SCOPES
      using PyModule s = Py.CreateScope("name");
      s.Set("x", 42).Exec("y = x + 1");
      int y = s.Get<int>("y");
      s.TryGet<string>("maybe", out string m);
      s.Import("json", "j"); s.ImportAll(otherModule);

    IMPORT / CALL / CONVERT
      PyObject m   = Py.Import("json");
      PyObject r   = m.InvokeMethod("dumps", value.ToPython());
      PyObject r2  = fn.Invoke(new[] { a, b }, Py.kw("key", v));
      string s     = r.As<string>();
      object o     = r.AsManagedObject(typeof(int));
      PyObject p   = clrValue.ToPython();

    OBJECT PROTOCOL
      GetAttr/SetAttr/HasAttr/DelAttr, GetItem/SetItem/DelItem, this[...]
      Length(), Dir(), Repr(), GetIterator(), IsCallable(), IsIterable(),
      IsInstance(t), IsSubclass(t), IsTrue(), IsNone(), GetPythonType()

    TYPES
      PyInt PyFloat PyString PyList PyTuple PyDict PySequence PyIterable
      PyIter PyNumber PyType PyModule PyBuffer

    CODECS
      PyObjectConversions.RegisterEncoder(enc);   // IPyObjectEncoder
      PyObjectConversions.RegisterDecoder(dec);   // IPyObjectDecoder
      TupleCodec<ValueTuple>.Register(); ListDecoder.Register();
      SequenceDecoder.Register(); IterableDecoder.Register();
      EnumPyIntCodec.Instance; RawProxyEncoder; EncoderGroup; DecoderGroup

    .NET -> PYTHON
      [PyExport(false)] hides a type;  [DocString("...")] sets __doc__
      Python: import clr; clr.AddReference("Asm"); from Ns import Type
      Python: class D(BaseNetType): __namespace__ = "Ns.Generated"
      Python: @clr.clrproperty(T) / @clr.clrmethod(TRet, [TArgs])

    ERRORS AND LIFETIME
      catch (PythonException ex) { ex.Type.Name; ex.Value; ex.Format(); }
      Finalizer.Instance.Threshold / Enable / Collect() / ErrorHandler
      RuntimeShutdownException = object outlived its interpreter run

    GOLDEN RULES
      1. Initialize once, shut down once, per process.
      2. Everything Python happens inside using (Py.GIL()).
      3. Acquire and release the GIL on the same thread; never await inside.
      4. Dispose PyObjects.
      5. Configure (PythonDLL, PythonHome, codecs, base-type providers) at
         startup - late configuration is silently ignored or throws.
================================================================================
