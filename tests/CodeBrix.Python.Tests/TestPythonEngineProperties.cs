using System;
using Xunit;
using SilverAssertions;
using SilverAssertions.Collections;
using SilverAssertions.Numeric;
using SilverAssertions.Primitives;
using SilverAssertions.Specialized;
using CodeBrix.Python;

namespace CodeBrix.Python.Tests; //was previously: Python.EmbeddingTest;

public class TestPythonEngineProperties
{
    [Fact]
    public static void GetBuildinfoDoesntCrash()
    {
        using (Py.GIL())
        {
            string s = PythonEngine.BuildInfo;

            Assert.True(s.Length > 5);
            Assert.Contains(",", s);
        }
    }

    [Fact]
    public static void GetCompilerDoesntCrash()
    {
        using (Py.GIL())
        {
            string s = PythonEngine.Compiler;

            Assert.True(s.Length > 0);
            Assert.Contains("[", s);
            Assert.Contains("]", s);
        }
    }

    [Fact]
    public static void GetCopyrightDoesntCrash()
    {
        using (Py.GIL())
        {
            string s = PythonEngine.Copyright;

            Assert.True(s.Length > 0);
            Assert.Contains("Python Software Foundation", s);
        }
    }

    [Fact]
    public static void GetPlatformDoesntCrash()
    {
        using (Py.GIL())
        {
            string s = PythonEngine.Platform;

            Assert.True(s.Length > 0);
            Assert.True(s.Contains("x") || s.Contains("win"));
        }
    }

    [Fact]
    public static void GetVersionDoesntCrash()
    {
        using (Py.GIL())
        {
            string s = PythonEngine.Version;

            Assert.True(s.Length > 0);
            Assert.Contains(",", s);
        }
    }

    [Fact]
    public static void GetPythonPathDefault()
    {
        string s = PythonEngine.PythonPath;

        Assert.Contains("python", s.ToLower());
    }

    [Fact]
    public static void GetProgramNameDefault()
    {
        string s = PythonEngine.ProgramName;

        Assert.NotNull(s);
    }

    /// <summary>
    /// Test default behavior of PYTHONHOME. If ENVVAR is set it will
    /// return the same value. If not, returns EmptyString.
    /// </summary>
    [Fact]
    public static void GetPythonHomeDefault()
    {
        string envPythonHome = Environment.GetEnvironmentVariable("PYTHONHOME") ?? "";

        string enginePythonHome = PythonEngine.PythonHome;

        Assert.Equal(envPythonHome, enginePythonHome);
    }
}
