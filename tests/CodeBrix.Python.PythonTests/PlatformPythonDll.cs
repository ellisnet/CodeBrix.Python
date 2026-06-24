using System;
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
///   <item><b>Linux</b>: a deliberate no-op &#8212; libpython keeps resolving through the
///   existing virtual-environment / <c>PYTHONNET_PYDLL</c> mechanism, unchanged.</item>
/// </list>
/// On Windows and macOS an already-set <c>PYTHONNET_PYDLL</c> always takes precedence.
/// The mechanism mirrors the PythonNetTest01 sample.
/// </summary>
internal static class PlatformPythonDll
{
    private const string PyDllEnvVar = "PYTHONNET_PYDLL";
    private const string ConfigFileName = "appsettings.json";
    private const string WindowsConfigKey = "PythonDllPath";
    private const string MacOsConfigKey = "PythonMacOsPath";

    /// <summary>
    /// On Windows and macOS, ensures <c>PYTHONNET_PYDLL</c> is set (from the matching
    /// path key in <c>appsettings.json</c>) before <c>PythonEngine.Initialize()</c> runs.
    /// An already-set <c>PYTHONNET_PYDLL</c> always takes precedence. No-op on Linux.
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

        // Linux: leave libpython discovery to the existing venv / env-var path, unchanged.
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
