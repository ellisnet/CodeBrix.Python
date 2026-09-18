using System;
using System.Globalization;
using System.Reflection;
using CodeBrix.Python;
using Xunit;
using Xunit.v3;

// Applied to the whole assembly: BeforeAfterTestAttribute declares
// AttributeUsage(AttributeTargets.Assembly | Class | Method), and xunit.v3 collects the
// assembly-level ones, so every test in this assembly is checked without anyone having to
// remember to decorate a new class.
[assembly: CodeBrix.Python.Tests.RequiresEngineThread]

namespace CodeBrix.Python.Tests;

/// <summary>
/// Fails a test that is about to run on a thread other than the one that owns the embedded
/// interpreter, turning an otherwise baffling process crash into a named failure on the test that
/// caused it.
/// <para>
/// The interpreter belongs to the thread that called <c>PythonEngine.Initialize()</c>: it holds
/// the Global Interpreter Lock for the whole run and never releases it. xUnit drives a test
/// collection as a single asynchronous flow, so awaiting an operation that has not already
/// completed resumes that flow on a thread pool thread - and every later test in the collection
/// runs there too, with no GIL. The next one to call into CPython takes the process down with a
/// native fault, in a test that has nothing to do with the one that awaited.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Method,
    AllowMultiple = false)]
public sealed class RequiresEngineThreadAttribute : BeforeAfterTestAttribute
{
    /// <summary>
    /// Whether a thread may run Python work.
    /// </summary>
    /// <param name="currentManagedThreadId">The managed id of the thread about to run a test.</param>
    /// <param name="engineManagedThreadId">
    /// The managed id of the thread that initialized the interpreter, which is
    /// <see cref="Runtime.MainManagedThreadId"/> once the engine is running and zero before that.
    /// </param>
    /// <returns>
    /// <c>true</c> when the two are the same thread, or when no engine has claimed one yet.
    /// </returns>
    internal static bool IsEngineThread(int currentManagedThreadId, int engineManagedThreadId)
        => engineManagedThreadId == 0 || currentManagedThreadId == engineManagedThreadId;

    /// <summary>
    /// The failure message: what happened, why the run would otherwise crash somewhere else, and
    /// what to do about it.
    /// </summary>
    /// <param name="testName">The test that was about to run.</param>
    /// <param name="currentManagedThreadId">The thread it was about to run on.</param>
    /// <param name="engineManagedThreadId">The thread that owns the interpreter.</param>
    /// <returns>The message.</returns>
    internal static string DescribeWrongThread(
        string testName, int currentManagedThreadId, int engineManagedThreadId)
        => string.Format(
            CultureInfo.InvariantCulture,
            "'{0}' was about to run on managed thread {1}, but the embedded interpreter belongs to"
            + " managed thread {2} - the thread that called PythonEngine.Initialize() and still"
            + " holds the GIL. Something earlier in this test collection awaited an operation that"
            + " had not already completed, which moved the rest of the run onto a thread pool"
            + " thread. Left alone, the next call into CPython would crash the process natively,"
            + " in a test unrelated to the one that awaited. Keep the tests in this assembly"
            + " synchronous: poll instead of awaiting, as ProcessExitShutdownModeTests does.",
            testName,
            currentManagedThreadId,
            engineManagedThreadId);

    /// <summary>
    /// Checks the thread before the test body runs.
    /// </summary>
    /// <param name="methodUnderTest">The test method about to run.</param>
    /// <param name="test">The test about to run.</param>
    public override void Before(MethodInfo methodUnderTest, IXunitTest test)
    {
        int engineManagedThreadId = Runtime.MainManagedThreadId;
        int currentManagedThreadId = Environment.CurrentManagedThreadId;

        if (!IsEngineThread(currentManagedThreadId, engineManagedThreadId))
        {
            Assert.Fail(DescribeWrongThread(
                test?.TestDisplayName ?? methodUnderTest?.Name ?? "<unknown test>",
                currentManagedThreadId,
                engineManagedThreadId));
        }
    }
}
