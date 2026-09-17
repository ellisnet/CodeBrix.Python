using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading;

using CodeBrix.Python.Native;

namespace CodeBrix.Python; //was previously: Python.Runtime;

/// <summary>
/// This class provides the public interface of the Python runtime.
/// </summary>
public class PythonEngine : IDisposable
{
    private static DelegateManager? delegateManager;
    // volatile: read from worker threads, written from Initialize/Shutdown.
    private static volatile bool initialized;
    private static IntPtr _pythonHome = IntPtr.Zero;
    private static IntPtr _programName = IntPtr.Zero;
    private static IntPtr _pythonPath = IntPtr.Zero;
    private static InteropConfiguration interopConfiguration = InteropConfiguration.MakeDefault();

    public PythonEngine()
    {
        Initialize();
    }

    public PythonEngine(params string[] args)
    {
        Initialize(args);
    }

    public PythonEngine(IEnumerable<string> args)
    {
        Initialize(args);
    }

    public void Dispose()
    {
        Shutdown();
    }

    public static bool IsInitialized
    {
        get { return initialized; }
    }

    private static void EnsureInitialized()
    {
        if (!IsInitialized)
            throw new InvalidOperationException(
                "Python must be initialized for this operation"
            );
    }

    /// <summary>Set to <c>true</c> to enable GIL debugging assistance.</summary>
    public static bool DebugGIL { get; set; } = false;

    internal static DelegateManager DelegateManager
    {
        get
        {
            if (delegateManager == null)
            {
                throw new InvalidOperationException(
                    "DelegateManager has not yet been initialized using CodeBrix.Python.PythonEngine.Initialize().");
            }
            return delegateManager;
        }
    }

    public static InteropConfiguration InteropConfiguration
    {
        get => interopConfiguration;
        set
        {
            if (IsInitialized)
                throw new NotSupportedException("Changing interop configuration when engine is running is not supported");

            interopConfiguration = value ?? throw new ArgumentNullException(nameof(InteropConfiguration));
        }
    }

    public static string ProgramName
    {
        get
        {
            IntPtr p = Runtime.TryUsingDll(() => Runtime.Py_GetProgramName());
            return UcsMarshaler.PtrToString(p) ?? "";
        }
        set
        {
            Marshal.FreeHGlobal(_programName);
            _programName = Runtime.TryUsingDll(
                () => UcsMarshaler.StringToPtr(value)
            );
            Runtime.Py_SetProgramName(_programName);
        }
    }

    /// <summary>
    /// The Python virtual environment the interpreter runs in, or <c>null</c> when none is
    /// configured. Assigning it is the code-level equivalent of the PYTHONNET_VENV environment
    /// variable, and takes precedence over PYTHONNET_VENV, VIRTUAL_ENV and PYTHONNET_PYEXE.
    /// <para>
    /// Assigning a path points the interpreter at that environment's launcher
    /// (<c>bin/python</c>, or <c>Scripts\python.exe</c> on Windows), which is what makes CPython
    /// read the environment's <c>pyvenv.cfg</c> at startup, report the environment as
    /// <c>sys.prefix</c> and put its <c>site-packages</c> on <c>sys.path</c>. When
    /// <see cref="Runtime.PythonDLL"/> has not been named outright, libpython is resolved from
    /// the base installation that the <c>pyvenv.cfg</c> <c>home</c> and <c>version</c> keys
    /// point at; a libpython that WAS named outright is kept, because a virtual environment
    /// never carries one of its own.
    /// </para>
    /// <para>
    /// Reading it reports the environment in effect whether it was assigned here or discovered
    /// from PYTHONNET_VENV or VIRTUAL_ENV.
    /// </para>
    /// </summary>
    /// <value>
    /// The virtual environment's root folder: the full path when it was assigned here, and the
    /// value of the environment variable when it was discovered from one.
    /// </value>
    /// <exception cref="ArgumentNullException">The assigned value is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    /// The assigned folder does not exist, holds no <c>pyvenv.cfg</c>, or that file has no
    /// <c>home</c> key naming the base interpreter. The message names the path and what was missing.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The runtime has already been initialized. Like <see cref="Runtime.PythonDLL"/> and
    /// <see cref="ProgramName"/>, this must be assigned before <see cref="Initialize()"/> runs.
    /// </exception>
    public static string? VirtualEnvironment
    {
        get => Runtime.PythonEnvironment.VenvPath;
        set
        {
            if (value is null)
            {
                throw new ArgumentNullException(nameof(VirtualEnvironment));
            }

            // Argument validation first, so a malformed path is reported as such whatever the
            // engine's state, and the caller never has to guess which of the two went wrong.
            PythonEnvironment configured =
                PythonEnvironment.FromVenvOrThrow(value, nameof(VirtualEnvironment));

            if (Runtime.IsInitialized)
            {
                throw new InvalidOperationException("This property must be set before runtime is initialized");
            }

            PythonEnvironment current = Runtime.PythonEnvironment;
            if (current.LibPythonIsExplicit)
            {
                configured.LibPython = current.LibPython;
                configured.LibPythonIsExplicit = true;
            }

            Runtime.PythonEnvironment = configured;
        }
    }

