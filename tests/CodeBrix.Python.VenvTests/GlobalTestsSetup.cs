using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using CodeBrix.Python;
using Xunit;
using Xunit.Sdk;   // ParallelMode
using Xunit.v3;    // ParallelizationAttribute

// The embedded interpreter is created once for the whole assembly and shut down exactly once
// after the last test. Test parallelization is disabled because the embedded CPython
// interpreter is single-threaded under the Global Interpreter Lock.
[assembly: AssemblyFixture(typeof(CodeBrix.Python.Tests.Venv.GlobalTestsSetup))]
[assembly: Parallelization(Mode = ParallelMode.None)]

namespace CodeBrix.Python.Tests.Venv;

/// <summary>
/// Builds a throwaway Python virtual environment, points the engine at it through
/// <see cref="PythonEngine.VirtualEnvironment"/> and starts the interpreter inside it. This
/// assembly exists because an interpreter can only be started once per process, so end-to-end
/// virtual-environment activation cannot share a host with the other test assemblies.
/// <para>
/// Nothing here pre-empts the library's own libpython resolution: the whole point is that the
/// environment's <c>pyvenv.cfg</c> is enough to find the base installation's libpython. The
/// PYTHONNET_PYDLL value the platform helper would export is used only to locate the base
/// interpreter executable on Windows and macOS, and is removed again before the engine starts.
/// </para>
/// </summary>
public sealed class GlobalTestsSetup : IDisposable
{
    private const string PyDllEnvVar = "PYTHONNET_PYDLL";
    private const string MarkerModuleName = "codebrix_venv_marker";

    /// <summary>The value the marker module in the environment's site-packages reports.</summary>
    internal const string MarkerValue = "codebrix-venv-marker";

    /// <summary>The name, without extension, of the module dropped into site-packages.</summary>
    internal static string MarkerModule => MarkerModuleName;

    /// <summary>How many extra attempts the throwaway environment's deletion gets.</summary>
    private const int DeleteRetries = 4;

    /// <summary>How long to wait between those attempts.</summary>
    private const int DeleteRetryDelayMilliseconds = 250;

    private static readonly object Gate = new();
    private static bool _initialized;
    private static bool _shutDown;
    private static string _temporaryRoot;
    private static string _virtualEnvironmentPath;

    /// <summary>The throwaway virtual environment this assembly runs inside.</summary>
    internal static string VirtualEnvironmentPath
    {
        get
        {
            EnsureEngine();
            return _virtualEnvironmentPath;
        }
    }

    /// <summary>Creates the virtual environment and starts the interpreter inside it.</summary>
    public GlobalTestsSetup() => EnsureEngine();

