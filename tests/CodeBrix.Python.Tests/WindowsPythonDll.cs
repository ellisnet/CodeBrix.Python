using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace CodeBrix.Python.Tests;

/// <summary>
/// Windows-only test bootstrapping helper. Reads a libpython DLL path from
/// <c>appsettings.json</c> (key <c>PythonDllPath</c>) and exports it as the
/// <c>PYTHONNET_PYDLL</c> environment variable for the current process only, so the
/// embedded interpreter can locate CPython without anyone configuring a persistent
/// machine/user environment variable.
/// <para>
/// On Linux and macOS this is a deliberate no-op: those platforms keep resolving
/// libpython through the existing virtual-environment / <c>PYTHONNET_PYDLL</c>
/// mechanism, unchanged. The mechanism mirrors the PythonNetTest01 sample.
/// </para>
/// </summary>
internal static class WindowsPythonDll
{
    private const string PyDllEnvVar = "PYTHONNET_PYDLL";
    private const string ConfigFileName = "appsettings.json";
    private const string ConfigKey = "PythonDllPath";

    /// <summary>
    /// On Windows, ensures <c>PYTHONNET_PYDLL</c> is set (from the <c>PythonDllPath</c>
    /// value in <c>appsettings.json</c>) before <c>PythonEngine.Initialize()</c> runs.
    /// An already-set <c>PYTHONNET_PYDLL</c> always takes precedence. No-op off Windows.
    /// </summary>
    public static void EnsureSet()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Linux/macOS: leave libpython discovery to the existing venv / env-var path.
            return;
        }

        // An explicit PYTHONNET_PYDLL (CI, shell, or a persistent variable) wins.
        var existing = Environment.GetEnvironmentVariable(PyDllEnvVar);
        if (!string.IsNullOrWhiteSpace(existing))
        {
            return;
        }

        var dllPath = ReadConfiguredDllPath();

        if (string.IsNullOrWhiteSpace(dllPath))
        {
            throw new InvalidOperationException(
                $"Running the tests on Windows requires a libpython DLL path. Set the '{ConfigKey}'"
                + $" value in '{ConfigFileName}' (copied next to the test assembly) to your"
                + " python3XX.dll, e.g. \"C:\\\\Program Files\\\\Python314\\\\python314.dll\""
                + $" - or set the {PyDllEnvVar} environment variable.");
        }

        if (!File.Exists(dllPath))
        {
            throw new InvalidOperationException(
                $"The '{ConfigKey}' value in '{ConfigFileName}' points to a file that does not exist:\n"
                + dllPath);
        }

        // Process-scoped only: this does NOT modify the user or system environment.
        Environment.SetEnvironmentVariable(PyDllEnvVar, dllPath);
    }

    private static string? ReadConfiguredDllPath()
    {
        var configPath = Path.Combine(AppContext.BaseDirectory, ConfigFileName);
        if (!File.Exists(configPath))
        {
            return null;
        }

        using var stream = File.OpenRead(configPath);
        using var doc = JsonDocument.Parse(stream);

        if (doc.RootElement.ValueKind == JsonValueKind.Object
            && doc.RootElement.TryGetProperty(ConfigKey, out var value)
            && value.ValueKind == JsonValueKind.String)
        {
            return value.GetString()?.Trim();
        }

        return null;
    }
}