    public static string PythonHome
    {
        get
        {
            EnsureInitialized();
            IntPtr p = Runtime.TryUsingDll(() => Runtime.Py_GetPythonHome());
            return UcsMarshaler.PtrToString(p) ?? "";
        }
        set
        {
            // this value is null in the beginning
            Marshal.FreeHGlobal(_pythonHome);
            _pythonHome = UcsMarshaler.StringToPtr(value);
            Runtime.TryUsingDll(() => Runtime.Py_SetPythonHome(_pythonHome));
        }
    }

    public static string PythonPath
    {
        get
        {
            IntPtr p = Runtime.TryUsingDll(() => Runtime.Py_GetPath());
            return UcsMarshaler.PtrToString(p) ?? "";
        }
        set
        {
            Marshal.FreeHGlobal(_pythonPath);
            _pythonPath = Runtime.TryUsingDll(
                () => UcsMarshaler.StringToPtr(value)
            );
            Runtime.Py_SetPath(_pythonPath);
        }
    }

    // Min is 3.10, not upstream-master's nominal value: the floor is set by the
    // Native/TypeOffset3XX.cs tables that ABI.Initialize resolves by reflection, and
    // TypeOffset310.cs is the lowest one present. (Upstream master reports 3.10 but
    // deleted its TypeOffset310.cs when regenerating it as TypeOffset315.cs, so it
    // cannot actually serve 3.10; this port keeps that table and so genuinely can.)
    public static Version MinSupportedVersion => new(3, 10);
    public static Version MaxSupportedVersion => new(3, 15, int.MaxValue, int.MaxValue);
    public static bool IsSupportedVersion(Version version) => version >= MinSupportedVersion && version <= MaxSupportedVersion;

    public static string Version
    {
        get { return Marshal.PtrToStringAnsi(Runtime.Py_GetVersion()); }
    }

    public static string BuildInfo
    {
        get { return Marshal.PtrToStringAnsi(Runtime.Py_GetBuildInfo()); }
    }

    public static string Platform
    {
        get { return Marshal.PtrToStringAnsi(Runtime.Py_GetPlatform()); }
    }

    public static string Copyright
    {
        get { return Marshal.PtrToStringAnsi(Runtime.Py_GetCopyright()); }
    }

    public static string Compiler
    {
        get { return Marshal.PtrToStringAnsi(Runtime.Py_GetCompiler()); }
    }

    /// <summary>
    /// Set the NoSiteFlag to disable loading the site module.
    /// Must be called before Initialize.
    /// https://docs.python.org/3/c-api/init.html#c.Py_NoSiteFlag
    /// </summary>
    public static void SetNoSiteFlag()
    {
        Runtime.SetNoSiteFlag();
    }

    public static int RunSimpleString(string code)
    {
        return Runtime.PyRun_SimpleString(code);
    }

    public static void Initialize()
    {
        Initialize(setSysArgv: true);
    }

    public static void Initialize(bool setSysArgv = true, bool initSigs = false)
    {
        Initialize(Enumerable.Empty<string>(), setSysArgv: setSysArgv, initSigs: initSigs);
    }

