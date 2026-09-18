using System.Collections;

// Split out of pyimport.cs. This type has NO namespace on purpose: it is the regression test
// for the upstream project's issue #1601 - initialization used to fail when a public class
// deriving from IEnumerable existed in the global namespace - so putting it in a namespace
// would quietly stop that regression from being tested. A file-scoped namespace cannot declare
// the global namespace, which is why this is a file of its own.

public class PublicEnumerator : IEnumerable
{
    public IEnumerator GetEnumerator()
    {
        return null;
    }
}
