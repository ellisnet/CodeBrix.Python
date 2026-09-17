using System;

namespace CodeBrix.Python;

/// <summary>
/// What the library does about the embedded interpreter when the process is exiting and
/// <see cref="PythonEngine.Shutdown"/> was never called. Selected through
/// <see cref="PythonEngine.ProcessExitShutdown"/>.
/// <para>
/// Why this exists: <c>Py_InitializeEx</c> leaves the Global Interpreter Lock held by the thread
/// that called <see cref="PythonEngine.Initialize()"/>, and nothing releases it; the CLR raises
/// <see cref="AppDomain.ProcessExit"/> on a different, dedicated shutdown thread while that
/// thread is parked waiting for the exit handlers to finish. Finalizing Python needs the GIL, so
/// the handler waits for a lock that only the parked thread could release. There is no timeout on
/// <see cref="AppDomain.ProcessExit"/> handlers, so the process never exits.
/// </para>
/// <para>
/// The way out that costs nothing is to own the interpreter's lifetime and call
/// <see cref="PythonEngine.Shutdown"/> exactly once yourself - that unsubscribes the handler, so
/// the situation cannot arise. These modes are for applications that cannot, or will not, do that.
/// </para>
/// </summary>
public enum ProcessExitShutdownMode
{
    /// <summary>
    /// Run <see cref="PythonEngine.Shutdown"/> on the thread raising
    /// <see cref="AppDomain.ProcessExit"/> and wait for it to finish, however long that takes.
    /// This is the default and the historical behaviour: it finalizes the interpreter properly
    /// when the GIL is free, and hangs the exiting process when it is not.
    /// </summary>
    Wait = 0,

    /// <summary>
    /// Run <see cref="PythonEngine.Shutdown"/> on a helper thread and wait no longer than
    /// <see cref="PythonEngine.ProcessExitShutdownTimeout"/> for it. When the shutdown finishes
    /// in time the outcome is identical to <see cref="Wait"/>, including any exception it raised.
    /// When it does not, the handler returns and the process exits with the interpreter
    /// unfinalized - which costs nothing, because the operating system is about to reclaim the
    /// whole address space anyway.
    /// </summary>
    WaitWithTimeout = 1,

    /// <summary>
    /// Do nothing at process exit. Choose this when the application shuts the engine down on its
    /// own schedule, or when it accepts an unfinalized interpreter: no Python-level
    /// <c>atexit</c> handler runs, no buffered Python output is flushed, and no native resource
    /// the interpreter holds is released before the process ends.
    /// </summary>
    Skip = 2,
}
