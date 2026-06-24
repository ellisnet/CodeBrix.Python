using System.Runtime.InteropServices;

namespace CodeBrix.Python.Tests;

/// <summary>
/// Builds a platform-specific "how to install this Python package" hint for test failure messages.
/// NOTE: mirrored in the sibling CodeBrix.Python.PythonTests project; keep the two copies in sync.
/// </summary>
internal static class MissingPythonPackage
{
    /// <summary>
    /// Returns a sentence describing how to install <paramref name="package"/> into the Python
    /// runtime these tests use, tailored to the operating system the tests are running on.
    /// </summary>
    public static string InstallHint(string package)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return $"On Windows, the missing library can be installed with: python -m pip install {package}.";
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return $"On macOS, the missing library can be installed with: python3 -m pip install {package}.";
        }
        return $"On Linux, the missing library can be installed with: pip install {package}.";
    }
}
