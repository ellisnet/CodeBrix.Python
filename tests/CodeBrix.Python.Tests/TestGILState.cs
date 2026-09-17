using CodeBrix.Python;
using SilverAssertions;
using SilverAssertions.Collections;
using SilverAssertions.Numeric;
using SilverAssertions.Primitives;
using SilverAssertions.Specialized;
using Xunit;

namespace CodeBrix.Python.Tests; //was previously: Python.EmbeddingTest;

public class TestGILState
{
    /// <summary>
    /// Ensure, that calling <see cref="Py.GILState.Dispose"/> multiple times is safe
    /// </summary>
    [Fact]
    public void CanDisposeMultipleTimes()
    {
        using (var gilState = Py.GIL())
        {
            for(int i = 0; i < 50; i++)
                gilState.Dispose();
        }
    }
}
