# Com.H Consolidation Playbook

## Goal

Eliminate the separate `NetStandard2.0/` project. Add `netstandard2.0` as a target to the existing `src/` project so a single codebase produces binaries for all TFMs. The result is one NuGet package that works everywhere from .NET Framework 4.6.1 (via netstandard2.0) through .NET 10.

---

## Step 1 — Update `src/Com.H.csproj`

### 1a. Add `netstandard2.0` to TargetFrameworks

Change:
```xml
<TargetFrameworks>net8.0;net9.0;net10.0</TargetFrameworks>
```
To:
```xml
<TargetFrameworks>netstandard2.0;net8.0;net9.0;net10.0</TargetFrameworks>
```

### 1b. Add LangVersion

Add inside `<PropertyGroup>`:
```xml
<LangVersion>latest</LangVersion>
```
This enables C# 12+ features (nullable annotations, file-scoped namespaces, `new()`, pattern matching) even when targeting netstandard2.0. These are compiler features, not runtime features.

### 1c. Conditional package references

Add these `ItemGroup` blocks:

```xml
<!-- Required for netstandard2.0 only -->
<ItemGroup Condition="'$(TargetFramework)' == 'netstandard2.0'">
  <PackageReference Include="Microsoft.CSharp" Version="4.7.0" />
</ItemGroup>
```

**Note:** `ImplicitUsings` and `Nullable` already in the csproj will apply to all TFMs. For netstandard2.0, the compiler will still enforce nullable analysis — no issue there.

---

## Step 2 — Add `#if` directives for net8+ only APIs

There are several API surfaces unavailable on netstandard2.0. Each needs a `#if NET8_0_OR_GREATER` guard (or `#if NET5_0_OR_GREATER` where noted).

### 2a. `IO/IOExtensions.cs` — `SearchValues<char>`

`System.Buffers.SearchValues` is net8+ only. The file name validation code needs a fallback.

**Using directive** — wrap the import:
```csharp
#if NET8_0_OR_GREATER
using System.Buffers;
#endif
```

**The `InvalidFileNameChars` field** — wrap it:
```csharp
#if NET8_0_OR_GREATER
public static readonly SearchValues<char> InvalidFileNameChars =
    SearchValues.Create(Path.GetInvalidFileNameChars());
#endif
```

**The validation loop that uses `InvalidFileNameChars`** — provide alternative for netstandard2.0:
```csharp
#if NET8_0_OR_GREATER
            var span = fileName.AsSpan();
            var invalidChars = new HashSet<char>();
            int index = 0;

            while (index < span.Length)
            {
                int foundIndex = span[index..].IndexOfAny(InvalidFileNameChars);
                if (foundIndex == -1)
                    break;

                invalidChars.Add(span[index + foundIndex]);
                index += foundIndex + 1;
            }

            if (invalidChars.Count > 0)
            {
                throw new ArgumentException(
                    $"File name `{fileName}` contains invalid characters: {string.Join(", ", invalidChars.Select(c => $"`{c}`"))}");
            }
#else
            var invalidFileNameChars = Path.GetInvalidFileNameChars();
            var invalidChars = new HashSet<char>();
            foreach (var c in fileName)
            {
                if (invalidFileNameChars.Contains(c))
                    invalidChars.Add(c);
            }

            if (invalidChars.Count > 0)
            {
                throw new ArgumentException(
                    $"File name `{fileName}` contains invalid characters: {string.Join(", ", invalidChars.Select(c => $"`{c}`"))}");
            }
#endif
```

**`WriteBase64ToFileAsync` and `WriteBase64ToTempFileAsync`** — these use `await using`, `ArrayPool`, and `AsSpan`/`AsMemory` overloads that aren't available on netstandard2.0. Wrap both methods:
```csharp
#if NET8_0_OR_GREATER
        public static async Task<long> WriteBase64ToFileAsync(...)
        {
            // ... existing implementation
        }

        public static async Task<(string tempPath, long fileSize)> WriteBase64ToTempFileAsync(...)
        {
            // ... existing implementation
        }

        public static long GetBase64DecodedSize(...)
        {
            // ... existing implementation
        }
#endif
```

### 2b. `Text/Csv/CsvExtensions.cs` — `StringSplitOptions.TrimEntries`

`StringSplitOptions.TrimEntries` is net8+ only (actually net5+ but not in netstandard2.0).

