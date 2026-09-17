namespace System.Runtime.CompilerServices; //was previously: System.Runtime.CompilerServices;

// The netstandard2.0 polyfill that made `init` accessors compile on a target framework
// whose BCL predates IsExternalInit. .NET 10 declares the real type, so this one is
// neutralized rather than deleted, to keep a future upstream diff lined up.
//internal static class IsExternalInit { }
