using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using static System.FormattableString;

namespace CodeBrix.Python; //was previously: Python.Runtime;


internal class PythonEnvironment
{
    readonly static string PYDLL_ENV_VAR = "PYTHONNET_PYDLL";
    readonly static string PYEXE_ENV_VAR = "PYTHONNET_PYEXE";
    readonly static string PYNET_VENV_ENV_VAR = "PYTHONNET_VENV";
    readonly static string VENV_ENV_VAR = "VIRTUAL_ENV";

    // The file every virtual environment carries at its root; its "home" key names the
    // folder holding the base interpreter that created it.
    internal const string VenvConfigFileName = "pyvenv.cfg";

    public string? VenvPath { get; private set; }
    public string? Home { get; private set; }
    public Version? Version { get; private set; }
    public string? ProgramName { get; set; }
    public string? LibPython { get; set; }

    /// <summary>
    /// True when <see cref="LibPython"/> was named outright - by assigning
    /// <see cref="Runtime.PythonDLL"/> or by setting PYTHONNET_PYDLL - rather than being
    /// derived from a virtual environment's pyvenv.cfg. An explicitly named libpython
    /// survives a later <see cref="PythonEngine.VirtualEnvironment"/> assignment; a derived
    /// one does not, because the code-configured environment replaces it wholesale.
    /// </summary>
    public bool LibPythonIsExplicit { get; set; }

    public bool IsValid =>
        !string.IsNullOrEmpty(ProgramName) && !string.IsNullOrEmpty(LibPython);


    // TODO: Move the lib-guessing step to separate function, use together with
    // PYTHONNET_PYEXE or a path lookup as last resort

    // Initialize PythonEnvironment instance from environment variables.
    //
    // If PYTHONNET_PYEXE and PYTHONNET_PYDLL are set, these always have precedence.
    // If PYTHONNET_VENV or VIRTUAL_ENV is set, we interpret the environment as a venv
    // and set the ProgramName/LibPython accordingly. PYTHONNET_VENV takes precedence.
    public static PythonEnvironment FromEnv()
    {
        var pydll = Environment.GetEnvironmentVariable(PYDLL_ENV_VAR);
        var pydllSet = !string.IsNullOrEmpty(pydll);
        var pyexe = Environment.GetEnvironmentVariable(PYEXE_ENV_VAR);
        var pyexeSet = !string.IsNullOrEmpty(pyexe);
        var pynetVenv = Environment.GetEnvironmentVariable(PYNET_VENV_ENV_VAR);
        var pynetVenvSet = !string.IsNullOrEmpty(pynetVenv);
        var venv = Environment.GetEnvironmentVariable(VENV_ENV_VAR);
        var venvSet = !string.IsNullOrEmpty(venv);

        PythonEnvironment? res = new();

        if (pynetVenvSet)
            res = FromVenv(pynetVenv) ?? res;
        else if (venvSet)
            res = FromVenv(venv) ?? res;

        if (pyexeSet)
            res.ProgramName = pyexe;

        if (pydllSet)
        {
            res.LibPython = pydll;
            res.LibPythonIsExplicit = true;
        }

        return res;
    }

    public static PythonEnvironment? FromVenv(string path)
    {
        var env = new PythonEnvironment
        {
            VenvPath = path
        };

        string venvCfg = Path.Combine(path, VenvConfigFileName);

        if (!File.Exists(venvCfg))
            return null;

        var settings = TryParse(venvCfg);

        if (!settings.ContainsKey("home"))
            return null;

        env.Home = settings["home"];
        var pname = ProgramNameFromPath(path);
        if (File.Exists(pname))
            env.ProgramName = pname;

        if (settings.TryGetValue("version", out string versionStr))
        {
            env.Version = ParseVersion(versionStr);
        }
        else if (settings.TryGetValue("version_info", out versionStr))
        {
            env.Version = ParseVersion(versionStr);
        }

        env.LibPython = FindLibPython(env.Home, env.Version);

        return env;
    }