In the `ParseDelimited` method, replace:
```csharp
    var data = text.Split(rowDelimieter, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    var headers = data.First().Split(colDelimieter, StringSplitOptions.TrimEntries);
    var rows = data.Skip(1).Select(col =>
        col.Split(colDelimieter, StringSplitOptions.TrimEntries));
```

With:
```csharp
#if NET8_0_OR_GREATER
    var data = text.Split(rowDelimieter, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    var headers = data.First().Split(colDelimieter, StringSplitOptions.TrimEntries);
    var rows = data.Skip(1).Select(col =>
        col.Split(colDelimieter, StringSplitOptions.TrimEntries));
#else
    var data = text.Split(rowDelimieter, StringSplitOptions.RemoveEmptyEntries)
        .Select(x => x.Trim()).ToArray();
    var headers = data.First().Split(colDelimieter, StringSplitOptions.None)
        .Select(x => x.Trim()).ToArray();
    var rows = data.Skip(1).Select(col =>
        col.Split(colDelimieter, StringSplitOptions.None)
            .Select(x => x.Trim()).ToArray());
#endif
```

Also in `ParseDelimited`, `ExpandoObject.TryAdd()` is not available on netstandard2.0. Replace:
```csharp
            exObj.TryAdd(item.h, item.c);
```
With:
```csharp
#if NET8_0_OR_GREATER
            exObj.TryAdd(item.h, item.c);
#else
            ((IDictionary<string, object>)exObj)[item.h] = item.c;
#endif
```

### 2c. `IAsyncEnumerable<T>` files — entire files are net8+ only

These files use `IAsyncEnumerable<T>`, `IAsyncEnumerator<T>`, `IAsyncDisposable`, `await foreach`, `await using`, etc. which don't exist in netstandard2.0.

Wrap the **entire file contents** (inside the namespace) with `#if NET8_0_OR_GREATER` / `#endif`:

| File | What to wrap |
|------|-------------|
| `Linq/Async/LinqAsyncExtensions.cs` | Entire class |
| `Collections/Generic/ChamberedAsyncEnumerable.cs` | Entire class |

For `Collections/Generic/Extensions.cs`, the sync methods (`ToChamberedEnumerable`, `RemainingItems`) work on netstandard2.0 but the async methods (`ToChamberedEnumerableAsync`, `RemainingItemsAsync`, `EmptyAsyncEnumerable`, `ToAsyncEnumerable`, `ConcatAsyncEnumerables`) do not. Wrap only the async methods:

```csharp
        // Keep ToChamberedEnumerable and RemainingItems as-is (they work everywhere)

#if NET8_0_OR_GREATER
        public static async Task<ChamberedAsyncEnumerable<dynamic>> ToChamberedEnumerableAsync(...)
        { ... }

        public static async IAsyncEnumerable<dynamic> RemainingItemsAsync(...)
        { ... }

        private static async IAsyncEnumerable<dynamic> EmptyAsyncEnumerable()
        { ... }

        private static async IAsyncEnumerable<dynamic> ToAsyncEnumerable(...)
        { ... }

        private static async IAsyncEnumerable<dynamic> ConcatAsyncEnumerables(...)
        { ... }
#endif
```

### 2d. `Text/Json/JsonExtensions.cs` — `System.Text.Json`

`System.Text.Json` is not inbox on netstandard2.0. You could either:
- **(Option A)** Wrap the entire file in `#if NET8_0_OR_GREATER` — simplest, excludes JSON support from netstandard2.0
- **(Option B)** Add a conditional `PackageReference` for `System.Text.Json` on netstandard2.0 — makes it available everywhere, but still need `#if` for `IAsyncEnumerable` usage in `SerializeAsync`.

**Recommended: Option A** (wrap entire file):
```csharp
#if NET8_0_OR_GREATER
// ... entire file content
#endif
```

If consumers on netstandard2.0 need JSON support later, you can switch to Option B and add:
```xml
<ItemGroup Condition="'$(TargetFramework)' == 'netstandard2.0'">
  <PackageReference Include="System.Text.Json" Version="9.0.4" />
</ItemGroup>
```
Then only wrap the `IAsyncEnumerable` branch inside `SerializeAsync` with `#if NET8_0_OR_GREATER`.

### 2e. `Xml/XmlExtensions.cs` — async XML serialization

The `XmlSerializeAsync` methods use `IAsyncEnumerable` in `SerializeAsync`. Wrap the async methods that reference `IAsyncEnumerable`:

