namespace CodeBrix.Python.Tests; //was previously: Python.EmbeddingTest;

using System;
using System.Linq;
using Xunit;
using SilverAssertions;
using SilverAssertions.Collections;
using SilverAssertions.Numeric;
using SilverAssertions.Primitives;
using SilverAssertions.Specialized;
using CodeBrix.Python;
using CodeBrix.Python.Codecs;

public class CodecGroups
{
    [Fact]
    public void GetEncodersByType()
    {
        var encoder1 = new ObjectToEncoderInstanceEncoder<Uri>();
        var encoder2 = new ObjectToEncoderInstanceEncoder<Uri>();
        var group = new EncoderGroup {
            new ObjectToEncoderInstanceEncoder<Tuple<int>>(),
            encoder1,
            encoder2,
        };

        var got = group.GetEncoders(typeof(Uri)).ToArray();
        (got).Should().Equal(new[] { encoder1, encoder2 });
    }

    [Fact]
    public void CanEncode()
    {
        var group = new EncoderGroup {
            new ObjectToEncoderInstanceEncoder<Tuple<int>>(),
            new ObjectToEncoderInstanceEncoder<Uri>(),
        };

        Assert.Multiple(() =>
        {
            Assert.True(group.CanEncode(typeof(Tuple<int>)));
            Assert.True(group.CanEncode(typeof(Uri)));
            Assert.False(group.CanEncode(typeof(string)));
        });

    }

    [Fact]
    public void Encodes()
    {
        var encoder0 = new ObjectToEncoderInstanceEncoder<Tuple<int>>();
        var encoder1 = new ObjectToEncoderInstanceEncoder<Uri>();
        var encoder2 = new ObjectToEncoderInstanceEncoder<Uri>();
        var group = new EncoderGroup {
            encoder0,
            encoder1,
            encoder2,
        };

        var uri = group.TryEncode(new Uri("data:"));
        var clrObject = (CLRObject)ManagedType.GetManagedObject(uri);
        Assert.Same(encoder1, clrObject.inst);
        Assert.NotSame(encoder2, clrObject.inst);

        var tuple = group.TryEncode(Tuple.Create(1));
        clrObject = (CLRObject)ManagedType.GetManagedObject(tuple);
        Assert.Same(encoder0, clrObject.inst);
    }

    [Fact]
    public void GetDecodersByTypes()
    {
        var pyint = new PyInt(10).GetPythonType();
        var pyfloat = new PyFloat(10).GetPythonType();
        var pystr = new PyString("world").GetPythonType();
        var decoder1 = new DecoderReturningPredefinedValue<long>(pyint, decodeResult: 42);
        var decoder2 = new DecoderReturningPredefinedValue<string>(pyfloat, decodeResult: "atad:");
        var group = new DecoderGroup {
            decoder1,
            decoder2,
        };

        var decoder = group.GetDecoder(pyfloat, typeof(string));
        Assert.Same(decoder2, decoder);
        decoder = group.GetDecoder(pystr, typeof(string));
        Assert.Null(decoder);
        decoder = group.GetDecoder(pyint, typeof(long));
        Assert.Same(decoder1, decoder);
    }
    [Fact]
    public void CanDecode()
    {
        var pyint = new PyInt(10).GetPythonType();
        var pyfloat = new PyFloat(10).GetPythonType();
        var pystr = new PyString("world").GetPythonType();
        var decoder1 = new DecoderReturningPredefinedValue<long>(pyint, decodeResult: 42);
        var decoder2 = new DecoderReturningPredefinedValue<string>(pyfloat, decodeResult: "atad:");
        var group = new DecoderGroup {
            decoder1,
            decoder2,
        };

        Assert.Multiple(() =>
        {
            Assert.True(group.CanDecode(pyint, typeof(long)));
            Assert.False(group.CanDecode(pyint, typeof(int)));
            Assert.True(group.CanDecode(pyfloat, typeof(string)));
            Assert.False(group.CanDecode(pystr, typeof(string)));
        });

    }

    [Fact]
    public void Decodes()
    {
        var pyint = new PyInt(10).GetPythonType();
        var pyfloat = new PyFloat(10).GetPythonType();
        var decoder1 = new DecoderReturningPredefinedValue<long>(pyint, decodeResult: 42);
        var decoder2 = new DecoderReturningPredefinedValue<string>(pyfloat, decodeResult: "atad:");
        var group = new DecoderGroup {
            decoder1,
            decoder2,
        };

        Assert.Multiple(() =>
        {
            Assert.True(group.TryDecode(new PyInt(10), out long longResult));
            Assert.Equal(42, longResult);
            Assert.True(group.TryDecode(new PyFloat(10), out string strResult));
            Assert.Same("atad:", strResult);
            Assert.False(group.TryDecode(new PyInt(10), out int _));
        });
    }
}
