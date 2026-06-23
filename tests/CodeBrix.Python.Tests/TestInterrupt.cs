using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using SilverAssertions;
using SilverAssertions.Collections;
using SilverAssertions.Numeric;
using SilverAssertions.Primitives;
using SilverAssertions.Specialized;
using CodeBrix.Python;

namespace CodeBrix.Python.Tests; //was previously: Python.EmbeddingTest;

public class TestInterrupt : IDisposable
{
    PyObject threading;
    public TestInterrupt()
    {
        // workaround for assert tlock.locked() warning
        threading = Py.Import("threading");
    }

    public void Dispose()
    {
        threading.Dispose();
    }

    // CodeBrix port: the upstream test used blocking Task.Wait()/Result. To satisfy
    // the xUnit analyzers (no blocking task ops, thread CancellationToken) while
    // preserving the GIL thread-affinity (Begin/EndAllowThreads must run on the same
    // thread, so we must NOT await between them), the completion wait is a non-blocking
    // poll and the result is awaited only after the threads region has ended.
    static void WaitForCompletion(Task task)
    {
        var sw = Stopwatch.StartNew();
        while (!task.IsCompleted && sw.Elapsed < TimeSpan.FromSeconds(5))
        {
            Thread.Sleep(10);
        }
    }

    [Fact]
    public async Task PythonThreadIDStable()
    {
        long pythonThreadID = 0;
        long pythonThreadID2 = 0;
        var asyncCall = Task.Factory.StartNew(() =>
        {
            using (Py.GIL())
            {
                Interlocked.Exchange(ref pythonThreadID, (long)PythonEngine.GetPythonThreadID());
                Interlocked.Exchange(ref pythonThreadID2, (long)PythonEngine.GetPythonThreadID());
            }
        }, TestContext.Current.CancellationToken);

        var timeout = Stopwatch.StartNew();

        IntPtr threadState = PythonEngine.BeginAllowThreads();
        while (Interlocked.Read(ref pythonThreadID) == 0 || Interlocked.Read(ref pythonThreadID2) == 0)
        {
            Assert.True(timeout.Elapsed < TimeSpan.FromSeconds(5));
        }
        WaitForCompletion(asyncCall);
        PythonEngine.EndAllowThreads(threadState);

        Assert.True(asyncCall.IsCompleted);
        await asyncCall;

        Assert.Equal(pythonThreadID, pythonThreadID2);
        Assert.NotEqual(0, pythonThreadID);
    }

    [Fact]
    public async Task InterruptTest()
    {
        long pythonThreadID = 0;
        var asyncCall = Task.Factory.StartNew(() =>
        {
            using (Py.GIL())
            {
                Interlocked.Exchange(ref pythonThreadID, (long)PythonEngine.GetPythonThreadID());
                return PythonEngine.RunSimpleString(@"
try:
  import time

  while True:
    time.sleep(0.2)
except KeyboardInterrupt:
  pass");
            }
        }, TestContext.Current.CancellationToken);

        var timeout = Stopwatch.StartNew();

        IntPtr threadState = PythonEngine.BeginAllowThreads();
        while (Interlocked.Read(ref pythonThreadID) == 0)
        {
            Assert.True(timeout.Elapsed < TimeSpan.FromSeconds(5));
        }
        PythonEngine.EndAllowThreads(threadState);

        int interruptReturnValue = PythonEngine.Interrupt((ulong)Interlocked.Read(ref pythonThreadID));
        Assert.Equal(1, interruptReturnValue);

        threadState = PythonEngine.BeginAllowThreads();
        WaitForCompletion(asyncCall);
        PythonEngine.EndAllowThreads(threadState);

        Assert.True(asyncCall.IsCompleted);
        Assert.Equal(0, await asyncCall);
    }
}