```csharp
#if NET8_0_OR_GREATER
        // The IAsyncEnumerable branch in SerializeAsync
        if (value is IAsyncEnumerable<object> asyncEnumerable)
        {
            // ...
        }
        else
#endif
        if (value is IEnumerable<object> enumerable)
        {
            // ...
        }
```

### 2f. `Text/TextExtensions.cs` — `StringSplitOptions.TrimEntries`

The `ExtractDates` method (line ~299) uses `StringSplitOptions.TrimEntries`. Wrap the split call:
```csharp
#if NET8_0_OR_GREATER
            var dates_string = text.Split(seperators?? new string[] { "|" },
                  StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .AsEnumerable();
#else
            var dates_string = text.Split(seperators?? new string[] { "|" },
                  StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .AsEnumerable();
#endif
```

### 2g. `Net/Mail/Message.cs` — `StringSplitOptions.TrimEntries`

The `ToStr`, `CcStr`, and `BccStr` property setters each split email addresses using `StringSplitOptions.TrimEntries`. There are 3 occurrences (lines ~45, ~71, ~96). For each, wrap:
```csharp
#if NET8_0_OR_GREATER
                foreach (var email in value.Split(new char[] { ',', ' ', ';', '\r', '\n' },
                  StringSplitOptions.RemoveEmptyEntries
                  | StringSplitOptions.TrimEntries).Where(x =>
                  !string.IsNullOrWhiteSpace(x)
                  ))
#else
                foreach (var email in value.Split(new char[] { ',', ' ', ';', '\r', '\n' },
                  StringSplitOptions.RemoveEmptyEntries)
                  .Select(x => x.Trim())
                  .Where(x => !string.IsNullOrWhiteSpace(x)
                  ))
#endif
```

### 2h. Range/Index syntax (`span[index..]`)

`System.Index` and `System.Range` are not available on netstandard2.0. Where used in `IOExtensions.cs`, these are already inside `#if NET8_0_OR_GREATER` blocks from Step 2a. Other locations outside `#if` blocks need guards:

- `Net/NetExtensions.cs` uses `[..(index)]` — change to:
```csharp
#if NET8_0_OR_GREATER
      baseUri[..(index + 1)]
#else
      baseUri.Substring(0, index + 1)
#endif
```

- `Linq/LinqExtensions.cs` uses `path[nodes[0].Length..]` in `FindDescendants` (line ~156) — change to:
```csharp
#if NET8_0_OR_GREATER
                return FindDescendants(traversableItem, path[nodes[0].Length..], findChildren, pathDelimiters);
#else
                return FindDescendants(traversableItem, path.Substring(nodes[0].Length), findChildren, pathDelimiters);
#endif
```

### 2i. `IO/FileSystemWatcherEx.cs`

Uses non-generic `TaskCompletionSource` (net5+ only) and `record` types. Wrap the entire file:
```csharp
#if NET8_0_OR_GREATER
// ... entire file content
#endif
```
(This file is also marked "InProgress" in its namespace, so excluding from netstandard2.0 is appropriate.)

---

## Step 3 — Minor code fixes for netstandard2.0 compatibility

These are small things the compiler will flag when you build for netstandard2.0.

### 3a. `ExpandoObject.TryAdd()` is not available

Anywhere you see `exObj.TryAdd(key, value)` on an `ExpandoObject`, netstandard2.0 needs:
```csharp
((IDictionary<string, object>)exObj)[key] = value;
```

Known locations (besides CsvExtensions already handled above):
- `Xml/Linq/XmlLinqExtensions.cs`

Use `#if NET8_0_OR_GREATER` / `#else` around each call.

### 3b. `ArgumentNullException.ThrowIfNull()` is net6+ only

Replace occurrences with:
```csharp
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(paramName);
#else
        if (paramName == null) throw new ArgumentNullException(nameof(paramName));
#endif
```

Known locations:
- `IO/IOExtensions.cs` — 1 occurrence in `IsWritableFolder`
- `Linq/LinqExtensions.cs` — 6 occurrences in `AggregateUntil` (3) and `AggregateWhile` (3)

### 3c. `init` accessors require `System.Runtime.CompilerServices.IsExternalInit`

On netstandard2.0, `init` accessors compile if `LangVersion` is high enough, **but** need a polyfill type. Add a file `IsExternalInit.cs` to the project:

```csharp
#if !NET5_0_OR_GREATER
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
#endif
```

This is a well-known polyfill. Known files using `init`: `Net/Mail/Message.cs`, `Net/Mail/MailAttachmentCollection.cs`, `Events/HGenericEventArgs.cs`.

