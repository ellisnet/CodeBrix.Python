using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using SilverAssertions;
using SilverAssertions.Collections;
using SilverAssertions.Numeric;
using SilverAssertions.Primitives;
using SilverAssertions.Specialized;
using CodeBrix.Python;

namespace CodeBrix.Python.Tests; //was previously: Python.EmbeddingTest;

public class TestNativeTypeOffset
{
    /// <summary>
    /// Tests that installation has generated code for NativeTypeOffset and that it can be loaded.
    /// </summary>        
    [Fact]
    public void LoadNativeTypeOffsetClass()
    {
        PyObject sys = Py.Import("sys");
        // "m" is benign; "t" (free-threaded) is handled via ABI.ObjectHeadOffset
        // rather than the install-time-generated NativeTypeOffset class.
        var abiflags = sys.HasAttr("abiflags") ? sys.GetAttr("abiflags").ToString() : "";
        abiflags = abiflags.Replace("m", "").Replace("t", "");
        if (!string.IsNullOrEmpty(abiflags))
        {
            string typeName = "CodeBrix.Python.NativeTypeOffset, CodeBrix.Python";
            Assert.NotNull(Type.GetType(typeName));
        }
    }
}