    /// <summary>
    /// Validating counterpart of <see cref="FromVenv"/>, used when a virtual environment is
    /// named from code rather than discovered from the process environment. It explains
    /// precisely what is missing instead of silently returning <c>null</c>, and it always
    /// produces a program name, so the interpreter is pointed at the environment's launcher
    /// even on a tree whose launcher has not been created yet.
    /// </summary>
    /// <param name="path">The virtual environment's root folder.</param>
    /// <param name="paramName">The name of the caller's parameter or property, for the exception.</param>
    /// <returns>The environment described by the folder's pyvenv.cfg.</returns>
    /// <exception cref="ArgumentException">
    /// The folder does not exist, holds no pyvenv.cfg, or that file has no <c>home</c> key.
    /// </exception>
    internal static PythonEnvironment FromVenvOrThrow(string path, string paramName)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("The virtual environment path is empty.", paramName);
        }

        string fullPath = Path.GetFullPath(path);

        if (!Directory.Exists(fullPath))
        {
            throw new ArgumentException(
                Invariant($"There is no virtual environment at '{fullPath}': that folder does not exist."),
                paramName);
        }

        string venvCfg = Path.Combine(fullPath, VenvConfigFileName);
        if (!File.Exists(venvCfg))
        {
            throw new ArgumentException(
                Invariant($"'{fullPath}' is not a virtual environment: it contains no '{VenvConfigFileName}'."),
                paramName);
        }

        if (!TryParse(venvCfg).TryGetValue("home", out string home) || string.IsNullOrWhiteSpace(home))
        {
            throw new ArgumentException(
                Invariant($"'{venvCfg}' has no 'home' key naming the base interpreter's folder,")
                    + " so the virtual environment cannot be used.",
                paramName);
        }

        PythonEnvironment? env = FromVenv(fullPath);
        if (env is null)
        {
            // Unreachable: the three checks above are exactly what FromVenv rejects.
            throw new ArgumentException(
                Invariant($"'{fullPath}' could not be read as a virtual environment."),
                paramName);
        }

        env.ProgramName ??= ProgramNameFromPath(fullPath);
        return env;
    }

    /// <summary>
    /// Confirms, once CPython has started, that a configured virtual environment actually
    /// took effect. Called with the freshly initialized interpreter's <c>sys.prefix</c> and
    /// <c>sys.executable</c>; does nothing when no virtual environment was configured.
    /// </summary>
    /// <param name="observedPrefix">The interpreter's <c>sys.prefix</c>.</param>
    /// <param name="observedExecutable">The interpreter's <c>sys.executable</c>.</param>
    /// <exception cref="InvalidOperationException">
    /// The interpreter started outside the requested virtual environment.
    /// </exception>
    internal void VerifyActivated(string? observedPrefix, string? observedExecutable)
    {
        string? venv = VenvPath;
        if (string.IsNullOrEmpty(venv))
        {
            return;
        }

        if (SamePath(observedPrefix, venv))
        {
            return;
        }

        throw new InvalidOperationException(
            Invariant($"The virtual environment '{venv}' was configured, but the interpreter started outside it:")
            + Invariant($" sys.prefix is '{observedPrefix ?? "<unset>"}' and sys.executable is")
            + Invariant($" '{observedExecutable ?? "<unset>"}'.")
            + " The two usual causes are a PYTHONHOME environment variable, which overrides the"
            + " environment's own prefix, and a libpython belonging to a different base installation"
            + Invariant($" than the one named by the 'home' key of '{Path.Combine(venv!, VenvConfigFileName)}'.")
            + " Assigning PythonEngine.ProgramName afterwards does it too, because the launcher is"
            + " what activates the environment.");
    }

    /// <summary>
    /// Compares two interpreter paths for equality, tolerating trailing separators, symbolic
    /// links, the macOS /private prefix, and the case-insensitive file systems Windows and
    /// macOS use by default.
    /// </summary>
    /// <param name="left">The first path, or <c>null</c>.</param>
    /// <param name="right">The second path, or <c>null</c>.</param>
    /// <returns><c>true</c> when both paths name the same folder.</returns>
    internal static bool SamePath(string? left, string? right)
    {
        if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right))
        {
            return false;
        }

        StringComparison comparison =
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

        if (string.Equals(NormalizePath(left!), NormalizePath(right!), comparison))
        {
            return true;
        }

        // Second chance: one of the two may be reached through a symbolic link. This is the
        // normal case on macOS, where /tmp, /var and /etc are links into /private.
        return string.Equals(NormalizePath(ResolveLinks(left!)), NormalizePath(ResolveLinks(right!)), comparison);
    }

    private static string NormalizePath(string path)
    {
        string full;
        try
        {
            full = Path.GetFullPath(path);
        }
        catch (ArgumentException)
        {
            full = path;
        }
        catch (NotSupportedException)
        {
            full = path;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) && full.StartsWith("/private/", StringComparison.Ordinal))
        {
            full = full.Substring("/private".Length);
        }

        string trimmed = full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return trimmed.Length == 0 ? full : trimmed;
    }

    private static string ResolveLinks(string path)
    {
        try
        {
            return Directory.ResolveLinkTarget(path, returnFinalTarget: true)?.FullName ?? path;
        }
        catch (IOException)
        {
            return path;
        }
        catch (UnauthorizedAccessException)
        {
            return path;
        }
    }

    /// <summary>
    /// Reads the interpreter version out of a pyvenv.cfg value. The <c>version</c> key written by
    /// CPython's own venv module is a plain "3.13.5", but the <c>version_info</c> key written by
    /// the virtualenv tool is "3.13.5.final.0", which <see cref="System.Version"/> rejects
    /// outright - leaving the environment with no version and therefore no libpython at all.
    /// Only the leading numeric components are kept; the library name needs major and minor.
    /// </summary>
    /// <param name="value">The raw value of the config key.</param>
    /// <returns>The version, or <c>null</c> when the value carries no major.minor pair.</returns>
    internal static Version? ParseVersion(string value)
    {
        if (Version.TryParse(value, out Version parsed))
        {
            return parsed;
        }

        if (value is null)
        {
            return null;
        }

        var numbers = new List<int>();
        foreach (string part in value.Split('.'))
        {
            if (!int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out int number))
            {
                break;
            }
            numbers.Add(number);
            if (numbers.Count == 3)
            {
                break;
            }
        }

        return numbers.Count >= 2
            ? new Version(numbers[0], numbers[1], numbers.Count > 2 ? numbers[2] : 0)
            : null;
    }

    private static Dictionary<string, string> TryParse(string venvCfg)
    {
        var settings = new Dictionary<string, string>();

        string[] lines = File.ReadAllLines(venvCfg);

        // The actually used format is really primitive: "<key> = <value>"
        foreach (string line in lines)
        {
            var split = line.Split(new[] { '=' }, 2);

            if (split.Length != 2)
                continue;

            settings[split[0].Trim()] = split[1].Trim();
        }

        return settings;
    }

    private static string? FindLibPython(string home, Version? maybeVersion)
    {
        // TODO: Check whether there is a .dll/.so/.dylib next to the executable

        if (maybeVersion is Version version)
        {
            return FindLibPythonInHome(home, version);
        }

        return null;
    }

    private static string? FindLibPythonInHome(string home, Version version)
    {
        // Probe both - pyvenv.cfg's version field doesn't distinguish free-threaded.
        var libPythonNames = new[]
        {
            GetDefaultDllName(version),
            GetDefaultDllName(version, freeThreaded: true),
        };

        return GetLibrarySearchDirectories()
            .SelectMany(path => libPythonNames.Select(name => Path.Combine(home, path, name)))
            .FirstOrDefault(File.Exists);
    }

    /// <summary>
    /// The folders, relative to the base interpreter's <c>home</c>, in which a libpython
    /// belonging to that installation is looked for, in probe order.
    /// </summary>
    /// <returns>Relative folder paths for the running operating system and process architecture.</returns>
    internal static IReadOnlyList<string> GetLibrarySearchDirectories()
    {
        List<string> pathsToCheck = new();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            var arch = RuntimeInformation.ProcessArchitecture;
            if (arch == Architecture.X64 || arch == Architecture.Arm64)
            {
                // multilib systems
                pathsToCheck.Add("../lib64");
            }
            pathsToCheck.Add("../lib");
            // Multiarch distributions (Debian, Ubuntu and their derivatives) keep libpython in
            // a per-architecture sub-folder of ../lib and nothing at all directly in ../lib, so
            // without this probe a base interpreter at /usr/bin resolves to no library at all.
            foreach (string tuple in GetLinuxMultiarchTuples(arch))
            {
                pathsToCheck.Add("../lib/" + tuple);
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            pathsToCheck.Add(".");
        }
        else
        {
            // macOS framework builds record a home of <framework>/Versions/<X.Y>/bin, so ../lib
            // is <framework>/Versions/<X.Y>/lib - where the python.org installers, Homebrew and
            // the relocatable standalone builds all keep libpythonX.Y.dylib.
            pathsToCheck.Add("../lib");
        }
        return pathsToCheck;
    }

    /// <summary>
    /// The GNU architecture tuples naming the multiarch library folder for a process
    /// architecture, most likely first. Empty for architectures with no known tuple.
    /// </summary>
    /// <param name="architecture">The architecture the current process runs as.</param>
    /// <returns>Folder names such as <c>x86_64-linux-gnu</c>.</returns>
    internal static IReadOnlyList<string> GetLinuxMultiarchTuples(Architecture architecture) => architecture switch
    {
        Architecture.X64 => new[] { "x86_64-linux-gnu" },
        Architecture.X86 => new[] { "i386-linux-gnu" },
        Architecture.Arm64 => new[] { "aarch64-linux-gnu" },
        // Debian splits 32-bit ARM into a hard-float and a soft-float port.
        Architecture.Arm => new[] { "arm-linux-gnueabihf", "arm-linux-gnueabi" },
        Architecture.Armv6 => new[] { "arm-linux-gnueabihf", "arm-linux-gnueabi" },
        Architecture.RiscV64 => new[] { "riscv64-linux-gnu" },
        Architecture.Ppc64le => new[] { "powerpc64le-linux-gnu" },
        Architecture.S390x => new[] { "s390x-linux-gnu" },
        Architecture.LoongArch64 => new[] { "loongarch64-linux-gnu" },
        _ => Array.Empty<string>(),
    };

    internal static string ProgramNameFromPath(string path)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return Path.Combine(path, "Scripts", "python.exe");
        }
        else
        {
            return Path.Combine(path, "bin", "python");
        }
    }

    internal static string GetDefaultDllName(Version version, bool freeThreaded = false)
    {
        string prefix = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "" : "lib";

        string ftSuffix = freeThreaded ? "t" : "";

        string suffix = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? Invariant($"{version.Major}{version.Minor}{ftSuffix}")
            : Invariant($"{version.Major}.{version.Minor}{ftSuffix}");

        string ext = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".dll"
            : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? ".dylib"
            : ".so";

        return prefix + "python" + suffix + ext;
    }
}
