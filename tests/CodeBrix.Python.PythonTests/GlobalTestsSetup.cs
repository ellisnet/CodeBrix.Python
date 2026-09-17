using System;
using Xunit;
using Xunit.Sdk;   // ParallelMode
using Xunit.v3;    // ParallelizationAttribute

// CodeBrix port: the upstream NUnit [OneTimeSetUp] / [OneTimeTearDown] pair on the
// python test runner becomes an xUnit v3 assembly fixture, so the embedded
// interpreter is created once for the whole assembly and shut down exactly once
// after the last test. Test parallelization is disabled because the embedded
// CPython interpreter is single-threaded under the Global Interpreter Lock.
[assembly: AssemblyFixture(typeof(CodeBrix.Python.Tests.PythonSuite.GlobalTestsSetup))]
[assembly: Parallelization(Mode = ParallelMode.None)]

namespace CodeBrix.Python.Tests.PythonSuite; //was previously: Python.PythonTestsRunner;

/// <summary>
/// Owns the embedded Python interpreter for this test assembly. The engine is
/// initialized once, on first use, and shut down exactly once - here, at the very
/// end of the run. It is never re-initialized afterwards: CPython cannot be brought
/// back up in the same process, so a second <see cref="PythonEngine.Initialize()"/>
/// would crash the host rather than fail cleanly.
/// </summary>
public sealed class GlobalTestsSetup : IDisposable
{
    /// <summary>
    /// Message used when Python work is attempted after the engine has been shut down.
    /// </summary>
    internal const string ShutdownMessage =
        "Cannot perform Python operations after PythonEngine.Shutdown() has been called.";

    private static readonly object Gate = new();
    private static bool _initialized;
    private static bool _shutDown;

    /// <summary>
    /// Initializes the embedded interpreter for the whole test assembly.
    /// </summary>
    public GlobalTestsSetup() => EnsureEngine();

    /// <summary>
    /// Initializes the embedded interpreter on first use. Subsequent calls are a no-op.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The engine has already been shut down for this process.
    /// </exception>
    internal static void EnsureEngine()
    {
        lock (Gate)
        {
            if (_shutDown)
            {
                throw new InvalidOperationException(ShutdownMessage);
            }
            if (_initialized)
            {
                return;
            }

            // On Windows and macOS, locate libpython via appsettings.json (process-scoped
            // PYTHONNET_PYDLL) so no persistent environment variable is needed. No-op on Linux.
            // Mirrors the sibling CodeBrix.Python.Tests project so both projects behave the same.
            PlatformPythonDll.EnsureSet();
            string dll = Environment.GetEnvironmentVariable("PYTHONNET_PYDLL");
            if (!string.IsNullOrEmpty(dll))
            {
                Runtime.PythonDLL = dll;
            }
            if (!PythonEngine.IsInitialized)
            {
                PythonEngine.Initialize();
            }
            _initialized = true;
        }
    }

    /// <summary>
    /// Throws when the engine has already been shut down, so a stray test cannot touch
    /// a dead interpreter and take the host process down with a native fault.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The engine has already been shut down for this process.
    /// </exception>
    internal static void ThrowIfShutDown()
    {
        lock (Gate)
        {
            if (_shutDown)
            {
                throw new InvalidOperationException(ShutdownMessage);
            }
        }
    }

    /// <summary>
    /// Shuts the embedded interpreter down, exactly once, after every test has run.
    /// </summary>
    public void Dispose()
    {
        lock (Gate)
        {
            if (_shutDown)
            {
                return;
            }
            _shutDown = true;
            if (_initialized)
            {
                _initialized = false;
                PythonEngine.Shutdown();
            }
        }
    }
}
