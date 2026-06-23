using System;
using Xunit;
using SilverAssertions;
using SilverAssertions.Collections;
using SilverAssertions.Numeric;
using SilverAssertions.Primitives;
using SilverAssertions.Specialized;
using CodeBrix.Python;

namespace CodeBrix.Python.Tests; //was previously: Python.EmbeddingTest;

public class TestCustomMarshal
{
    [Fact]
    public static void GetManagedStringTwice()
    {
        const string expected = "FooBar";

        using var op = Runtime.PyString_FromString(expected);
        string s1 = Runtime.GetManagedString(op.BorrowOrThrow());
        string s2 = Runtime.GetManagedString(op.Borrow());

        Assert.Equal(1, Runtime.Refcount32(op.Borrow()));
        Assert.Equal(expected, s1);
        Assert.Equal(expected, s2);
    }
}
