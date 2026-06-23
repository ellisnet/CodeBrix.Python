namespace CodeBrix.Python.TestSupport; //was previously: Python.Test;

using CodeBrix.Python;

// this class should not be visible to Python
[PyExport(false)]
public class NonExportable { }
