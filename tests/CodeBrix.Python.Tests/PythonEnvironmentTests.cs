using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using CodeBrix.Python;
using SilverAssertions;
using SilverAssertions.Collections;
using SilverAssertions.Numeric;
using SilverAssertions.Primitives;
using SilverAssertions.Specialized;
using Xunit;

namespace CodeBrix.Python.Tests;

/// <summary>
/// Pure file-system tests for virtual-environment discovery. Nothing here starts an
/// interpreter: every case builds a throwaway tree in a temp folder that looks the way a
/// real base installation and a real virtual environment look, and checks what
/// <see cref="PythonEnvironment"/> makes of it. The end-to-end activation test lives in the
/// CodeBrix.Python.VenvTests project, because an interpreter can only be started once per
/// process.
/// </summary>
public class PythonEnvironmentTests : IDisposable
{
    private const string PyDllEnvVar = "PYTHONNET_PYDLL";
    private const string PyExeEnvVar = "PYTHONNET_PYEXE";
    private const string PyNetVenvEnvVar = "PYTHONNET_VENV";
    private const string VirtualEnvEnvVar = "VIRTUAL_ENV";

    private static readonly Version SampleVersion = new(3, 13, 5);

    private readonly string _root;

    /// <summary>Creates the temp folder every tree in this class is built under.</summary>
    public PythonEnvironmentTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "codebrix-python-env-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    /// <summary>Removes the temp folder.</summary>
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch (IOException)
        {
            // A stray handle on Windows must not fail an otherwise passing test.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    /// <summary>The search folders the running OS and process architecture actually probe.</summary>
    /// <returns>One theory case per relative search folder.</returns>
    public static TheoryData<string> LibrarySearchDirectories()
    {
        var data = new TheoryData<string>();
        foreach (string directory in PythonEnvironment.GetLibrarySearchDirectories())
        {
            data.Add(directory);
        }
        return data;
    }

    // ---------------------------------------------------------------------------------
    // FromVenv - reading pyvenv.cfg
    // ---------------------------------------------------------------------------------

    [Fact]
    public void FromVenv_reads_the_home_version_and_launcher_out_of_pyvenv_cfg()
    {
        //Arrange
        string home = CreateBaseInstallation("base");
        string venv = CreateVirtualEnvironment("venv", home, "3.13.5");

        //Act
        PythonEnvironment environment = PythonEnvironment.FromVenv(venv);

        //Assert
        environment.Should().NotBeNull();
        environment.VenvPath.Should().Be(venv);
        environment.Home.Should().Be(home);
        environment.Version.Should().Be(SampleVersion);
        environment.ProgramName.Should().Be(PythonEnvironment.ProgramNameFromPath(venv));
    }

    [Fact]
    public void FromVenv_reads_the_version_info_key_when_there_is_no_version_key()
    {
        //Arrange
        string home = CreateBaseInstallation("base");
        string venv = CreateVirtualEnvironment("venv", home, version: null, versionInfo: "3.13.5.final.0");

        //Act
        PythonEnvironment environment = PythonEnvironment.FromVenv(venv);

        //Assert
        environment.Version.Should().Be(SampleVersion);
    }

    [Theory]
    [InlineData("3.13.5", 3, 13)]
    [InlineData("3.13", 3, 13)]
    // What the virtualenv tool writes into version_info; System.Version rejects it outright,
    // which used to leave the environment with no version and therefore no libpython at all.
    [InlineData("3.13.5.final.0", 3, 13)]
    [InlineData("3.14.0.candidate.1", 3, 14)]
    public void ParseVersion_keeps_the_leading_numeric_components(string value, int major, int minor)
    {
        //Arrange
        Version parsed = PythonEnvironment.ParseVersion(value);

        //Act
        string libraryName = PythonEnvironment.GetDefaultDllName(parsed);

        //Assert
        parsed.Major.Should().Be(major);
        parsed.Minor.Should().Be(minor);
        libraryName.Should().Be(PythonEnvironment.GetDefaultDllName(new Version(major, minor)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("3")]
    [InlineData("final")]
    [InlineData("three.thirteen")]
    public void ParseVersion_returns_null_without_a_major_and_minor_number(string value)
        => PythonEnvironment.ParseVersion(value).Should().BeNull();

    [Fact]
    public void FromVenv_ignores_lines_that_are_not_key_equals_value()
    {
        //Arrange
        string home = CreateBaseInstallation("base");
        string venv = CreateVirtualEnvironment("venv", home, "3.13.5");
        File.AppendAllText(
            Path.Combine(venv, PythonEnvironment.VenvConfigFileName),
            "\n# a comment\n\nnot a setting\n");

        //Act
        PythonEnvironment environment = PythonEnvironment.FromVenv(venv);

        //Assert
        environment.Home.Should().Be(home);
    }

    [Fact]
    public void FromVenv_returns_null_when_the_folder_holds_no_pyvenv_cfg()
    {
        //Arrange
        string venv = Path.Combine(_root, "not-a-venv");
        Directory.CreateDirectory(venv);

        //Act
        PythonEnvironment environment = PythonEnvironment.FromVenv(venv);

        //Assert
        environment.Should().BeNull();
    }

    [Fact]
    public void FromVenv_returns_null_when_pyvenv_cfg_has_no_home_key()
    {
        //Arrange
        string venv = Path.Combine(_root, "homeless");
        Directory.CreateDirectory(venv);
        File.WriteAllText(
            Path.Combine(venv, PythonEnvironment.VenvConfigFileName),
            "version = 3.13.5\ninclude-system-site-packages = false\n");

        //Act
        PythonEnvironment environment = PythonEnvironment.FromVenv(venv);

        //Assert
        environment.Should().BeNull();
    }

    [Fact]
    public void FromVenv_leaves_the_program_name_unset_when_the_launcher_is_missing()
    {
        //Arrange
        string home = CreateBaseInstallation("base");
        string venv = CreateVirtualEnvironment("venv", home, "3.13.5", createLauncher: false);

        //Act
        PythonEnvironment environment = PythonEnvironment.FromVenv(venv);

        //Assert
        environment.ProgramName.Should().BeNull();
    }

    // ---------------------------------------------------------------------------------
    // FromVenv - locating libpython next to the base installation
    // ---------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(LibrarySearchDirectories))]
    public void FromVenv_finds_libpython_in_every_probed_search_directory(string searchDirectory)
    {
        //Arrange
        string home = CreateBaseInstallation("base-" + Sanitize(searchDirectory));
        string library = PlaceLibrary(home, searchDirectory, PythonEnvironment.GetDefaultDllName(SampleVersion));
        string venv = CreateVirtualEnvironment("venv-" + Sanitize(searchDirectory), home, "3.13.5");

        //Act
        PythonEnvironment environment = PythonEnvironment.FromVenv(venv);

        //Assert
        Path.GetFullPath(environment.LibPython).Should().Be(library);
    }

    [Fact]
    public void FromVenv_finds_a_free_threaded_libpython()
    {
        //Arrange
        string home = CreateBaseInstallation("base");
        string searchDirectory = PythonEnvironment.GetLibrarySearchDirectories()[0];
        string library = PlaceLibrary(
            home, searchDirectory, PythonEnvironment.GetDefaultDllName(SampleVersion, freeThreaded: true));
        string venv = CreateVirtualEnvironment("venv", home, "3.13.5");

        //Act
        PythonEnvironment environment = PythonEnvironment.FromVenv(venv);

        //Assert
        Path.GetFullPath(environment.LibPython).Should().Be(library);
    }

    [Fact]
    public void FromVenv_leaves_libpython_null_when_no_library_is_anywhere_near_home()
    {
        //Arrange
        string home = CreateBaseInstallation("base");
        string venv = CreateVirtualEnvironment("venv", home, "3.13.5");

        //Act
        PythonEnvironment environment = PythonEnvironment.FromVenv(venv);

        //Assert
        environment.LibPython.Should().BeNull();
    }

    [Fact]
    public void FromVenv_leaves_libpython_null_when_pyvenv_cfg_records_no_version()
    {
        //Arrange
        string home = CreateBaseInstallation("base");
        PlaceLibrary(home, PythonEnvironment.GetLibrarySearchDirectories()[0],
            PythonEnvironment.GetDefaultDllName(SampleVersion));
        string venv = CreateVirtualEnvironment("venv", home, version: null);

        //Act
        PythonEnvironment environment = PythonEnvironment.FromVenv(venv);

        //Assert
        environment.LibPython.Should().BeNull();
    }

    // ---------------------------------------------------------------------------------
    // The search folders themselves
    // ---------------------------------------------------------------------------------

    [Fact]
    public void GetLibrarySearchDirectories_probes_the_multiarch_folder_on_linux()
    {
        //Arrange
        IReadOnlyList<string> directories = PythonEnvironment.GetLibrarySearchDirectories();

        //Act
        bool probesMultiarch = false;
        foreach (string tuple in PythonEnvironment.GetLinuxMultiarchTuples(RuntimeInformation.ProcessArchitecture))
        {
            probesMultiarch |= directories.Contains("../lib/" + tuple);
        }

        //Assert
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            directories.Should().Contain("../lib");
            probesMultiarch.Should().BeTrue();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            directories.Should().Equal(new[] { "." });
        }
        else
        {
            directories.Should().Equal(new[] { "../lib" });
        }
    }

    [Theory]
    [InlineData(Architecture.X64, "x86_64-linux-gnu")]
    [InlineData(Architecture.X86, "i386-linux-gnu")]
    [InlineData(Architecture.Arm64, "aarch64-linux-gnu")]
    [InlineData(Architecture.Arm, "arm-linux-gnueabihf")]
    [InlineData(Architecture.RiscV64, "riscv64-linux-gnu")]
    [InlineData(Architecture.Ppc64le, "powerpc64le-linux-gnu")]
    [InlineData(Architecture.S390x, "s390x-linux-gnu")]
    [InlineData(Architecture.LoongArch64, "loongarch64-linux-gnu")]
    public void GetLinuxMultiarchTuples_maps_an_architecture_to_its_gnu_tuple(
        Architecture architecture, string expectedFirstTuple)
    {
        //Arrange
        IReadOnlyList<string> tuples = PythonEnvironment.GetLinuxMultiarchTuples(architecture);

        //Act
        string first = tuples.Count > 0 ? tuples[0] : null;

        //Assert
        first.Should().Be(expectedFirstTuple);
    }

    [Fact]
    public void GetLinuxMultiarchTuples_is_empty_for_an_architecture_with_no_gnu_tuple()
        => PythonEnvironment.GetLinuxMultiarchTuples(Architecture.Wasm).Should().BeEmpty();

    [Fact]
    public void GetDefaultDllName_uses_the_shared_library_naming_of_the_running_platform()
    {
        //Arrange
        string expected =
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "python313.dll"
            : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "libpython3.13.dylib"
            : "libpython3.13.so";

        //Act
        string name = PythonEnvironment.GetDefaultDllName(SampleVersion);

        //Assert
        name.Should().Be(expected);
    }

    [Fact]
    public void ProgramNameFromPath_names_the_launcher_the_running_platform_uses()
    {
        //Arrange
        string venv = Path.Combine(_root, "venv");
        string expected = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? Path.Combine(venv, "Scripts", "python.exe")
            : Path.Combine(venv, "bin", "python");

        //Act
        string launcher = PythonEnvironment.ProgramNameFromPath(venv);

        //Assert
        launcher.Should().Be(expected);
    }

    [Fact]
    public void GetDefaultDllName_appends_the_free_threaded_suffix()
    {
        //Arrange
        string ordinary = PythonEnvironment.GetDefaultDllName(SampleVersion);

        //Act
        string freeThreaded = PythonEnvironment.GetDefaultDllName(SampleVersion, freeThreaded: true);

        //Assert
        freeThreaded.Should().NotBe(ordinary);
        freeThreaded.Should().Contain("13t");
    }

    // ---------------------------------------------------------------------------------
    // FromEnv - environment-variable precedence
    // ---------------------------------------------------------------------------------

    [Fact]
    public void FromEnv_returns_an_unconfigured_environment_when_no_variable_is_set()
    {
        //Arrange
        using var _ = new EnvironmentVariables(
            (PyDllEnvVar, null), (PyExeEnvVar, null), (PyNetVenvEnvVar, null), (VirtualEnvEnvVar, null));

        //Act
        PythonEnvironment environment = PythonEnvironment.FromEnv();

        //Assert
        environment.VenvPath.Should().BeNull();
        environment.LibPython.Should().BeNull();
        environment.ProgramName.Should().BeNull();
        environment.IsValid.Should().BeFalse();
    }

    [Fact]
    public void FromEnv_prefers_PYTHONNET_VENV_over_VIRTUAL_ENV()
    {
        //Arrange
        string home = CreateBaseInstallation("base");
        string preferred = CreateVirtualEnvironment("preferred", home, "3.13.5");
        string ignored = CreateVirtualEnvironment("ignored", home, "3.13.5");
        using var _ = new EnvironmentVariables(
            (PyDllEnvVar, null), (PyExeEnvVar, null),
            (PyNetVenvEnvVar, preferred), (VirtualEnvEnvVar, ignored));

        //Act
        PythonEnvironment environment = PythonEnvironment.FromEnv();

        //Assert
        environment.VenvPath.Should().Be(preferred);
    }

    [Fact]
    public void FromEnv_falls_back_to_VIRTUAL_ENV()
    {
        //Arrange
        string home = CreateBaseInstallation("base");
        string venv = CreateVirtualEnvironment("venv", home, "3.13.5");
        using var _ = new EnvironmentVariables(
            (PyDllEnvVar, null), (PyExeEnvVar, null), (PyNetVenvEnvVar, null), (VirtualEnvEnvVar, venv));

        //Act
        PythonEnvironment environment = PythonEnvironment.FromEnv();

        //Assert
        environment.VenvPath.Should().Be(venv);
    }

    [Fact]
    public void FromEnv_ignores_a_venv_variable_that_names_something_that_is_not_a_venv()
    {
        //Arrange
        using var _ = new EnvironmentVariables(
            (PyDllEnvVar, null), (PyExeEnvVar, null), (PyNetVenvEnvVar, _root), (VirtualEnvEnvVar, null));

        //Act
        PythonEnvironment environment = PythonEnvironment.FromEnv();

        //Assert
        environment.VenvPath.Should().BeNull();
    }

    [Fact]
    public void FromEnv_lets_PYTHONNET_PYEXE_override_the_launcher_the_venv_supplies()
    {
        //Arrange
        string home = CreateBaseInstallation("base");
        string venv = CreateVirtualEnvironment("venv", home, "3.13.5");
        string launcher = Path.Combine(home, "python3");
        using var _ = new EnvironmentVariables(
            (PyDllEnvVar, null), (PyExeEnvVar, launcher), (PyNetVenvEnvVar, venv), (VirtualEnvEnvVar, null));

        //Act
        PythonEnvironment environment = PythonEnvironment.FromEnv();

        //Assert
        environment.ProgramName.Should().Be(launcher);
    }

    [Fact]
    public void FromEnv_lets_PYTHONNET_PYDLL_override_the_library_and_marks_it_explicit()
    {
        //Arrange
        string home = CreateBaseInstallation("base");
        PlaceLibrary(home, PythonEnvironment.GetLibrarySearchDirectories()[0],
            PythonEnvironment.GetDefaultDllName(SampleVersion));
        string venv = CreateVirtualEnvironment("venv", home, "3.13.5");
        string chosen = Path.Combine(_root, "chosen-libpython");
        using var _ = new EnvironmentVariables(
            (PyDllEnvVar, chosen), (PyExeEnvVar, null), (PyNetVenvEnvVar, venv), (VirtualEnvEnvVar, null));

        //Act
        PythonEnvironment environment = PythonEnvironment.FromEnv();

        //Assert
        environment.LibPython.Should().Be(chosen);
        environment.LibPythonIsExplicit.Should().BeTrue();
    }

    [Fact]
    public void FromEnv_does_not_mark_a_venv_derived_library_as_explicit()
    {
        //Arrange
        string home = CreateBaseInstallation("base");
        PlaceLibrary(home, PythonEnvironment.GetLibrarySearchDirectories()[0],
            PythonEnvironment.GetDefaultDllName(SampleVersion));
        string venv = CreateVirtualEnvironment("venv", home, "3.13.5");
        using var _ = new EnvironmentVariables(
            (PyDllEnvVar, null), (PyExeEnvVar, null), (PyNetVenvEnvVar, venv), (VirtualEnvEnvVar, null));

        //Act
        PythonEnvironment environment = PythonEnvironment.FromEnv();

        //Assert
        environment.LibPython.Should().NotBeNull();
        environment.LibPythonIsExplicit.Should().BeFalse();
    }

    // ---------------------------------------------------------------------------------
    // FromVenvOrThrow - the validating path the public API uses
    // ---------------------------------------------------------------------------------

    [Fact]
    public void FromVenvOrThrow_names_the_missing_folder()
    {
        //Arrange
        string missing = Path.Combine(_root, "missing");

        //Act
        Action act = () => PythonEnvironment.FromVenvOrThrow(missing, "venv");

        //Assert
        act.Should().Throw<ArgumentException>().WithMessage("*" + missing + "*does not exist*");
    }

    [Fact]
    public void FromVenvOrThrow_names_the_missing_config_file()
    {
        //Arrange
        string folder = Path.Combine(_root, "plain-folder");
        Directory.CreateDirectory(folder);

        //Act
        Action act = () => PythonEnvironment.FromVenvOrThrow(folder, "venv");

        //Assert
        act.Should().Throw<ArgumentException>().WithMessage("*" + PythonEnvironment.VenvConfigFileName + "*");
    }

    [Fact]
    public void FromVenvOrThrow_names_the_missing_home_key()
    {
        //Arrange
        string folder = Path.Combine(_root, "homeless");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, PythonEnvironment.VenvConfigFileName), "version = 3.13.5\n");

        //Act
        Action act = () => PythonEnvironment.FromVenvOrThrow(folder, "venv");

        //Assert
        act.Should().Throw<ArgumentException>().WithMessage("*'home' key*");
    }