    /// <summary>
    /// Creates the environment and initializes the engine on first use. Subsequent calls are
    /// a no-op.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The engine has already been shut down for this process, no base interpreter could be
    /// found, or creating the virtual environment failed.
    /// </exception>
    internal static void EnsureEngine()
    {
        lock (Gate)
        {
            if (_shutDown)
            {
                throw new InvalidOperationException(
                    "Cannot perform Python operations after PythonEngine.Shutdown() has been called.");
            }
            if (_initialized)
            {
                return;
            }

            string baseInterpreter = FindBaseInterpreter();

            _temporaryRoot = Path.Combine(
                Path.GetTempPath(), "codebrix-venvtests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_temporaryRoot);
            _virtualEnvironmentPath = Path.Combine(_temporaryRoot, "venv");

            CreateVirtualEnvironment(baseInterpreter, _virtualEnvironmentPath);
            WriteMarkerModule(_virtualEnvironmentPath);

            PythonEngine.VirtualEnvironment = _virtualEnvironmentPath;
            PythonEngine.Initialize();

            _initialized = true;
        }
    }

    /// <summary>
    /// Shuts the interpreter down, exactly once, and removes the throwaway environment.
    /// Without that shutdown the test executable never exits: the library's process-exit
    /// handler would try to shut the engine down from the CLR's shutdown thread while this
    /// thread still holds the interpreter.
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
            DeleteTemporaryRoot();
        }
    }

    /// <summary>
    /// Locates the interpreter the throwaway environment is created from: <c>python3</c> on
    /// PATH on Linux, and on Windows and macOS an executable derived from the libpython path
    /// configured in appsettings.json.
    /// </summary>
    /// <returns>The command or full path to run as the base interpreter.</returns>
    /// <exception cref="InvalidOperationException">No interpreter could be located.</exception>
    private static string FindBaseInterpreter()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            // Same rule the other test projects use to find CPython on Linux.
            return "python3";
        }

        string libPython = ReadConfiguredLibPython();

        if (string.IsNullOrWhiteSpace(libPython))
        {
            throw new InvalidOperationException(
                "These tests create a throwaway Python virtual environment and therefore need the"
                + " interpreter that owns the configured libpython. Set 'PythonDllPath' (Windows) or"
                + " 'PythonMacOsPath' (macOS) in 'appsettings.json' next to this test assembly - or set"
                + $" the {PyDllEnvVar} environment variable - to a real libpython path.");
        }

        string directory = Path.GetDirectoryName(libPython);
        string interpreter = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            // python3XX.dll sits next to python.exe in a CPython installation.
            ? Path.Combine(directory, "python.exe")
            // A macOS framework build keeps libpythonX.Y.dylib in Versions/X.Y/lib, one folder
            // away from Versions/X.Y/bin/python3.
            : Path.GetFullPath(Path.Combine(directory, "..", "bin", "python3"));

        if (!File.Exists(interpreter))
        {
            throw new InvalidOperationException(
                $"No Python interpreter was found at '{interpreter}', which is where one is expected"
                + $" relative to the configured libpython '{libPython}'. Point the appsettings.json"
                + " path at the libpython inside a complete CPython installation.");
        }

        return interpreter;
    }

    /// <summary>
    /// Asks the platform helper for the configured libpython path, then removes the environment
    /// variable it exported unless it was already set by the caller, so that resolving libpython
    /// stays the virtual environment's job.
    /// </summary>
    /// <returns>The configured libpython path, or <c>null</c>.</returns>
    private static string ReadConfiguredLibPython()
    {
        string preExisting = Environment.GetEnvironmentVariable(PyDllEnvVar);
        PlatformPythonDll.EnsureSet();
        string configured = Environment.GetEnvironmentVariable(PyDllEnvVar);

        if (string.IsNullOrWhiteSpace(preExisting))
        {
            Environment.SetEnvironmentVariable(PyDllEnvVar, null);
        }

        return configured;
    }

    /// <summary>
    /// Runs the base interpreter's <c>venv</c> module. <c>--without-pip</c> keeps it offline and
    /// fast; nothing here installs a package.
    /// </summary>
    /// <param name="baseInterpreter">The interpreter to create the environment with.</param>
    /// <param name="path">Where to create it.</param>
    /// <exception cref="InvalidOperationException">The interpreter could not be run, or it failed.</exception>
    private static void CreateVirtualEnvironment(string baseInterpreter, string path)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = baseInterpreter,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("-m");
        startInfo.ArgumentList.Add("venv");
        startInfo.ArgumentList.Add("--without-pip");
        startInfo.ArgumentList.Add(path);

        Process process;
        try
        {
            process = Process.Start(startInfo);
        }
        catch (Exception failure)
        {
            throw new InvalidOperationException(
                $"'{baseInterpreter}' could not be started to create a virtual environment.", failure);
        }

        if (process is null)
        {
            throw new InvalidOperationException(
                $"'{baseInterpreter}' could not be started to create a virtual environment.");
        }

        using (process)
        {
            // Both pipes are drained as the child writes them. Reading one to its end and only
            // then the other deadlocks whenever the child fills the pipe it is not being read
            // from, which is a real risk on Windows, where the venv module is chattier.
            var output = new StringBuilder();
            var error = new StringBuilder();
            process.OutputDataReceived += (_, line) =>
            {
                if (line.Data != null)
                {
                    output.AppendLine(line.Data);
                }
            };
            process.ErrorDataReceived += (_, line) =>
            {
                if (line.Data != null)
                {
                    error.AppendLine(line.Data);
                }
            };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // The parameterless overload also waits for those two readers to finish.
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"'{baseInterpreter} -m venv --without-pip {path}' failed with exit code"
                    + $" {process.ExitCode}.\n{output}\n{error}");
            }
        }

        if (!File.Exists(Path.Combine(path, "pyvenv.cfg")))
        {
            throw new InvalidOperationException(
                $"'{baseInterpreter} -m venv' reported success but wrote no pyvenv.cfg to '{path}'.");
        }
    }

    /// <summary>
    /// Drops a one-line module into the environment's site-packages. Importing it is the proof
    /// that the environment's own package folder reached <c>sys.path</c>.
    /// </summary>
    /// <param name="path">The virtual environment's root folder.</param>
    /// <exception cref="InvalidOperationException">The environment has no site-packages folder.</exception>
    private static void WriteMarkerModule(string path)
    {
        string sitePackages = Directory
            .EnumerateDirectories(path, "site-packages", SearchOption.AllDirectories)
            .OrderBy(directory => directory.Length)
            .FirstOrDefault();

        if (sitePackages is null)
        {
            throw new InvalidOperationException(
                $"The virtual environment at '{path}' has no site-packages folder.");
        }

        File.WriteAllText(
            Path.Combine(sitePackages, MarkerModuleName + ".py"),
            "MARKER = \"" + MarkerValue + "\"\n");
    }

    private static void DeleteTemporaryRoot()
    {
        if (_temporaryRoot is null || !Directory.Exists(_temporaryRoot))
        {
            return;
        }

        // Windows refuses to delete a file another handle still holds, and a handle the
        // interpreter opened while importing from this tree can outlive the shutdown by a
        // moment, so a couple of retries turn most of those into a clean delete.
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                Directory.Delete(_temporaryRoot, recursive: true);
                return;
            }
            catch (IOException) when (attempt < DeleteRetries)
            {
            }
            catch (UnauthorizedAccessException) when (attempt < DeleteRetries)
            {
            }
            catch (IOException)
            {
                // Still held: the folder is under the system temp directory and will be swept
                // up with it. This must not fail an otherwise passing run.
                return;
            }
            catch (UnauthorizedAccessException)
            {
                return;
            }

            Thread.Sleep(DeleteRetryDelayMilliseconds);
        }
    }
}
