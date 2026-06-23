using System;
using Xunit;
using SilverAssertions;
using SilverAssertions.Collections;
using SilverAssertions.Numeric;
using SilverAssertions.Primitives;
using SilverAssertions.Specialized;
using CodeBrix.Python;

namespace CodeBrix.Python.Tests; //was previously: Python.EmbeddingTest;

public class TestCallbacks {
    [Fact]
    public void TestNoOverloadException() {
        int passed = 0;
        var aFunctionThatCallsIntoPython = new Action<int>(value => passed = value);
        using (Py.GIL()) {
            using dynamic callWith42 = PythonEngine.Eval("lambda f: f([42])");
            using var pyFunc = aFunctionThatCallsIntoPython.ToPython();
            var error =  Assert.Throws<PythonException>(() => callWith42(pyFunc));
            Assert.Equal("TypeError", error.Type.Name);
            string expectedArgTypes = "(<class 'list'>)";
            Assert.EndsWith(expectedArgTypes, error.Message);
            error.Traceback.Dispose();
        }
    }
}
