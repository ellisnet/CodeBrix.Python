using System;
using System.IO;
using System.Runtime.InteropServices;
using CodeBrix.Python;
using SilverAssertions;
using SilverAssertions.Collections;
using SilverAssertions.Numeric;
using SilverAssertions.Primitives;
using SilverAssertions.Specialized;
using Xunit;

namespace CodeBrix.Python.Tests.Venv;

/// <summary>
/// End-to-end proof that <see cref="PythonEngine.VirtualEnvironment"/> actually activates an
/// environment: the interpreter this assembly runs reports the throwaway environment as its
/// prefix, runs that environment's launcher, and imports a module that exists only in that
/// environment's site-packages.
/// </summary>
public class PythonEngineTests
{
    private static string Evaluate(string expression)
    {
        GlobalTestsSetup.EnsureEngine();
        using var pyState = Py.GIL();
        using var pyScope = Py.CreateScope();
        pyScope.Exec("import sys");
        using PyObject result = pyScope.Eval(expression);
        return result.ToString();
    }

    [Fact]
    public void VirtualEnvironment_becomes_the_interpreter_prefix()
    {
        //Arrange
        string expected = GlobalTestsSetup.VirtualEnvironmentPath;

        //Act
        string prefix = Evaluate("sys.prefix");

        //Assert
        SamePath(prefix, expected).Should().BeTrue(
            "sys.prefix was '" + prefix + "' but the configured environment is '" + expected + "'");
    }

    [Fact]
    public void VirtualEnvironment_leaves_the_base_installation_as_the_base_prefix()
    {
        //Arrange
        string venv = GlobalTestsSetup.VirtualEnvironmentPath;

        //Act
        string basePrefix = Evaluate("sys.base_prefix");

        //Assert
        SamePath(basePrefix, venv).Should().BeFalse();
    }

    [Fact]
    public void VirtualEnvironment_becomes_the_interpreter_executable()
    {
        //Arrange
        string venv = GlobalTestsSetup.VirtualEnvironmentPath;

        //Act
        string executable = Evaluate("sys.executable");

        //Assert
        bool insideEnvironment = Normalize(executable)
            .StartsWith(Normalize(venv) + Path.DirectorySeparatorChar, PathComparison);
        insideEnvironment.Should().BeTrue(
            "sys.executable was '" + executable + "' but the configured environment is '" + venv + "'");
    }

    [Fact]
    public void VirtualEnvironment_puts_its_own_site_packages_on_the_import_path()
    {
        //Arrange
        GlobalTestsSetup.EnsureEngine();

        //Act
        using var pyState = Py.GIL();
        using var pyScope = Py.CreateScope();
        pyScope.Exec("import " + GlobalTestsSetup.MarkerModule);
        using PyObject marker = pyScope.Eval(GlobalTestsSetup.MarkerModule + ".MARKER");

        //Assert
        marker.As<string>().Should().Be(GlobalTestsSetup.MarkerValue);
    }

    [Fact]
    public void VirtualEnvironment_getter_reports_the_environment_that_was_configured()
    {
        //Arrange
        GlobalTestsSetup.EnsureEngine();

        //Act
        string reported = PythonEngine.VirtualEnvironment;

        //Assert
        reported.Should().Be(GlobalTestsSetup.VirtualEnvironmentPath);
    }

    [Fact]
    public void VirtualEnvironment_resolves_libpython_from_the_environments_own_configuration()
    {
        //Arrange
        GlobalTestsSetup.EnsureEngine();

        //Act
        string libPython = Runtime.PythonDLL;

        //Assert
        libPython.Should().NotBeNullOrWhiteSpace();
        File.Exists(libPython).Should().BeTrue("resolved libpython '" + libPython + "' should exist");
    }

    /// <summary>
    /// How paths compare on the running operating system: Windows and macOS use
    /// case-insensitive file systems by default, Linux does not.
    /// </summary>
    private static StringComparison PathComparison =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    /// <summary>
    /// Puts a path into a form that can be compared as text. CPython reports paths as it resolved
    /// them, which is not always spelled the way .NET spelled them: on macOS the system temp
    /// folder is reached through /var, /tmp and /etc symbolic links into /private, so sys.prefix
    /// comes back with a /private prefix that Path.GetFullPath does not add or remove.
    /// </summary>
    /// <param name="path">The path to normalize.</param>
    /// <returns>The full path, without a /private prefix or a trailing separator.</returns>
    private static string Normalize(string path)
    {
        string full = Path.GetFullPath(path);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
            && full.StartsWith("/private/", StringComparison.Ordinal))
        {
            full = full.Substring("/private".Length);
        }

        string trimmed = full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return trimmed.Length == 0 ? full : trimmed;
    }

    private static bool SamePath(string left, string right)
        => string.Equals(Normalize(left), Normalize(right), PathComparison);
}
