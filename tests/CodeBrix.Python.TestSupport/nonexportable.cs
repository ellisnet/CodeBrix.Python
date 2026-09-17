using CodeBrix.Python;

namespace CodeBrix.Python.TestSupport; //was previously: Python.Test;

// this class should not be visible to Python
[PyExport(false)]
public class NonExportable { }
