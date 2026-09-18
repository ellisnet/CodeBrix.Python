using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Xunit;
using SilverAssertions;
using SilverAssertions.Primitives;
using SilverAssertions.Specialized;

namespace CodeBrix.Python.Tests;

/// <summary>
/// Tests for <see cref="ProcessExitShutdownMode"/> and the two <see cref="PythonEngine"/>
/// properties that select it.
/// <para>
/// What the mode changes can only be seen while a process is exiting, so the behavioural tests
/// run the CodeBrix.Python.ExitProbe console application - one child process per mode - and watch
/// whether it ends by itself. The default mode is expected NOT to end: that is the documented
/// deadlock, and this fences it so a change in it is noticed. The child is always killed
/// afterwards, so a probe that hangs by design can never hang this test run.
/// </para>
/// <para>
/// Everything here is deliberately synchronous. This assembly's interpreter belongs to the thread
/// that initialized it and that thread never releases the GIL, so a test that awaits an operation
/// which really suspends hands the rest of the run to a thread pool thread, where the next test
/// to touch the Python C API crashes the process. Waiting is therefore done by polling, with the
/// test's cancellation token as the wait handle.
/// </para>
/// </summary>
public class ProcessExitShutdownModeTests
{
    private const string ProbeFolderName = "exitprobe";
    private const string ProbeName = "CodeBrix.Python.ExitProbe";
    private const string PyDllEnvVar = "PYTHONNET_PYDLL";

    /// <summary>How long a probe is given to start an interpreter and report that it is ready.</summary>
    private static readonly TimeSpan ReadyTimeout = TimeSpan.FromSeconds(60);

    /// <summary>How long a probe that is expected to end is given to end.</summary>
    private static readonly TimeSpan ExitTimeout = TimeSpan.FromSeconds(30);

    /// <summary>How long the deadlocking probe is watched before it is declared blocked.</summary>
    private static readonly TimeSpan BlockedWindow = TimeSpan.FromSeconds(3);

    /// <summary>How often the polling waits look at the world.</summary>
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(50);

    [Fact]
    public void ProcessExitShutdown_defaults_to_waiting_for_the_shutdown()
    {
        //Arrange & Act
        ProcessExitShutdownMode mode = PythonEngine.ProcessExitShutdown;

        //Assert
        mode.Should().Be(ProcessExitShutdownMode.Wait);
    }

    [Fact]
    public void ProcessExitShutdownTimeout_defaults_to_five_seconds()
    {
        //Arrange & Act
        TimeSpan timeout = PythonEngine.ProcessExitShutdownTimeout;

        //Assert
        timeout.Should().Be(TimeSpan.FromSeconds(5));
    }

    [Theory]
    [InlineData(ProcessExitShutdownMode.Wait)]
    [InlineData(ProcessExitShutdownMode.WaitWithTimeout)]
    [InlineData(ProcessExitShutdownMode.Skip)]
    public void ProcessExitShutdown_round_trips_every_declared_mode(ProcessExitShutdownMode mode)
    {
        //Arrange
        ProcessExitShutdownMode original = PythonEngine.ProcessExitShutdown;

        try
        {
            //Act
            PythonEngine.ProcessExitShutdown = mode;

            //Assert
            PythonEngine.ProcessExitShutdown.Should().Be(mode);
        }
        finally
        {
            PythonEngine.ProcessExitShutdown = original;
        }
    }

