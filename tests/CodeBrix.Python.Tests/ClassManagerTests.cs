using Xunit;
using SilverAssertions;
using SilverAssertions.Collections;
using SilverAssertions.Numeric;
using SilverAssertions.Primitives;
using SilverAssertions.Specialized;
using CodeBrix.Python;

namespace CodeBrix.Python.Tests; //was previously: Python.EmbeddingTest;

public class ClassManagerTests
{
    [Fact]
    public void NestedClassDerivingFromParent()
    {
        var f = new NestedTestContainer().ToPython();
        f.GetAttr(nameof(NestedTestContainer.Bar));
    }
}

public class NestedTestParent
{
    public class Nested : NestedTestParent
    {
    }
}

public class NestedTestContainer
{
    public NestedTestParent Bar = new NestedTestParent.Nested();
}