    /// <summary>
    /// Initialize Method
    /// </summary>
    /// <remarks>
    /// Initialize the Python runtime. It is safe to call this method
    /// more than once, though initialization will only happen on the
    /// first call. It is *not* necessary to hold the Python global
    /// interpreter lock (GIL) to call this method.
    /// initSigs can be set to 1 to do default python signal configuration. This will override the way signals are handled by the application.
    /// </remarks>
    public static void Initialize(IEnumerable<string> args, bool setSysArgv = true, bool initSigs = false)
    {
        if (initialized)
        {
            return;
        }
        // Creating the delegateManager MUST happen before Runtime.Initialize
        // is called. If it happens afterwards, DelegateManager's CodeGenerator
        // throws an exception in its ctor.  This exception is eaten somehow
        // during an initial "import clr", and the world ends shortly thereafter.
        // This is probably masking some bad mojo happening somewhere in Runtime.Initialize().
        delegateManager = new DelegateManager();
        Runtime.Initialize(initSigs);
        initialized = true;
        Exceptions.Clear();

        // Make sure we clean up properly on app domain unload.
        AppDomain.CurrentDomain.DomainUnload += OnDomainUnload;
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;

        if (setSysArgv)
        {
            Py.SetArgv(args);
        }

        // Load the clr.py resource into the clr module
        BorrowedReference clr_dict = Runtime.PyModule_GetDict(ImportHook.ClrModuleReference);

        var locals = new PyDict();
        try
        {
            BorrowedReference module = DefineModule("clr._extras");
            BorrowedReference module_globals = Runtime.PyModule_GetDict(module);

            Assembly assembly = Assembly.GetExecutingAssembly();
            // add the contents of clr.py to the module
            string clr_py = assembly.ReadStringResource("clr.py");

            Exec(clr_py, module_globals, locals.Reference);

            LoadSubmodule(module_globals, "clr.interop", "interop.py");

            LoadMixins(module_globals);

            // add the imported module to the clr module, and copy the API functions
            // and decorators into the main clr module.
            Runtime.PyDict_SetItemString(clr_dict, "_extras", module);

            // append version
            var version = typeof(PythonEngine)
                .Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                .InformationalVersion;
            using var versionObj = Runtime.PyString_FromString(version);
            Runtime.PyDict_SetItemString(clr_dict, "__version__", versionObj.Borrow());

            using var keys = locals.Keys();
            foreach (PyObject key in keys)
            {
                if (!key.ToString()!.StartsWith("_"))
                {
                    using PyObject value = locals[key];
                    Runtime.PyDict_SetItem(clr_dict, key.Reference, value.Reference);
                }
                key.Dispose();
            }
        }
        finally
        {
            locals.Dispose();
        }

        ImportHook.UpdateCLRModuleDict();
    }

    static BorrowedReference DefineModule(string name)
    {
        var module = Runtime.PyImport_AddModule(name);
        var module_globals = Runtime.PyModule_GetDict(module);
        var builtins = Runtime.PyEval_GetBuiltins();
        Runtime.PyDict_SetItemString(module_globals, "__builtins__", builtins);
        return module;
    }

    static void LoadSubmodule(BorrowedReference targetModuleDict, string fullName, string resourceName)
    {
        string? memberName = fullName.AfterLast('.');
        Debug.Assert(memberName != null);

        var module = DefineModule(fullName);
        var module_globals = Runtime.PyModule_GetDict(module);

        Assembly assembly = Assembly.GetExecutingAssembly();
        string pyCode = assembly.ReadStringResource(resourceName);
        Exec(pyCode, module_globals, module_globals);

        Runtime.PyDict_SetItemString(targetModuleDict, memberName!, module);
    }

    static void LoadMixins(BorrowedReference targetModuleDict)
    {
        foreach (string nested in new[] { "collections", "dlr" })
        {
            LoadSubmodule(targetModuleDict,
                fullName: "clr._extras." + nested,
                resourceName: typeof(PythonEngine).Namespace + ".Mixins." + nested + ".py");
        }
    }

    static void OnDomainUnload(object _, EventArgs __)
    {
        Shutdown();
    }

    // volatile: assigned by the application from any thread and read by the process-exit handler
    // on the CLR's own shutdown thread. The enum's underlying type is int, so volatile applies.
    private static volatile ProcessExitShutdownMode _processExitShutdown = ProcessExitShutdownMode.Wait;

