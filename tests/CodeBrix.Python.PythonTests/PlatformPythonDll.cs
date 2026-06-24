using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace CodeBrix.Python.Tests;

// NOTE: This is intentionally identical to the copy in the sibling CodeBrix.Python.Tests
// project. Both .Tests projects locate libpython the same way; keep the two copies in sync.

/// <summary>
/// Platform-specific test bootstrapping helper. Before <c>PythonEngine.Initialize()</c>
/// runs, it points the embedded interpreter at a libpython shared library by exporting
/// the <c>PYTHONNET_PYDLL</c> environment variable for the current process only, so no
/// one has to configure a persistent machine/user environment variable.
/// <list type="bullet">
///   <item><b>Windows</b>: reads the libpython path from <c>appsettings.json</c>
///   (key <c>PythonDllPath</c>, e.g. a <c>python3XX.dll</c>).</item>
///   <item><b>macOS</b>: reads the libpython path from <c>appsettings.json</c>
///   (key <c>PythonMacOsPath</c>, e.g. a <c>libpython3.XX.dylib</c>).</item>
///   <item><b>Linux</b>: auto-discovers libpython by asking the system <c>python3</c>
///   for its own <c>sysconfig</c> LIBDIR/INSTSONAME &#8212; no hard-coded path and nothing
///   to configure, because the interpreter reports its own build layout.</item>
/// </list>
/// On every platform an already-set <c>PYTHONNET_PYDLL</c> always takes precedence.
/// The Windows/macOS mechanism mirrors the PythonNetTest01 sample.
/// </summary>
internal static class PlatformPythonDll
{
    private const string PyDllEnvVar = "PYTHONNET_PYDLL";
    private const string ConfigFileName = "appsettings.json";
    private const string WindowsConfigKey = "PythonDllPath";
    private const string MacOsConfigKey = "PythonMacOsPath";
    private const string LinuxPythonCommand = "python3";

    /// <summary>
    /// Ensures <c>PYTHONNET_PYDLL</c> is set before <c>PythonEngine.Initialize()</c> runs:
    /// on Windows and macOS from the matching path key in <c>appsettings.json</c>, and on
    /// Linux by auto-discovering the system <c>python3</c>'s libpython. An already-set
    /// <c>PYTHONNET_PYDLL</c> always takes precedence.
    /// </summary>
    public static void EnsureSet()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            EnsureSetFromConfig(
                platformName: "Windows",
                configKey: WindowsConfigKey,
                exampleHint: "python3XX.dll, e.g. \"C:\\\\Program Files\\\\Python314\\\\python314.dll\"");
            return;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            EnsureSetFromConfig(
                platformName: "macOS",
                configKey: MacOsConfigKey,
                exampleHint: "libpython3.XX.dylib, e.g."
                    + " \"/Library/Frameworks/Python.framework/Versions/3.14/lib/libpython3.14.dylib\"");
            return;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            EnsureSetFromAutoDiscovery();
            return;
        }
    }

    private static void EnsureSetFromConfig(string platformName, string configKey, string exampleHint)
    {
        // An explicit PYTHONNET_PYDLL (CI, shell, or a persistent variable) wins.
        var existing = Environment.GetEnvironmentVariable(PyDllEnvVar);
        if (!string.IsNullOrWhiteSpace(existing))
        {
            return;
        }

        var dllPath = ReadConfiguredDllPath(configKey);

        if (string.IsNullOrWhiteSpace(dllPath))
        {
            throw new InvalidOperationException(
                $"Running the tests on {platformName} requires a libpython path. Set the '{configKey}'"
                + $" value in '{ConfigFileName}' (copied next to the test assembly) to your {exampleHint}"
                + $" - or set the {PyDllEnvVar} environment variable.");
        }

        if (!File.Exists(dllPath))
        {
            throw new InvalidOperationException(
                $"The '{configKey}' value in '{ConfigFileName}' points to a file that does not exist:\n"
                + dllPath);
        }

        // Process-scoped only: this does NOT modify the user or system environment.
        Environment.SetEnvironmentVariable(PyDllEnvVar, dllPath);
    }

    private static void EnsureSetFromAutoDiscovery()
    {
        // An explicit PYTHONNET_PYDLL (CI, shell, or a persistent variable) wins.
        var existing = Environment.GetEnvironmentVariable(PyDllEnvVar);
        if (!string.IsNullOrWhiteSpace(existing))
        {
            return;
        }

        var dllPath = DiscoverLinuxLibPython();

        if (string.IsNullOrWhiteSpace(dllPath))
        {
            throw new InvalidOperationException(
                "Running the tests on Linux requires a loadable libpython shared library, which"
                + $" could not be auto-discovered by asking '{LinuxPythonCommand}' for its sysconfig"
                + " LIBDIR/INSTSONAME. Ensure '" + LinuxPythonCommand + "' is on PATH and a libpython"
                + " (e.g. libpython3.13.so.1.0) is installed - or set the " + PyDllEnvVar
                + " environment variable to its path.");
        }

        if (!File.Exists(dllPath))
        {
            throw new InvalidOperationException(
                $"The libpython path auto-discovered via '{LinuxPythonCommand}' does not exist:\n"
                + dllPath + $"\nSet the {PyDllEnvVar} environment variable to a valid libpython path.");
        }

        // Process-scoped only: this does NOT modify the user or system environment.
        Environment.SetEnvironmentVariable(PyDllEnvVar, dllPath);
    }

    private static string? DiscoverLinuxLibPython()
    {
        // Ask the system Python where its OWN embeddable shared library lives. sysconfig is
        // part of the standard library (no extra packages needed); INSTSONAME is the runtime
        // SONAME (e.g. libpython3.13.so.1.0) and LDLIBRARY the dev symlink - prefer whichever
        // exists on disk. This adapts automatically to the distro, Python minor version, and
        // CPU architecture, so nothing has to be hard-coded.
        const string script =
            "import os, sysconfig\n"
            + "libdir = sysconfig.get_config_var('LIBDIR') or ''\n"
            + "for key in ('INSTSONAME', 'LDLIBRARY'):\n"
            + "    name = sysconfig.get_config_var(key) or ''\n"
            + "    path = os.path.join(libdir, name)\n"
            + "    if name and os.path.exists(path):\n"
            + "        print(path)\n"
            + "        break\n";

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = LinuxPythonCommand,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            startInfo.ArgumentList.Add("-c");
            startInfo.ArgumentList.Add(script);

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return null;
            }

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode == 0 ? output.Trim() : null;
        }
        catch
        {
            // python3 not on PATH, or the probe failed - the caller raises a clear error.
            return null;
        }
    }

    private static string? ReadConfiguredDllPath(string configKey)
    {
        var configPath = Path.Combine(AppContext.BaseDirectory, ConfigFileName);
        if (!File.Exists(configPath))
        {
            return null;
        }

        using var stream = File.OpenRead(configPath);
        using var doc = JsonDocument.Parse(stream);

        if (doc.RootElement.ValueKind == JsonValueKind.Object
            && doc.RootElement.TryGetProperty(configKey, out var value)
            && value.ValueKind == JsonValueKind.String)
        {
            return value.GetString()?.Trim();
        }

        return null;
    }
}