    [Fact]
    public void FromVenvOrThrow_rejects_an_empty_path()
    {
        //Arrange
        Action act = () => PythonEnvironment.FromVenvOrThrow("   ", "venv");

        //Act & Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void FromVenvOrThrow_supplies_a_launcher_even_when_the_venv_has_none_yet()
    {
        //Arrange
        string home = CreateBaseInstallation("base");
        string venv = CreateVirtualEnvironment("venv", home, "3.13.5", createLauncher: false);

        //Act
        PythonEnvironment environment = PythonEnvironment.FromVenvOrThrow(venv, "venv");

        //Assert
        environment.ProgramName.Should().Be(PythonEnvironment.ProgramNameFromPath(venv));
    }

    // ---------------------------------------------------------------------------------
    // Path comparison and post-startup verification
    // ---------------------------------------------------------------------------------

    [Fact]
    public void SamePath_ignores_a_trailing_separator()
        => PythonEnvironment.SamePath(_root, _root + Path.DirectorySeparatorChar).Should().BeTrue();

    [Fact]
    public void SamePath_ignores_a_redundant_dot_segment()
        => PythonEnvironment.SamePath(_root, Path.Combine(_root, ".")).Should().BeTrue();

    [Fact]
    public void SamePath_ignores_the_macOS_private_prefix_where_macOS_has_one()
    {
        //Arrange
        // On macOS /tmp, /var and /etc are symbolic links into /private, so CPython reports a
        // prefix under /private that .NET never spells that way. Everywhere else the two are
        // simply different folders, and this must stay false there.
        bool expected = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

        //Act
        bool same = PythonEnvironment.SamePath("/private/tmp/codebrix-python", "/tmp/codebrix-python");

        //Assert
        same.Should().Be(expected);
    }

    [Fact]
    public void SamePath_is_false_for_two_different_folders()
        => PythonEnvironment.SamePath(_root, Path.Combine(_root, "elsewhere")).Should().BeFalse();

    [Fact]
    public void SamePath_is_false_when_either_side_is_empty()
    {
        //Arrange & Act & Assert
        PythonEnvironment.SamePath(null, _root).Should().BeFalse();
        PythonEnvironment.SamePath(_root, "").Should().BeFalse();
    }

    [Fact]
    public void VerifyActivated_does_nothing_when_no_virtual_environment_was_configured()
    {
        //Arrange
        var environment = new PythonEnvironment();

        //Act
        Action act = () => environment.VerifyActivated("/somewhere/else", "/somewhere/else/bin/python");

        //Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void VerifyActivated_accepts_a_prefix_that_names_the_configured_environment()
    {
        //Arrange
        string home = CreateBaseInstallation("base");
        string venv = CreateVirtualEnvironment("venv", home, "3.13.5");
        PythonEnvironment environment = PythonEnvironment.FromVenv(venv);

        //Act
        Action act = () => environment.VerifyActivated(
            venv + Path.DirectorySeparatorChar, Path.Combine(venv, "bin", "python"));

        //Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void VerifyActivated_reports_the_environment_the_prefix_and_the_executable_on_a_mismatch()
    {
        //Arrange
        string home = CreateBaseInstallation("base");
        string venv = CreateVirtualEnvironment("venv", home, "3.13.5");
        PythonEnvironment environment = PythonEnvironment.FromVenv(venv);

        //Act
        Action act = () => environment.VerifyActivated("/usr", "/usr/bin/python3");

        //Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*" + venv + "*/usr*/usr/bin/python3*PYTHONHOME*");
    }

    // ---------------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------------

    /// <summary>Creates a "&lt;root&gt;/&lt;name&gt;/bin" folder to stand in for a base interpreter's home.</summary>
    private string CreateBaseInstallation(string name)
    {
        string home = Path.Combine(_root, name, "bin");
        Directory.CreateDirectory(home);
        return home;
    }

    /// <summary>Creates an empty file named like a libpython in one of the probed search folders.</summary>
    private static string PlaceLibrary(string home, string searchDirectory, string fileName)
    {
        string directory = Path.GetFullPath(Path.Combine(home, searchDirectory));
        Directory.CreateDirectory(directory);
        string library = Path.Combine(directory, fileName);
        File.WriteAllBytes(library, Array.Empty<byte>());
        return library;
    }

    /// <summary>Creates a folder that looks like a virtual environment, launcher included.</summary>
    private string CreateVirtualEnvironment(
        string name, string home, string version, string versionInfo = null, bool createLauncher = true)
    {
        string venv = Path.Combine(_root, name);
        Directory.CreateDirectory(venv);

        var config = "home = " + home + "\ninclude-system-site-packages = false\n";
        if (version != null)
        {
            config += "version = " + version + "\n";
        }
        if (versionInfo != null)
        {
            config += "version_info = " + versionInfo + "\n";
        }
        File.WriteAllText(Path.Combine(venv, PythonEnvironment.VenvConfigFileName), config);

        if (createLauncher)
        {
            string launcher = PythonEnvironment.ProgramNameFromPath(venv);
            Directory.CreateDirectory(Path.GetDirectoryName(launcher));
            File.WriteAllBytes(launcher, Array.Empty<byte>());
        }

        return venv;
    }

    private static string Sanitize(string searchDirectory)
        => searchDirectory.Replace("/", "-").Replace("\\", "-").Replace(".", "dot");

    /// <summary>
    /// Sets environment variables for the current process only and puts every one of them
    /// back on disposal, whatever the test does in between.
    /// </summary>
    private sealed class EnvironmentVariables : IDisposable
    {
        private readonly List<KeyValuePair<string, string>> _previous = new();

        public EnvironmentVariables(params (string Name, string Value)[] values)
        {
            foreach ((string name, string value) in values)
            {
                _previous.Add(new KeyValuePair<string, string>(name, Environment.GetEnvironmentVariable(name)));
                Environment.SetEnvironmentVariable(name, value);
            }
        }

        public void Dispose()
        {
            foreach (KeyValuePair<string, string> entry in _previous)
            {
                Environment.SetEnvironmentVariable(entry.Key, entry.Value);
            }
        }
    }
}
