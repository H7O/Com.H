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

As of 10.2.0 the netstandard2.0 build is at **full public-API parity** with the modern targets. Async streams (`IAsyncEnumerable<T>`, `await foreach`), the `System.Text.Json` helpers, the chambered enumerable/async-enumerable types, and the streaming base64-to-file helpers are all available on every target.

Where a modern BCL API is used internally, netstandard2.0 uses an equivalent implementation rather than dropping the feature — `SearchValues<char>` falls back to a `HashSet<char>`, span-based scanning to its non-span equivalent, and `FileStream` async disposal to synchronous disposal (which still flushes).

### Testing

The suite runs against **both** builds:

| Project | Target | Exercises |
|---|---|---|
| `tests/` | net10.0 | the modern build |
| `tests.net481/` | net481 | the **netstandard2.0** build, including every polyfill and fallback |

Both projects compile the same linked test sources, so they cannot drift. 123 tests, green on both.

### netstandard2.0 dependencies

The modern targets have **no** package dependencies. netstandard2.0 pulls in `Microsoft.CSharp`, `System.Text.Json` and `Microsoft.Bcl.AsyncInterfaces` to reach the parity described above.

## License

MIT
