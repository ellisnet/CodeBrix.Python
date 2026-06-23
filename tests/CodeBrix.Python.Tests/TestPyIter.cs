using System.Linq;
using System.Text;
using Xunit;
using SilverAssertions;
using SilverAssertions.Collections;
using SilverAssertions.Numeric;
using SilverAssertions.Primitives;
using SilverAssertions.Specialized;
using CodeBrix.Python;

namespace CodeBrix.Python.Tests; //was previously: Python.EmbeddingTest;

public class TestPyIter
{
    [Fact]
    public void KeepOldObjects()
    {
        using (Py.GIL())
        using (var testString = new PyString("hello world! !$%&/()=?"))
        {
            PyObject[] chars = testString.ToArray();
            Assert.True(chars.Length > 1);
            string reconstructed = string.Concat(chars.Select(c => c.As<string>()));
            Assert.Equal(testString.As<string>(), reconstructed);
        }
    }
}
