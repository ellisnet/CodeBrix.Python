using System;

namespace CodeBrix.Python; //was previously: Python.Runtime;

public class InternalPythonnetException : Exception
{
    public InternalPythonnetException(string message, Exception innerException)
        : base(message, innerException) { }
}