    // TimeSpan cannot be volatile, so the tick count is the field and Interlocked is the barrier.
    private static long _processExitShutdownTimeoutTicks = TimeSpan.FromSeconds(5).Ticks;

    /// <summary>
    /// What the process-exit handler does about the interpreter when the process ends and
    /// <see cref="Shutdown"/> was never called. The default,
    /// <see cref="ProcessExitShutdownMode.Wait"/>, shuts the interpreter down on the thread
    /// raising <see cref="AppDomain.ProcessExit"/> and waits for it indefinitely.
    /// <para>
    /// That wait can never finish when the thread that called <see cref="Initialize()"/> still
    /// holds the Global Interpreter Lock: <c>Py_InitializeEx</c> acquires the GIL and nothing
    /// releases it, the CLR raises <see cref="AppDomain.ProcessExit"/> on a different thread while
    /// the initializing thread is parked waiting for the exit handlers, and shutting the
    /// interpreter down needs that same GIL. Nothing times out a
    /// <see cref="AppDomain.ProcessExit"/> handler, so the process hangs.
    /// <see cref="ProcessExitShutdownMode.WaitWithTimeout"/> and
    /// <see cref="ProcessExitShutdownMode.Skip"/> are the opt-in ways out; owning the lifetime and
    /// calling <see cref="Shutdown"/> exactly once is the way that needs no opt-in at all, because
    /// it unsubscribes the handler.
    /// </para>
    /// <para>
    /// May be assigned before or after <see cref="Initialize()"/>: the handler reads it when the
    /// process exits, not when the engine starts.
    /// </para>
    /// </summary>
    /// <value>
    /// The mode in effect; <see cref="ProcessExitShutdownMode.Wait"/> until something assigns
    /// another one.
    /// </value>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a declared mode.</exception>
    public static ProcessExitShutdownMode ProcessExitShutdown
    {
        get => _processExitShutdown;
        set
        {
            if (!Enum.IsDefined<ProcessExitShutdownMode>(value))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(ProcessExitShutdown), value,
                    $"'{value}' is not a declared {nameof(ProcessExitShutdownMode)}.");
            }

