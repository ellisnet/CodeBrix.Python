using System;
using System.IO;
using CodeBrix.Python;
using SilverAssertions;
using SilverAssertions.Collections;
using SilverAssertions.Numeric;
using SilverAssertions.Primitives;
using SilverAssertions.Specialized;
using Xunit;

namespace CodeBrix.Python.Tests;

/// <summary>
/// Tests for <see cref="PythonEngine.VirtualEnvironment"/> that do not need an interpreter of
/// their own: the argument validation, which happens before the engine's state is consulted so
/// that a malformed path is always reported as such, and the refusal to reconfigure a running
/// runtime. Actually starting an interpreter inside a virtual environment is the
/// CodeBrix.Python.VenvTests project's job, because an engine initializes once per process and
/// this assembly's engine is already up.
/// </summary>
public class PythonEngineTests : IDisposable
{
    private readonly string _root;

    /// <summary>Creates the temp folder the fake environments in this class are built under.</summary>
    public PythonEngineTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "codebrix-python-engine-" + Guid.NewGuid().ToString("N"));
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
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    [Fact]
    public void VirtualEnvironment_rejects_null()
    {
        //Arrange
        Action act = () => PythonEngine.VirtualEnvironment = null;

        //Act & Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void VirtualEnvironment_rejects_a_folder_that_does_not_exist()
    {
        //Arrange
        string missing = Path.Combine(_root, "missing");

        //Act
        Action act = () => PythonEngine.VirtualEnvironment = missing;

        //Assert
        act.Should().Throw<ArgumentException>().WithMessage("*" + missing + "*does not exist*");
    }

    [Fact]
    public void VirtualEnvironment_rejects_a_folder_without_a_pyvenv_cfg()
    {
        //Arrange
        string folder = Path.Combine(_root, "plain-folder");
        Directory.CreateDirectory(folder);

        //Act
        Action act = () => PythonEngine.VirtualEnvironment = folder;

        //Assert
        act.Should().Throw<ArgumentException>().WithMessage("*pyvenv.cfg*");
    }

    [Fact]
    public void VirtualEnvironment_rejects_a_pyvenv_cfg_without_a_home_key()
    {
        //Arrange
        string folder = Path.Combine(_root, "homeless");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "pyvenv.cfg"), "version = 3.13.5\n");

        //Act
        Action act = () => PythonEngine.VirtualEnvironment = folder;

        //Assert
        act.Should().Throw<ArgumentException>().WithMessage("*'home' key*");
    }

    [Fact]
    public void VirtualEnvironment_cannot_be_set_once_the_runtime_is_running()
    {
        //Arrange
        string venv = CreateFakeVirtualEnvironment();

        //Act
        Action act = () => PythonEngine.VirtualEnvironment = venv;

        //Assert
        PythonEngine.IsInitialized.Should().BeTrue();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*before runtime is initialized*");
    }

    [Fact]
    public void VirtualEnvironment_reports_the_environment_the_process_environment_named()
    {
        //Arrange
        string named = Environment.GetEnvironmentVariable("PYTHONNET_VENV");
        if (string.IsNullOrEmpty(named))
        {
            named = Environment.GetEnvironmentVariable("VIRTUAL_ENV");
        }
        bool discoverable = !string.IsNullOrEmpty(named) && PythonEnvironment.FromVenv(named) != null;

        //Act
        string reported = PythonEngine.VirtualEnvironment;

        //Assert
        if (discoverable)
        {
            reported.Should().Be(named);
        }
        else
        {
            reported.Should().BeNull();
        }
    }

    /// <summary>Creates a folder that passes every one of the setter's validation checks.</summary>
    private string CreateFakeVirtualEnvironment()
    {
        string home = Path.Combine(_root, "base", "bin");
        Directory.CreateDirectory(home);

        string venv = Path.Combine(_root, "venv");
        Directory.CreateDirectory(venv);
        File.WriteAllText(
            Path.Combine(venv, "pyvenv.cfg"),
            "home = " + home + "\nversion = 3.13.5\ninclude-system-site-packages = false\n");
        return venv;
    }
}
