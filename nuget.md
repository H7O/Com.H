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

The netstandard2.0 build includes all core functionality. Some advanced features (async streams via `IAsyncEnumerable<T>`, `System.Text.Json` helpers, `SearchValues<char>` optimised file-name validation, and streaming base64-to-file decoding) are only available on net8.0 and above.

> **Note:** The separate 2.0.0.x netstandard2.0 package line has been retired. All framework targets are now served by this single package.
