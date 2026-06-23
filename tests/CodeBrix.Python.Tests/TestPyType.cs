using System.Runtime.InteropServices;
using System.Text;
using Xunit;
using SilverAssertions;
using SilverAssertions.Collections;
using SilverAssertions.Numeric;
using SilverAssertions.Primitives;
using SilverAssertions.Specialized;
using CodeBrix.Python;
using CodeBrix.Python.Native;

namespace CodeBrix.Python.Tests; //was previously: Python.EmbeddingTest;

public class TestPyType
{
    [Fact]
    public void CanCreateHeapType()
    {
        const string name = "nÁmæ";
        const string docStr = "dÁcæ";

        using var doc = new StrPtr(docStr);

        var spec = new TypeSpec(
            name: name,
            basicSize: Util.ReadInt32(Runtime.PyBaseObjectType, TypeOffset.tp_basicsize),
            slots: new TypeSpec.Slot[] {
                new (TypeSlotID.tp_doc, doc.RawPointer),
            },
            TypeFlags.Default | TypeFlags.HeapType
        );

        using var type = new PyType(spec);
        Assert.Equal(name, type.GetAttr("__name__").As<string>());
        Assert.Equal(name, type.Name);
        Assert.Equal(docStr, type.GetAttr("__doc__").As<string>());
    }
}
