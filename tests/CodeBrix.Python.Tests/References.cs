using CodeBrix.Python;
using SilverAssertions;
using SilverAssertions.Collections;
using SilverAssertions.Numeric;
using SilverAssertions.Primitives;
using SilverAssertions.Specialized;
using Xunit;

namespace CodeBrix.Python.Tests; //was previously: Python.EmbeddingTest;

public class References
{
    [Fact]
    public void MoveToPyObject_SetsNull()
    {
        var dict = new PyDict();
        NewReference reference = Runtime.PyDict_Items(dict.Reference);
        try
        {
            Assert.False(reference.IsNull());

            using (reference.MoveToPyObject())
                Assert.True(reference.IsNull());
        }
        finally
        {
            reference.Dispose();
        }
    }

    [Fact]
    public void CanBorrowFromNewReference()
    {
        var dict = new PyDict();
        using NewReference reference = Runtime.PyDict_Items(dict.Reference);
        BorrowedReference borrowed = reference.BorrowOrThrow();
        PythonException.ThrowIfIsNotZero(Runtime.PyList_Reverse(borrowed));
    }
}