            _processExitShutdown = value;
        }
    }

    /// <summary>
    /// How long the process-exit handler waits for the interpreter to shut down when
    /// <see cref="ProcessExitShutdown"/> is <see cref="ProcessExitShutdownMode.WaitWithTimeout"/>.
    /// Ignored by the other modes. The default is five seconds, which is long enough for a
    /// shutdown that can run at all and short enough not to be mistaken for a hang.
    /// <para>
    /// Time spent here is time the process spends not exiting, so prefer a small value: when the
    /// GIL is free the shutdown takes milliseconds, and when it is not, no amount of waiting will
    /// help.
    /// </para>
    /// </summary>
    /// <value>The bounded wait; five seconds until something assigns another value.</value>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The value is zero or negative, or longer than <see cref="int.MaxValue"/> milliseconds
    /// (roughly 24 days), which is the longest wait the underlying thread join accepts.
    /// </exception>
    public static TimeSpan ProcessExitShutdownTimeout
    {
        get => TimeSpan.FromTicks(Interlocked.Read(ref _processExitShutdownTimeoutTicks));
        set
        {
            if (value <= TimeSpan.Zero || value.TotalMilliseconds > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(ProcessExitShutdownTimeout), value,
                    "The timeout must be greater than zero and no longer than int.MaxValue milliseconds.");
            }

            Interlocked.Exchange(ref _processExitShutdownTimeoutTicks, value.Ticks);
        }
    }

    static void OnProcessExit(object _, EventArgs __)
    {
        switch (_processExitShutdown)
        {
            case ProcessExitShutdownMode.Skip:
                // Opt-in: the application owns the interpreter's end of life, or accepts that it
                // never gets one. Nothing to do; the process is on its way out.
                return;

            case ProcessExitShutdownMode.WaitWithTimeout:
                // Opt-in: the same shutdown, on a thread that can be abandoned when the GIL never
                // comes free, so a deadlocked shutdown cannot stop the process from exiting.
                RunBoundedProcessExitShutdown(ProcessExitShutdownTimeout);
                return;

            default:
                // The default, and the behaviour this library has always had.
                Runtime.ProcessIsTerminating = true;
                Shutdown();
                return;
        }
    }

    /// <summary>
    /// Runs the shutdown on a background thread and waits at most <paramref name="timeout"/> for
    /// it. A shutdown that finished is reported exactly as the blocking path would report it,
    /// including its exception; one that did not is abandoned, leaving the interpreter
    /// unfinalized in a process that is ending regardless.
    /// </summary>
    /// <param name="timeout">The longest the exiting process should wait.</param>
    static void RunBoundedProcessExitShutdown(TimeSpan timeout)
    {
        ExceptionDispatchInfo? failure = null;

        var worker = new Thread(() =>
        {
            try
            {
                Runtime.ProcessIsTerminating = true;
                Shutdown();
            }
            catch (Exception error)
            {
                // Rethrown below on the exiting thread when the shutdown finished in time. An
                // escaping exception on this thread would tear the process down differently from
                // the blocking path, which is the one behaviour this mode must not change.
                failure = ExceptionDispatchInfo.Capture(error);
            }
        })
        {
            // Background, so an abandoned shutdown can never be what keeps the process alive.
            IsBackground = true,
            Name = "CodeBrix.Python process-exit shutdown",
        };

        worker.Start();

        if (!worker.Join(timeout))
        {
            return;
        }

        // Join() published the worker's writes to this thread.
        failure?.Throw();
    }

    /// <summary>
    /// A helper to perform initialization from the context of an active
    /// CPython interpreter process - this bootstraps the managed runtime
    /// when it is imported by the CLR extension module.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [Obsolete(Util.InternalUseOnly)]
    public static IntPtr InitExt()
    {
        try
        {
            if (Runtime.IsInitialized)
            {
                var builtins = Runtime.PyEval_GetBuiltins();
                var runtimeError = Runtime.PyDict_GetItemString(builtins, "RuntimeError");
                Exceptions.SetError(runtimeError, "Python.NET runtime is already initialized");
                return IntPtr.Zero;
            }
            Runtime.HostedInPython = true;

            Initialize(setSysArgv: false);

            Finalizer.Instance.ErrorHandler += AllowLeaksDuringShutdown;
        }
        catch (PythonException e)
        {
            e.Restore();
            return IntPtr.Zero;
        }

        return CodeBrix.Python.ImportHook.GetCLRModule()
               .DangerousMoveToPointerOrNull();
    }

    private static void AllowLeaksDuringShutdown(object sender, Finalizer.ErrorArgs e)
    {
        if (e.Error is RuntimeShutdownException)
        {
            e.Handled = true;
        }
    }

    /// <summary>
    /// Shutdown and release resources held by the Python runtime. The
    /// Python runtime can no longer be used in the current process
    /// after calling the Shutdown method.
    /// </summary>
    public static void Shutdown()
    {
        if (!initialized)
        {
            return;
        }

        using (Py.GIL())
        {
            if (Exceptions.ErrorOccurred())
            {
                throw new InvalidOperationException(
                    "Python error indicator is set",
                    innerException: PythonException.PeekCurrentOrNull(out _));
            }
        }

        // If the shutdown handlers trigger a domain unload,
        // don't call shutdown again.
        AppDomain.CurrentDomain.DomainUnload -= OnDomainUnload;
        AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;

        ExecuteShutdownHandlers();
        // Remember to shut down the runtime.
        Runtime.Shutdown();

        initialized = false;

        InteropConfiguration = InteropConfiguration.MakeDefault();
    }

    /// <summary>
    /// Called when the engine is shut down.
    ///
    /// Shutdown handlers are run in reverse order they were added, so that
    /// resources available when running a shutdown handler are the same as
    /// what was available when it was added.
    /// </summary>
    public delegate void ShutdownHandler();

    // Lock: ConcurrentStack lacks remove-by-equality; List<> needs serialised mutation.
    static readonly List<ShutdownHandler> ShutdownHandlers = new();
    static readonly object _shutdownHandlersLock = new();

    /// <summary>
    /// Add a function to be called when the engine is shut down.
    ///
    /// Shutdown handlers are executed in the opposite order they were
    /// added, so that you can be sure that everything that was initialized
    /// when you added the handler is still initialized when you need to shut
    /// down.
    ///
    /// If the same shutdown handler is added several times, it will be run
    /// several times.
    ///
    /// Don't add shutdown handlers while running a shutdown handler.
    /// </summary>
    public static void AddShutdownHandler(ShutdownHandler handler)
    {
        lock (_shutdownHandlersLock) ShutdownHandlers.Add(handler);
    }

    /// <summary>
    /// Remove a shutdown handler.
    ///
    /// If the same shutdown handler is added several times, only the last
    /// one is removed.
    ///
    /// Don't remove shutdown handlers while running a shutdown handler.
    /// </summary>
    public static void RemoveShutdownHandler(ShutdownHandler handler)
    {
        lock (_shutdownHandlersLock)
        {
            for (int index = ShutdownHandlers.Count - 1; index >= 0; --index)
            {
                if (ShutdownHandlers[index] == handler)
                {
                    ShutdownHandlers.RemoveAt(index);
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Run all the shutdown handlers.
    ///
    /// They're run in opposite order they were added.
    /// </summary>
    static void ExecuteShutdownHandlers()
    {
        // Invoke unlocked so handlers can re-enter Add/Remove.
        while (true)
        {
            ShutdownHandler handler;
            lock (_shutdownHandlersLock)
            {
                if (ShutdownHandlers.Count == 0) return;
                int last = ShutdownHandlers.Count - 1;
                handler = ShutdownHandlers[last];
                ShutdownHandlers.RemoveAt(last);
            }
            handler();
        }
    }

    /// <summary>
    /// AcquireLock Method
    /// </summary>
    /// <remarks>
    /// Acquire the Python global interpreter lock (GIL). Managed code
    /// *must* call this method before using any objects or calling any
    /// methods on objects in the CodeBrix.Python namespace. The only
    /// exception is PythonEngine.Initialize, which may be called without
    /// first calling AcquireLock.
    /// Each call to AcquireLock must be matched by a corresponding call
    /// to ReleaseLock, passing the token obtained from AcquireLock.
    /// For more information, see the "Extending and Embedding" section
    /// of the Python documentation on www.python.org.
    /// </remarks>
    internal static PyGILState AcquireLock()
    {
        return Runtime.PyGILState_Ensure();
    }


    /// <summary>
    /// ReleaseLock Method
    /// </summary>
    /// <remarks>
    /// Release the Python global interpreter lock using a token obtained
    /// from a previous call to AcquireLock.
    /// For more information, see the "Extending and Embedding" section
    /// of the Python documentation on www.python.org.
    /// </remarks>
    internal static void ReleaseLock(PyGILState gs)
    {
        Runtime.PyGILState_Release(gs);
    }


    /// <summary>
    /// BeginAllowThreads Method
    /// </summary>
    /// <remarks>
    /// Release the Python global interpreter lock to allow other threads
    /// to run. This is equivalent to the Py_BEGIN_ALLOW_THREADS macro
    /// provided by the C Python API.
    /// For more information, see the "Extending and Embedding" section
    /// of the Python documentation on www.python.org.
    /// </remarks>
    public static unsafe IntPtr BeginAllowThreads()
    {
        return (IntPtr)Runtime.PyEval_SaveThread();
    }


    /// <summary>
    /// EndAllowThreads Method
    /// </summary>
    /// <remarks>
    /// Re-aquire the Python global interpreter lock for the current
    /// thread. This is equivalent to the Py_END_ALLOW_THREADS macro
    /// provided by the C Python API.
    /// For more information, see the "Extending and Embedding" section
    /// of the Python documentation on www.python.org.
    /// </remarks>
    public static unsafe void EndAllowThreads(IntPtr ts)
    {
        Runtime.PyEval_RestoreThread((PyThreadState*)ts);
    }

    public static PyObject Compile(string code, string filename = "", RunFlagType mode = RunFlagType.File)
    {
        var flag = (int)mode;
        NewReference ptr = Runtime.Py_CompileString(code, filename, flag);
        PythonException.ThrowIfIsNull(ptr);
        return ptr.MoveToPyObject();
    }


    /// <summary>
    /// Eval Method
    /// </summary>
    /// <remarks>
    /// Evaluate a Python expression and returns the result.
    /// It's a subset of Python eval function.
    /// </remarks>
    public static PyObject Eval(string code, PyDict? globals = null, PyObject? locals = null)
    {
        PyObject result = RunString(code, globals.BorrowNullable(), locals.BorrowNullable(), RunFlagType.Eval);
        return result;
    }


    /// <summary>
    /// Exec Method
    /// </summary>
    /// <remarks>
    /// Run a string containing Python code.
    /// It's a subset of Python exec function.
    /// </remarks>
    public static void Exec(string code, PyDict? globals = null, PyObject? locals = null)
    {
        using PyObject result = RunString(code, globals.BorrowNullable(), locals.BorrowNullable(), RunFlagType.File);
        if (result.obj != Runtime.PyNone)
        {
            throw PythonException.ThrowLastAsClrException();
        }
    }
    /// <summary>
    /// Exec Method
    /// </summary>
    /// <remarks>
    /// Run a string containing Python code.
    /// It's a subset of Python exec function.
    /// </remarks>
    internal static void Exec(string code, BorrowedReference globals, BorrowedReference locals = default)
    {
        using PyObject result = RunString(code, globals: globals, locals: locals, RunFlagType.File);
        if (result.obj != Runtime.PyNone)
        {
            throw PythonException.ThrowLastAsClrException();
        }
    }

    /// <summary>
    /// Gets the Python thread ID.
    /// </summary>
    /// <returns>The Python thread ID.</returns>
    public static ulong GetPythonThreadID()
    {
        using PyObject threading = Py.Import("threading");
        using PyObject id = threading.InvokeMethod("get_ident");
        return id.As<ulong>();
    }

    /// <summary>
    /// Interrupts the execution of a thread.
    /// </summary>
    /// <param name="pythonThreadID">The Python thread ID.</param>
    /// <returns>The number of thread states modified; this is normally one, but will be zero if the thread id is not found.</returns>
    public static int Interrupt(ulong pythonThreadID)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return Runtime.PyThreadState_SetAsyncExcLLP64((uint)pythonThreadID, Exceptions.KeyboardInterrupt);
        }

        return Runtime.PyThreadState_SetAsyncExcLP64(pythonThreadID, Exceptions.KeyboardInterrupt);
    }

    /// <summary>
    /// RunString Method. Function has been deprecated and will be removed.
    /// Use Exec/Eval/RunSimpleString instead.
    /// </summary>
    [Obsolete("RunString is deprecated and will be removed. Use Exec/Eval/RunSimpleString instead.")]
    public static PyObject RunString(string code, PyDict? globals = null, PyObject? locals = null)
    {
        return RunString(code, globals.BorrowNullable(), locals.BorrowNullable(), RunFlagType.File);
    }

    /// <summary>
    /// Internal RunString Method.
    /// </summary>
    /// <remarks>
    /// Run a string containing Python code. Returns the result of
    /// executing the code string as a PyObject instance, or null if
    /// an exception was raised.
    /// </remarks>
    internal static PyObject RunString(string code, BorrowedReference globals, BorrowedReference locals, RunFlagType flag)
    {
        if (code is null) throw new ArgumentNullException(nameof(code));

        NewReference tempGlobals = default;
        if (globals.IsNull)
        {
            globals = Runtime.PyEval_GetGlobals();
            if (globals.IsNull)
            {
                tempGlobals = Runtime.PyDict_New();
                globals = tempGlobals.BorrowOrThrow();
                Runtime.PyDict_SetItem(
                    globals, PyIdentifier.__builtins__,
                    Runtime.PyEval_GetBuiltins()
                );
            }
        }

        if (locals == null)
        {
            locals = globals;
        }

        try
        {
            NewReference result = Runtime.PyRun_String(
                code, flag, globals, locals
            );
            PythonException.ThrowIfIsNull(result);
            return result.MoveToPyObject();
        }
        finally
        {
            tempGlobals.Dispose();
        }
    }
}

public enum RunFlagType : int
{
    Single = 256,
    File = 257, /* Py_file_input */
    Eval = 258
}
