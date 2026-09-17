using System;
using System.Linq;
using System.Reflection;
using SilverAssertions;
using SilverAssertions.Primitives;
using Xunit;

namespace CodeBrix.Python.Tests.Venv;

/// <summary>
/// Regression fences for the assembly-wide engine ownership. Without the assembly fixture,
/// nothing calls <see cref="PythonEngine.Shutdown"/>, the library's process-exit handler tries
/// to do it from the CLR's shutdown thread while the initializing thread still holds the
/// interpreter, and the test executable never exits. Without the parallelization setting, tests
/// would share one interpreter across threads.
/// </summary>
public class GlobalTestsSetupTests
{
    private static object[] AssemblyAttributes()
        => typeof(GlobalTestsSetup).Assembly.GetCustomAttributes(inherit: false);

    [Fact]
    public void assembly_declares_the_engine_owner_as_its_assembly_fixture()
    {
        //Arrange
        object[] attributes = AssemblyAttributes();

        //Act
        bool declared = attributes.Any(attribute =>
            attribute.GetType().Name == "AssemblyFixtureAttribute"
            && attribute.GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Any(property => property.GetValue(attribute) as Type == typeof(GlobalTestsSetup)));

        //Assert
        declared.Should().BeTrue();
    }

    [Fact]
    public void assembly_disables_test_parallelization()
    {
        //Arrange
        object[] attributes = AssemblyAttributes();

        //Act
        object parallelization = attributes.FirstOrDefault(
            attribute => attribute.GetType().Name == "ParallelizationAttribute");

        //Assert
        parallelization.Should().NotBeNull();
    }

    [Fact]
    public void engine_owner_shuts_the_interpreter_down_exactly_once()
    {
        //Arrange
        MethodInfo dispose = typeof(GlobalTestsSetup).GetMethod(nameof(IDisposable.Dispose));

        //Act
        bool disposable = typeof(IDisposable).IsAssignableFrom(typeof(GlobalTestsSetup));

        //Assert
        disposable.Should().BeTrue();
        dispose.Should().NotBeNull();
    }
}
