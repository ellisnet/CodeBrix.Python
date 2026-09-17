using System;
using System.Globalization;
using System.IO;

namespace CodeBrix.Python.ExitProbe;

/// <summary>
/// Starts an embedded interpreter, reports that it is ready and returns from <c>Main</c>. Whether
/// the process then ends is the observation: everything after the return happens inside the
/// CLR's process-exit sequence, where this library's <c>AppDomain.ProcessExit</c> handler runs.
/// The first command-line argument selects what the handler is told to do; an optional second one
/// names a file to create once the interpreter is up, which is how a watching process can tell a
/// slow start apart from the outcome it is measuring.
/// </summary>
internal static class Program
{
    /// <summary>Printed on the last line of standard output, once the interpreter is up.</summary>
    internal const string ReadyMarker = "READY";

    /// <summary>The interpreter is started but never shut down; the default handler runs.</summary>
    internal const string DefaultMode = "default";

    /// <summary>The handler is bounded by a short timeout and abandons a blocked shutdown.</summary>
    internal const string BoundedMode = "bounded";

    /// <summary>The handler does nothing at all.</summary>
    internal const string SkipMode = "skip";

    /// <summary>The interpreter is shut down explicitly, which unsubscribes the handler.</summary>
    internal const string ShutdownMode = "shutdown";

    /// <summary>The GIL is released before returning, so the default handler can take it.</summary>
    internal const string AllowThreadsMode = "allowthreads";

    /// <summary>How long the bounded mode is given here: long enough to succeed, short enough
    /// that a test can tell a bounded wait from a hang.</summary>
    private static readonly TimeSpan BoundedTimeout = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Runs one probe.
    /// </summary>
    /// <param name="args">
    /// The mode to probe (default <see cref="DefaultMode"/>), optionally followed by the path of
    /// a ready file to create once the interpreter is running.
    /// </param>
    /// <returns>0 when the interpreter started, 2 when the mode was not recognized.</returns>
    internal static int Main(string[] args)
    {
        string mode = args.Length > 0 ? args[0] : DefaultMode;
        string readyFile = args.Length > 1 ? args[1] : null;

        switch (mode)
        {
            case DefaultMode:
            case ShutdownMode:
            case AllowThreadsMode:
                break;

            case BoundedMode:
                PythonEngine.ProcessExitShutdown = ProcessExitShutdownMode.WaitWithTimeout;
                PythonEngine.ProcessExitShutdownTimeout = BoundedTimeout;
                break;

            case SkipMode:
                PythonEngine.ProcessExitShutdown = ProcessExitShutdownMode.Skip;
                break;

            default:
                Console.Error.WriteLine(
                    $"Unknown mode '{mode}'. Expected one of: {DefaultMode}, {BoundedMode},"
                    + $" {SkipMode}, {ShutdownMode}, {AllowThreadsMode}.");
                return 2;
        }

        // libpython comes from PYTHONNET_PYDLL, which the spawning test exports after its own
        // platform discovery has run. Run by hand, this needs that variable set.
        PythonEngine.Initialize();

        using (Py.GIL())
        {
            using PyObject sys = Py.Import("sys");
            using PyObject maxsize = sys.GetAttr("maxsize");
            Console.WriteLine(string.Format(
                CultureInfo.InvariantCulture, "sys.maxsize = {0}", maxsize.As<long>()));
        }

        switch (mode)
        {
            case ShutdownMode:
                // The documented way out: shutting down unsubscribes the process-exit handler.
                PythonEngine.Shutdown();
                break;

            case AllowThreadsMode:
                // The other way out: hand the GIL back, so the handler's shutdown can take it.
                PythonEngine.BeginAllowThreads();
                break;
        }

        Console.WriteLine(ReadyMarker);
        Console.Out.Flush();

        if (!string.IsNullOrEmpty(readyFile))
        {
            // Written last, and only ever read after it exists, so its mere presence is the
            // signal. A file works the same on every operating system and cannot deadlock the
            // watching process the way an unread redirected pipe can.
            File.WriteAllText(readyFile, mode);
        }

        return 0;
    }
}