    [Fact]
    public void ProcessExitShutdown_rejects_an_undeclared_mode()
    {
        //Arrange
        Action act = () => PythonEngine.ProcessExitShutdown = (ProcessExitShutdownMode)42;

        //Act & Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
        PythonEngine.ProcessExitShutdown.Should().Be(ProcessExitShutdownMode.Wait);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ProcessExitShutdownTimeout_rejects_a_wait_that_is_not_positive(int seconds)
    {
        //Arrange
        Action act = () => PythonEngine.ProcessExitShutdownTimeout = TimeSpan.FromSeconds(seconds);

        //Act & Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ProcessExitShutdownTimeout_rejects_a_wait_longer_than_a_thread_join_accepts()
    {
        //Arrange
        TimeSpan tooLong = TimeSpan.FromMilliseconds(int.MaxValue) + TimeSpan.FromMilliseconds(1);
        Action act = () => PythonEngine.ProcessExitShutdownTimeout = tooLong;

        //Act & Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ProcessExitShutdownTimeout_round_trips_a_positive_wait()
    {
        //Arrange
        TimeSpan original = PythonEngine.ProcessExitShutdownTimeout;

        try
        {
            //Act
            PythonEngine.ProcessExitShutdownTimeout = TimeSpan.FromMilliseconds(250);

            //Assert
            PythonEngine.ProcessExitShutdownTimeout.Should().Be(TimeSpan.FromMilliseconds(250));
        }
        finally
        {
            PythonEngine.ProcessExitShutdownTimeout = original;
        }
    }

    [Fact]
    public void the_default_mode_leaves_the_exiting_process_blocked()
    {
        //Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        //Act
        ProbeRun run = RunProbe(Probe.DefaultMode, BlockedWindow, cancellationToken);

        //Assert
        run.Exited.Should().BeFalse(
            "the default mode shuts the interpreter down from the exit thread while the thread"
            + " that initialized it still holds the GIL, which is the documented deadlock."
            + run.Report());
    }

    [Theory]
    [InlineData(Probe.BoundedMode)]
    [InlineData(Probe.SkipMode)]
    [InlineData(Probe.ShutdownMode)]
    [InlineData(Probe.AllowThreadsMode)]
    public void every_way_out_lets_the_exiting_process_finish(string mode)
    {
        //Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        //Act
        ProbeRun run = RunProbe(mode, ExitTimeout, cancellationToken);

        //Assert
        run.Exited.Should().BeTrue($"'{mode}' must let the process exit." + run.Report());
        run.ExitCode.Should().Be(0, $"'{mode}' must exit cleanly." + run.Report());
    }

    /// <summary>The probe modes, spelled the way the probe's own command line spells them.</summary>
    private static class Probe
    {
        internal const string DefaultMode = "default";
        internal const string BoundedMode = "bounded";
        internal const string SkipMode = "skip";
        internal const string ShutdownMode = "shutdown";
        internal const string AllowThreadsMode = "allowthreads";
    }

    /// <summary>What one probe run is worth knowing about afterwards.</summary>
    /// <param name="Exited">Whether the process ended within the time it was given.</param>
    /// <param name="ExitCode">Its exit code, or -1 when it never ended.</param>
    /// <param name="Output">Everything it wrote to standard output.</param>
    /// <param name="Errors">Everything it wrote to standard error.</param>
    private sealed record ProbeRun(bool Exited, int ExitCode, string Output, string Errors)
    {
        /// <summary>Renders the run for an assertion message.</summary>
        /// <returns>The probe's output, or an empty string when it produced none.</returns>
        internal string Report()
        {
            string text = (Output + Errors).Trim();
            return text.Length == 0 ? string.Empty : "\nProbe output:\n" + text;
        }
    }

    /// <summary>
    /// Runs one probe, waits for it to report that its interpreter is up, then watches for at
    /// most <paramref name="exitWait"/> to see whether it ends. The process is always killed
    /// before returning.
    /// </summary>
    /// <param name="mode">The probe mode to run.</param>
    /// <param name="exitWait">How long to wait for the process to end.</param>
    /// <param name="cancellationToken">The test's cancellation token.</param>
    /// <returns>What happened.</returns>
    private static ProbeRun RunProbe(string mode, TimeSpan exitWait, CancellationToken cancellationToken)
    {
        string readyFile = Path.Combine(
            Path.GetTempPath(), "codebrix-exitprobe-" + Guid.NewGuid().ToString("N") + ".ready");

        try
        {
            using Process process = StartProbe(mode, readyFile);

            bool exited;
            try
            {
                WaitUntilReady(process, readyFile, cancellationToken);
                exited = WaitForExit(process, exitWait, cancellationToken);
            }
            finally
            {
                KillIfRunning(process);
            }

            // Draining is safe only once the process is gone - a live child's stream would block
            // this thread forever - so a child that somehow survived the kill above is reported
            // without its output rather than waited on. The probe writes a couple of short lines,
            // far below any pipe buffer.
            string output = string.Empty;
            string errors = string.Empty;
            if (process.HasExited)
            {
                output = process.StandardOutput.ReadToEnd();
                errors = process.StandardError.ReadToEnd();
            }

            return new ProbeRun(exited, exited ? process.ExitCode : -1, output, errors);
        }
        finally
        {
            DeleteIfPresent(readyFile);
        }
    }

    /// <summary>
    /// Polls for the file the probe creates once its interpreter is running, so that a slow start
    /// is never mistaken for one of the outcomes under test.
    /// </summary>
    /// <param name="process">The running probe.</param>
    /// <param name="readyFile">The file the probe was told to create.</param>
    /// <param name="cancellationToken">The test's cancellation token.</param>
    /// <exception cref="InvalidOperationException">The probe never reported itself ready.</exception>
    private static void WaitUntilReady(Process process, string readyFile, CancellationToken cancellationToken)
    {
        var elapsed = Stopwatch.StartNew();

        while (!File.Exists(readyFile))
        {
            if (process.HasExited)
            {
                throw new InvalidOperationException(
                    "The exit probe ended before its interpreter was running (exit code"
                    + $" {process.ExitCode}).\n{process.StandardError.ReadToEnd()}");
            }

            if (elapsed.Elapsed > ReadyTimeout)
            {
                throw new InvalidOperationException(
                    $"The exit probe did not report a running interpreter within {ReadyTimeout}.");
            }

            Wait(cancellationToken);
        }
    }

    /// <summary>Waits a bounded time for the process to end.</summary>
    /// <param name="process">The running probe.</param>
    /// <param name="exitWait">How long to wait.</param>
    /// <param name="cancellationToken">The test's cancellation token.</param>
    /// <returns><c>true</c> when it ended within the wait.</returns>
    private static bool WaitForExit(Process process, TimeSpan exitWait, CancellationToken cancellationToken)
    {
        var elapsed = Stopwatch.StartNew();

        while (!process.HasExited)
        {
            if (elapsed.Elapsed > exitWait)
            {
                return false;
            }

            Wait(cancellationToken);
        }

        return true;
    }

    /// <summary>
    /// Sleeps for one poll interval, or throws as soon as the test run is cancelled.
    /// </summary>
    /// <param name="cancellationToken">The test's cancellation token.</param>
    private static void Wait(CancellationToken cancellationToken)
    {
        if (cancellationToken.WaitHandle.WaitOne(PollInterval))
        {
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    /// <summary>
    /// Starts one probe, preferring the native launcher and falling back to the <c>dotnet</c>
    /// muxer when there is none or the operating system refuses to execute it.
    /// </summary>
    /// <param name="mode">The probe mode to run.</param>
    /// <param name="readyFile">The file the probe should create once it is running.</param>
    /// <returns>The running process.</returns>
    private static Process StartProbe(string mode, string readyFile)
    {
        try
        {
            ProcessStartInfo startInfo = CreateProbeStartInfo(mode, readyFile, useMuxer: false);
            return Process.Start(startInfo)
                ?? throw new InvalidOperationException($"'{startInfo.FileName}' could not be started.");
        }
        catch (Win32Exception)
        {
            // The launcher is there but not executable - a file mode that did not survive being
            // copied, for instance. The assembly always runs through the muxer.
            ProcessStartInfo startInfo = CreateProbeStartInfo(mode, readyFile, useMuxer: true);
            return Process.Start(startInfo)
                ?? throw new InvalidOperationException($"'{startInfo.FileName}' could not be started.");
        }
    }

    /// <summary>
    /// Builds the command that runs one probe. The native launcher next to the probe assembly is
    /// preferred - one is produced on all three operating systems and it needs nothing on PATH -
    /// and the <c>dotnet</c> muxer is the fallback.
    /// </summary>
    /// <param name="mode">The probe mode to pass on the command line.</param>
    /// <param name="readyFile">The file the probe should create once it is running.</param>
    /// <param name="useMuxer">Run the assembly through <c>dotnet</c> rather than the launcher.</param>
    /// <returns>The start information, with libpython already named for the child.</returns>
    private static ProcessStartInfo CreateProbeStartInfo(string mode, string readyFile, bool useMuxer)
    {
        string directory = Path.Combine(AppContext.BaseDirectory, ProbeFolderName);
        string launcher = Path.Combine(
            directory,
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ProbeName + ".exe" : ProbeName);
        string assembly = Path.Combine(directory, ProbeName + ".dll");

        if (!File.Exists(assembly))
        {
            throw new InvalidOperationException(
                $"The exit probe was not found at '{assembly}'. It is copied there by this"
                + " project's CopyExitProbeToOutput target; build the solution, not just this"
                + " project.");
        }

        var startInfo = new ProcessStartInfo
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = directory,
        };

        if (!useMuxer && File.Exists(launcher))
        {
            startInfo.FileName = launcher;
        }
        else
        {
            startInfo.FileName = "dotnet";
            startInfo.ArgumentList.Add(assembly);
        }

        startInfo.ArgumentList.Add(mode);
        startInfo.ArgumentList.Add(readyFile);

        // The probe does no discovery of its own; it is handed the libpython this assembly found.
        PlatformPythonDll.EnsureSet();
        string libPython = Environment.GetEnvironmentVariable(PyDllEnvVar);
        if (!string.IsNullOrWhiteSpace(libPython))
        {
            startInfo.Environment[PyDllEnvVar] = libPython;
        }

        // A virtual environment inherited from whoever started the test run would change what the
        // child's interpreter does at startup, which is not what these tests are about.
        startInfo.Environment.Remove("PYTHONNET_VENV");
        startInfo.Environment.Remove("VIRTUAL_ENV");
        startInfo.Environment.Remove("PYTHONNET_PYEXE");

        return startInfo;
    }

    /// <summary>Kills a probe that is still running, and the children it may have started.</summary>
    /// <param name="process">The probe.</param>
    private static void KillIfRunning(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(milliseconds: 10_000);
            }
        }
        catch (InvalidOperationException)
        {
            // Already gone between the check and the kill.
        }
        catch (Win32Exception)
        {
            // The operating system refused the kill. Nothing more this test can do about it, and
            // throwing from a finally would hide whatever the test was actually reporting.
        }
        catch (NotSupportedException)
        {
        }
    }

    /// <summary>Removes the ready file, tolerating a file another process still holds.</summary>
    /// <param name="path">The file to remove.</param>
    private static void DeleteIfPresent(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
