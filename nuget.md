# Com.H
Kindly visit the project's github page for documentation [https://github.com/H7O/Com.H](https://github.com/H7O/Com.H)

## Target Frameworks

This package multi-targets the following frameworks from a single NuGet package:

| Target | Minimum Runtime |
|---|---|
| **netstandard2.0** | .NET Framework 4.6.1+, .NET Core 2.0+, Xamarin, Unity, UWP |
| **net8.0** | .NET 8 |
| **net9.0** | .NET 9 |
| **net10.0** | .NET 10 |

As of 10.2.0 the netstandard2.0 build is at **full public-API parity** with the modern targets — async streams via `IAsyncEnumerable<T>`, the `System.Text.Json` helpers and the streaming base64-to-file helpers are all available everywhere. Where a modern BCL API is used internally, netstandard2.0 falls back to an equivalent implementation rather than dropping the feature. The full test suite runs against the netstandard2.0 build on .NET Framework 4.8.1 as well as against the modern build.

The net8.0/net9.0/net10.0 targets have no package dependencies. netstandard2.0 additionally brings in `Microsoft.CSharp`, `System.Text.Json` and `Microsoft.Bcl.AsyncInterfaces`.

> **Note:** The separate 2.0.0.x netstandard2.0 package line has been retired. All framework targets are now served by this single package.
