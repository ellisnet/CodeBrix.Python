using System;
using System.Diagnostics;
using System.Threading;
using Xunit;
using SilverAssertions;
using SilverAssertions.Collections;
using SilverAssertions.Numeric;
using SilverAssertions.Primitives;
using SilverAssertions.Specialized;
using CodeBrix.Python;

namespace CodeBrix.Python.Tests; //was previously: Python.EmbeddingTest;

public class Events
{
    [Fact]
    public void UsingDoesNotLeak()
    {
        using var scope = Py.CreateScope();
        scope.Exec(@"
import gc

from CodeBrix.Python.Tests import ClassWithEventHandler

def event_handler():
    pass

for _ in range(2000):
    example = ClassWithEventHandler()
    example.LeakEvent += event_handler
    example.LeakEvent -= event_handler
    del example

gc.collect()
");
        Runtime.TryCollectingGarbage(10);
        Assert.Equal(0, ClassWithEventHandler.alive);
    }
}

public class ClassWithEventHandler
{
    internal static int alive;

    public event EventHandler LeakEvent;
    private Array arr;  // dummy array to exacerbate memory leak

    public ClassWithEventHandler()
    {
        Interlocked.Increment(ref alive);
        this.arr = new int[800];
    }

    // Reference LeakEvent to silence warning
    protected virtual void OnLeakEvent(EventArgs e)
    {
        LeakEvent?.Invoke(this, e);
    }

    ~ClassWithEventHandler()
    {
        Interlocked.Decrement(ref alive);
    }
}
