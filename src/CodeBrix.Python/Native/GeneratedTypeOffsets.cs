using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace CodeBrix.Python.Native; //was previously: Python.Runtime.Native;

[StructLayout(LayoutKind.Sequential)]
abstract class GeneratedTypeOffsets
{
    protected GeneratedTypeOffsets()
    {
        var type = this.GetType();
        var offsetProperties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
        int fieldSize = IntPtr.Size;
        for (int fieldIndex = 0; fieldIndex < offsetProperties.Length; fieldIndex++)
        {
            int offset = fieldIndex * fieldSize;
            offsetProperties[fieldIndex].SetValue(this, offset, index: null);
        }
    }
}
