using System;
using CodeBrix.Python;
using SilverAssertions;
using SilverAssertions.Primitives;
using Xunit;

namespace CodeBrix.Python.Tests;

/// <summary>
/// Tests for <see cref="RequiresEngineThreadAttribute"/>'s decision, exercised through the helper
/// rather than by actually moving off the interpreter's thread: doing that for real is precisely
/// the thing that crashes the process, so it can be proved only on paper.
/// </summary>
public class RequiresEngineThreadAttributeTests
{
    [Fact]
    public void IsEngineThread_rejects_a_thread_that_is_not_the_engines()
    {
        //Arrange
        int engineThread = Runtime.MainManagedThreadId;

        //Act
        bool allowed = RequiresEngineThreadAttribute.IsEngineThread(engineThread + 1, engineThread);

        //Assert
        allowed.Should().BeFalse();
    }

    [Fact]
    public void IsEngineThread_accepts_the_thread_that_owns_the_engine()
    {
        //Arrange
        int engineThread = Runtime.MainManagedThreadId;

        //Act
        bool allowed = RequiresEngineThreadAttribute.IsEngineThread(engineThread, engineThread);

        //Assert
        allowed.Should().BeTrue();
    }

    [Fact]
    public void IsEngineThread_accepts_any_thread_before_an_engine_has_claimed_one()
    {
        //Arrange
        const int noEngineYet = 0;

        //Act
        bool allowed = RequiresEngineThreadAttribute.IsEngineThread(12345, noEngineYet);

        //Assert
        allowed.Should().BeTrue();
    }

    [Fact]
    public void DescribeWrongThread_names_the_test_both_threads_and_the_cause()
    {
        //Arrange
        const string testName = "SomeNamespace.SomeClass.some_test";

        //Act
        string message = RequiresEngineThreadAttribute.DescribeWrongThread(testName, 42, 7);

        //Assert
        message.Should().Contain(testName);
        message.Should().Contain("42");
        message.Should().Contain("7");
        message.Should().Contain("awaited");
    }

    [Fact]
    public void the_engine_thread_is_the_one_every_test_in_this_assembly_runs_on()
    {
        //Arrange
        int engineThread = Runtime.MainManagedThreadId;

        //Act
        bool allowed = RequiresEngineThreadAttribute.IsEngineThread(
            Environment.CurrentManagedThreadId, engineThread);

        //Assert
        engineThread.Should().NotBe(0);
        allowed.Should().BeTrue();
    }
}