### 3d. `await using` → `#if` or restructure

`await using var x = ...` requires `IAsyncDisposable` (net8+). In files NOT already fully wrapped in `#if NET8_0_OR_GREATER` (like WriteBase64 methods which are already wrapped), check for any remaining `await using` and replace with:
```csharp
#if NET8_0_OR_GREATER
            await using var fileStream = new FileStream(...);
#else
            using var fileStream = new FileStream(...);
#endif
```

### 3e. Range/Index syntax (`span[index..]`)

Already covered in Step 2h. Verify no other usages leak outside `#if` guards during the build step.

---

## Step 4 — Build and fix

Run:
```
dotnet build
```

The netstandard2.0 TFM will produce the most errors. Address them one by one — they will all be one of:
1. Missing API → wrap in `#if NET8_0_OR_GREATER`
2. `ExpandoObject.TryAdd` → cast to `IDictionary<string, object>`
3. `ArgumentNullException.ThrowIfNull` → manual null check
4. Range/Index syntax → `.Substring()` / `.Slice()`
5. `await using` → `using`

The net8/net9/net10 TFMs should compile without changes.

---

## Step 5 — Delete the old NetStandard2.0 project

Once `dotnet build` succeeds for all TFMs:

1. Remove `NetStandard2.0/` project from `Com.H.sln`
2. Delete the `NetStandard2.0/` folder
3. Update NuGet package version (suggest `10.1.0` to signal the consolidation)

---

## Step 6 — Verification

```
dotnet pack
```

Inspect the .nupkg — it should contain:
```
lib/
  netstandard2.0/Com.H.dll
  net8.0/Com.H.dll
  net9.0/Com.H.dll
  net10.0/Com.H.dll
```

Test that a netstandard2.0 consumer (e.g., a .NET Framework 4.8 console app or the Com.Nd.Omx library) can reference the package and compile.

---

## Summary of `#if` locations

| File | What's guarded | Why |
|------|---------------|-----|
| `IO/IOExtensions.cs` | `SearchValues`, `WriteBase64*`, `GetBase64DecodedSize`, `ArgumentNullException.ThrowIfNull` | net8+ APIs |
| `Text/Csv/CsvExtensions.cs` | `StringSplitOptions.TrimEntries`, `TryAdd` | net5+/net8+ API |
| `Text/TextExtensions.cs` | `StringSplitOptions.TrimEntries` in `ExtractDates` | net5+ API |
| `Net/Mail/Message.cs` | `StringSplitOptions.TrimEntries` in property setters (3×) | net5+ API |
| `Linq/LinqExtensions.cs` | `ArgumentNullException.ThrowIfNull` (6×), Range syntax in `FindDescendants` | net6+/net8+ APIs |
| `Linq/Async/LinqAsyncExtensions.cs` | Entire file | `IAsyncEnumerable` |
| `Collections/Generic/ChamberedAsyncEnumerable.cs` | Entire file | `IAsyncEnumerable` |
| `Collections/Generic/Extensions.cs` | Async methods only | `IAsyncEnumerable` |
| `Text/Json/JsonExtensions.cs` | Entire file | `System.Text.Json` + `IAsyncEnumerable` |
| `Xml/XmlExtensions.cs` | `IAsyncEnumerable` branch in `SerializeAsync` | `IAsyncEnumerable` |
| `Xml/Linq/XmlLinqExtensions.cs` | `TryAdd` calls | `ExpandoObject.TryAdd` |
| `IO/FileSystemWatcherEx.cs` | Entire file | `TaskCompletionSource` (non-generic) + `record` + InProgress |
| `Net/NetExtensions.cs` | Range syntax `[..(index)]` | `System.Range` |
| `Net/Mail/Message.cs` | (none — `init` handled by polyfill) | — |
| `Events/HGenericEventArgs.cs` | (none — `init` handled by polyfill) | — |
| *(new file)* `IsExternalInit.cs` | Polyfill for `init` accessor | netstandard2.0 |

---

## What NOT to change

- Do **not** remove nullable annotations (`string?`). They work on netstandard2.0 with `<LangVersion>latest</LangVersion>`.
- Do **not** revert `new()` target-typed expressions. They work everywhere with latest LangVersion.
- Do **not** revert file-scoped namespaces. They're a compiler feature.
- Do **not** revert `is not null` pattern matching. Compiler feature.
- Do **not** revert `using var` declarations (without `await`). They work on netstandard2.0.
