using System.Collections.Generic;

namespace CodeBrix.Python; //was previously: Python.Runtime;

public interface ICLRObjectStorer
{
    ICollection<CLRMappedItem> Store(CLRWrapperCollection wrappers, Dictionary<string, object?> storage);
    CLRWrapperCollection Restore(Dictionary<string, object?> storage);
}
