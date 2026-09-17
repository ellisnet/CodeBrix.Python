using System;

namespace CodeBrix.Python; //was previously: Python.Runtime;

[AttributeUsage(AttributeTargets.Struct)]
class NonCopyableAttribute : Attribute { }
