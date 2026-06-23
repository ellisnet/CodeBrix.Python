using System;
using CodeBrix.Python;
using Xunit;

// CodeBrix port: the upstream NUnit [SetUpFixture] (assembly-wide one-time
// setup/teardown) becomes an xUnit v3 assembly fixture. Test parallelization is
// disabled because the embedded CPython interpreter is single-threaded under the
// Global Interpreter Lock.
[assembly: AssemblyFixture(typeof(CodeBrix.Python.Tests.GlobalTestsSetup))]
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace CodeBrix.Python.Tests; //was previously: Python.EmbeddingTest;

/// <summary>
/// Assembly-wide setup/teardown: initializes the Python engine before any test
/// runs and shuts it down after all tests complete.
/// </summary>
public sealed partial class GlobalTestsSetup : IDisposable
{
    /// <summary>Initializes the Python engine for the whole test assembly.</summary>
    public GlobalTestsSetup()
    {
        // On Windows, point the embedded interpreter at libpython via appsettings.json
        // (process-scoped PYTHONNET_PYDLL) so no persistent environment variable is
        // needed. No-op on Linux/macOS, which use the existing venv/env-var discovery.
        WindowsPythonDll.EnsureSet();

        Finalizer.Instance.ErrorHandler += FinalizerErrorHandler;
        PythonEngine.Initialize();

        // CodeBrix port: register the Inheritance tests' base-type providers here,
        // before any CLR type is materialized into the process-wide Python type
        // cache. (Upstream did this per-class via [OneTimeSetUp]; under the shared
        // embedded interpreter the registration must precede the first use of the
        // affected types, so it is hoisted to assembly scope for deterministic order.)
        using var locals = new PyDict();
        PythonEngine.Exec(InheritanceTestBaseClassWrapper.ClassSourceCode, locals: locals);
        Inheritance.SharedNoEffectBaseTypeProvider = new NoEffectBaseTypeProvider();
        Inheritance.SharedExtraBaseTypeProvider = new ExtraBaseTypeProvider(
            new PyType(locals[InheritanceTestBaseClassWrapper.ClassName]));
        var baseTypeProviders = PythonEngine.InteropConfiguration.PythonBaseTypeProviders;
        baseTypeProviders.Add(Inheritance.SharedExtraBaseTypeProvider);
        baseTypeProviders.Add(Inheritance.SharedNoEffectBaseTypeProvider);
    }

    private void FinalizerErrorHandler(object sender, Finalizer.ErrorArgs e)
    {
        if (e.Error is RuntimeShutdownException)
        {
            // allow objects to leak after the python runtime run
            // they were created in is gone
            e.Handled = true;
        }
    }

    /// <summary>Shuts the Python engine down after all tests have run.</summary>
    public void Dispose()
    {
        if (PythonEngine.IsInitialized)
        {
            PythonEngine.Shutdown();
        }
    }
}
