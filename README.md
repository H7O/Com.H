# Com.H
General purpose library containing, in its current form, a very small collection of helpful functionalties that don't warrent a seperate library for each at this early stage.

## Installation

```
dotnet add package Com.H
```

## Target Frameworks

A single NuGet package multi-targets:

- **netstandard2.0** — .NET Framework 4.6.1+, .NET Core 2.0+, Xamarin, Unity
- **net8.0** / **net9.0** / **net10.0** — latest .NET releases

The netstandard2.0 build includes all core functionality (collections, caching, CSV/XML/template parsing, reflection mapping, networking, SSH, mail, cryptography, shell utilities, threading helpers, etc.). A small number of advanced features are available only on net8.0+:

| net8.0+ only | Reason |
|---|---|
| Async streams (`IAsyncEnumerable<T>`) | No runtime support in netstandard2.0 |
| `System.Text.Json` helpers | Namespace not available |
| `SearchValues<char>` file-name validation | API introduced in .NET 8 |
| Streaming base64-to-file decoding | ArrayPool + Memory patterns |
| `FileSystemWatcherEx` | Requires non-generic `TaskCompletionSource` + records |

## License

MIT
