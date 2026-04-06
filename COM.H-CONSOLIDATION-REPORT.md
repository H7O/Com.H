# Com.H — NetStandard 2.0 Consolidation Report

**Date:** April 2026  
**Scope:** Merge the separate `NetStandard2.0/` project into the main `src/Com.H.csproj` as a single multi-targeting build.

---

## 1. Objective

The library previously shipped two independent `.csproj` files:

| Project | Target Frameworks |
|---|---|
| `src/Com.H.csproj` | net8.0 ; net9.0 ; net10.0 |
| `NetStandard2.0/NetStandard2.0.csproj` | netstandard2.0 |

Maintaining two separate projects meant duplicated source files, divergent bug fixes, and a higher risk of shipping mismatched behaviour between framework targets.

**Goal:** Consolidate into a single `src/Com.H.csproj` that multi-targets `netstandard2.0;net8.0;net9.0;net10.0`, using `#if` preprocessor directives to exclude modern-API-dependent code from the netstandard2.0 build.

---

## 2. Pre-Work — Playbook Review

Before touching any source code we reviewed the existing consolidation guide (`COM.H-PLAYBOOK.md`) and corrected five gaps:

1. **Missing `Text/TextExtensions.cs` coverage** — `StringSplitOptions.TrimEntries` in `ExtractDates` was not listed.
2. **Missing `Net/Mail/Message.cs` coverage** — three uses of `TrimEntries` in the `ToStr`/`CcStr`/`BccStr` setters.
3. **Missing `Linq/LinqExtensions.cs` ThrowIfNull + Range syntax** — six `ArgumentNullException.ThrowIfNull` calls and one `path[nodes[0].Length..]` range expression.
4. **Incorrect reason for `IO/FileSystemWatcherEx.cs`** — playbook cited `await using` but the real blockers are the non-generic `TaskCompletionSource` and C# `record` types.
5. **Structural fixes** — restored lost Steps 4–5, fixed section numbering, updated the summary table.

---

## 3. Pre-Work — Baseline Test Suite

To ensure no regressions, an xUnit test project was created **before** any source changes:

- **Project:** `tests/Com.H.Tests.csproj` (xUnit 2.9.3, net10.0)
- **81 tests** across 10 test files covering every code path expected to be affected by the consolidation:

| Test file | Tests | Coverage area |
|---|---|---|
| `CsvExtensionsTests.cs` | 6 | ParseCsv, ParsePsv, ParseDelimited |
| `TextExtensionsTests.cs` | 4 | ExtractDates |
| `MailMessageTests.cs` | 9 | ToStr / CcStr / BccStr setters |
| `IOExtensionsTests.cs` | 14 | ValidateFileName, GetBase64DecodedSize, WriteBase64*, IsWritableFolder |
| `LinqExtensionsTests.cs` | 9 | AggregateUntil, AggregateWhile, FindDescendants |
| `NetExtensionsTests.cs` | 5 | GetParentUri |
| `XmlExtensionsTests.cs` | 8 | ParseXml, AsDynamic, XmlSerializeAsync |
| `CollectionsAndAsyncTests.cs` | 8 | ChamberedEnumerable sync/async, ToListAsync |
| `JsonExtensionsTests.cs` | 5 | JsonSerializeAsync |
| `EventArgsTests.cs` | 2 | `init` property coverage (HGenericEventArgs) |

All 81 tests passed after every change cycle.

---

## 4. Project File Changes

### 4.1 `src/Com.H.csproj`

```xml
<TargetFrameworks>netstandard2.0;net8.0;net9.0;net10.0</TargetFrameworks>
<LangVersion>latest</LangVersion>
```

Conditional NuGet packages for netstandard2.0 only:

```xml
<ItemGroup Condition="'$(TargetFramework)' == 'netstandard2.0'">
  <PackageReference Include="Microsoft.CSharp" Version="4.7.0" />
  <PackageReference Include="System.Memory" Version="4.5.5" />
</ItemGroup>
```

- **`Microsoft.CSharp`** — required for `dynamic` / `ExpandoObject` support at runtime on netstandard2.0.
- **`System.Memory`** — provides `Span<T>`, `ReadOnlySpan<T>`, `AsSpan()`, etc.

### 4.2 Solution file

Removed `NetStandard2.0\NetStandard2.0.csproj` from `Com.H.sln`.  
The folder is retained for reference but is no longer part of the build.

---

## 5. Polyfill Files Created

### 5.1 `src/IsExternalInit.cs`

The `init` accessor keyword (C# 9) requires a compiler marker type that doesn't exist in netstandard2.0.

```csharp
#if !NET5_0_OR_GREATER
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
#endif
```

### 5.2 `src/MaybeNullWhenPolyfill.cs`

`[MaybeNullWhen(false)]` is used on 7 `out` parameters across 4 collection classes.

```csharp
#if NETSTANDARD2_0
namespace System.Diagnostics.CodeAnalysis
{
    [AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
    internal sealed class MaybeNullWhenAttribute : Attribute
    {
        public MaybeNullWhenAttribute(bool returnValue) => ReturnValue = returnValue;
        public bool ReturnValue { get; }
    }
}
#endif
```

---

## 6. Files Entirely Excluded from netstandard2.0

These files are wrapped top-to-bottom in `#if NET8_0_OR_GREATER … #endif` because they depend on APIs that have no reasonable netstandard2.0 polyfill:

| File | Blocking API |
|---|---|
| `Linq/Async/LinqAsyncExtensions.cs` | `IAsyncEnumerable<T>` |
| `Collections/Generic/ChamberedAsyncEnumerable.cs` | `IAsyncEnumerable<T>`, `IAsyncDisposable`, `ValueTask` |
| `Text/Json/JsonExtensions.cs` | `System.Text.Json` (entire namespace) |
| `IO/FileSystemWatcherEx.cs` | Non-generic `TaskCompletionSource`, `record` types |

---

## 7. Per-File `#if` Directive Changes

### 7.1 `IO/IOExtensions.cs`

| Issue | Resolution |
|---|---|
| `using System.Buffers` | `#if NET8_0_OR_GREATER` conditional import |
| `SearchValues<char> InvalidFileNameChars` | `#if` — `SearchValues` on net8+, `HashSet<char>` fallback on netstandard2.0 |
| File-name validation loop (`span[index..].IndexOfAny(SearchValues)`) | net8+ uses `SearchValues` + slice; netstandard2.0 uses simple `foreach` + `HashSet.Contains` |
| `ArgumentNullException.ThrowIfNull(uri)` in `IsWritableFolder` | Replaced with manual `if (uri == null) throw new ArgumentNullException(nameof(uri))` |
| `WriteBase64ToFileAsync`, `WriteBase64ToTempFileAsync`, `GetBase64DecodedSize` | Entire `#region` wrapped in `#if NET8_0_OR_GREATER` (ArrayPool + await using + AsSpan/AsMemory patterns) |

### 7.2 `Text/Csv/CsvExtensions.cs`

| Issue | Resolution |
|---|---|
| `StringSplitOptions.TrimEntries` (3 occurrences) | `#if NET5_0_OR_GREATER` for the flag; `#else` fallback with `.Trim()` / `.Select(x => x.Trim())` |
| `ExpandoObject.TryAdd(key, value)` | `#if NET5_0_OR_GREATER` keeps `TryAdd`; `#else` uses `((IDictionary<string, object?>)exObj)[key] = value` (netstandard2.0 only) |
| `WriteAsync(string.AsMemory(), CancellationToken)` | `#if NET8_0_OR_GREATER` for Memory overload; `#else` plain `WriteAsync(string)` |

### 7.3 `Text/TextExtensions.cs`

| Issue | Resolution |
|---|---|
| `StringSplitOptions.TrimEntries` in `ExtractDates` | `#if NET5_0_OR_GREATER` flag; `#else` post-split `.Select(x => x.Trim())` |
| `MatchCollection.Where(…)` | Added `.Cast<Match>()` before `.Where()` (netstandard2.0 `MatchCollection` lacks generic `IEnumerable<Match>`) |
| `string.Replace(string, string, bool, CultureInfo)` (2 calls) | `#if NET8_0_OR_GREATER` keeps original; `#else` uses `Regex.Replace` with `RegexOptions.IgnoreCase` |

### 7.4 `Net/Mail/Message.cs`

| Issue | Resolution |
|---|---|
| `StringSplitOptions.TrimEntries` (3 setters: To, Cc, Bcc) | `#if NET5_0_OR_GREATER` conditional flag |
| `string.Join(char, IEnumerable)` (3 getters) | Changed `','` to `","` (string overload available on all TFMs) |
| `SmtpClient.SendMailAsync(MailMessage, CancellationToken)` | `#if NET8_0_OR_GREATER` for CancellationToken overload; `#else` falls back to parameterless overload |

### 7.5 `Net/Mail/MailAttachmentCollection.cs`

| Issue | Resolution |
|---|---|
| `Stream.CopyToAsync(Stream, CancellationToken)` | Changed to `CopyToAsync(Stream, int, CancellationToken)` with explicit `bufferSize: 81920` (available on netstandard2.0) |

### 7.6 `Net/NetExtensions.cs`

| Issue | Resolution |
|---|---|
| Range syntax `uriPath[..(lastIndexOfSeperator + 1)]` | Replaced with `uriPath.Substring(0, lastIndexOfSeperator + 1)` |
| `HttpClient.GetByteArrayAsync(Uri, CancellationToken)` | `#if NET8_0_OR_GREATER` keeps CancellationToken overload; `#else` calls `GetByteArrayAsync(string)` |
| `File.ReadAllTextAsync` (doesn't exist in netstandard2.0) | `#if NET8_0_OR_GREATER` keeps async; `#else` wraps `File.ReadAllText` in `Task.FromResult` |

### 7.7 `Xml/XmlExtensions.cs`

| Issue | Resolution |
|---|---|
| `IAsyncEnumerable<object>` branch in `SerializeAsync` | `#if NET8_0_OR_GREATER` for the async enumerable branch; `#else` falls through directly to `IEnumerable<object>` |

### 7.8 `Xml/Linq/XmlLinqExtensions.cs`

| Issue | Resolution |
|---|---|
| `ExpandoObject.TryAdd` (2 occurrences in `AsDynamic`) | `#if NET5_0_OR_GREATER` keeps `TryAdd`; `#else` uses `((IDictionary<string, object?>)obj)[key] = value` (netstandard2.0 only) |

### 7.9 `Linq/LinqExtensions.cs`

| Issue | Resolution |
|---|---|
| Range syntax `path[nodes[0].Length..]` | Replaced with `path.Substring(nodes[0].Length)` |
| `ArgumentNullException.ThrowIfNull` (6 calls in AggregateUntil/AggregateWhile) | Replaced with manual `if (x == null) throw new ArgumentNullException(nameof(x))` |

### 7.10 `Collections/Generic/ChamberedEnumerable.cs`

| Issue | Resolution |
|---|---|
| `IAsyncDisposable` interface implementation | `#if NET8_0_OR_GREATER` conditional interface |
| `ValueTask DisposeAsync()` method | `#if NET8_0_OR_GREATER` conditional method |

### 7.11 `Collections/Generic/Extensions.cs`

| Issue | Resolution |
|---|---|
| `ToChamberedEnumerableAsync`, `RemainingItemsAsync`, `EmptyAsyncEnumerable`, `ToAsyncEnumerable`, `ConcatAsyncEnumerables` | All wrapped in `#if NET8_0_OR_GREATER` (depend on `IAsyncEnumerable`) |
| Sync methods (`ToChamberedEnumerable`, `RemainingItems`) | Kept available on all TFMs |

### 7.12 `Reflection/DataMapper.cs`

| Issue | Resolution |
|---|---|
| `using System.ComponentModel.DataAnnotations.Schema` | `#if NET8_0_OR_GREATER` conditional import (actual `ColumnAttribute` usage was already commented out) |

### 7.13 `Pdf/ExternalPdfConverter.cs`

| Issue | Resolution |
|---|---|
| `OSPlatform.FreeBSD` (not defined in netstandard2.0) | `#if NET8_0_OR_GREATER` around the FreeBSD detection block |

### 7.14 `Threading/Awaiter.cs`

| Issue | Resolution |
|---|---|
| `new Lazy<CancellationTokenSource>(new CTS())` (2 sites) | `#if NET5_0_OR_GREATER` keeps the original `Lazy<T>(T value)` constructor (lock-free, immediate `IsValueCreated`); `#else` uses `Lazy<T>(() => value)` lambda form for netstandard2.0 |

### 7.15 Lazy Constructor Fix (4 collection files)

The `Lazy<T>(T value)` constructor was introduced in .NET 5. All four `LazyConcurrent*` dictionary classes and `Awaiter.cs` used it:

- `Collections/Concurrent/LazyConcurrentDictionary.cs` (4 occurrences)
- `Collections/Concurrent/LazyConcurrentSortedDictionary.cs` (5 occurrences)
- `Collections/Concurrent/LazyConcurrentLimitedSortedDictionary.cs` (4 occurrences)
- `Threading/Awaiter.cs` (2 occurrences)

**Resolution:** `#if NET5_0_OR_GREATER` preserves the original `new Lazy<T>(value)` constructor on net5+ (lock-free, `IsValueCreated == true` immediately); `#else` falls back to `new Lazy<T>(() => value)` for netstandard2.0 compatibility. This ensures net8/9/10 consumers see identical threading behaviour to the pre-consolidation code.

---

## 8. What Is NOT Available on netstandard2.0

The following functionality compiles only for net8.0+ consumers:

| Feature | Reason |
|---|---|
| `IAsyncEnumerable<T>` / `IAsyncDisposable` / async streams | No runtime support in netstandard2.0 |
| `System.Text.Json` serialization (`JsonExtensions`) | Namespace not available |
| `SearchValues<char>` high-perf file-name validation | API introduced in .NET 8 |
| `WriteBase64ToFileAsync` / `WriteBase64ToTempFileAsync` | ArrayPool + AsMemory streaming pattern |
| `FileSystemWatcherEx` | Non-generic `TaskCompletionSource`, `record` types |
| `OSPlatform.FreeBSD` detection in PDF converter | Constant not defined |
| `SmtpClient.SendMailAsync` CancellationToken overload | Overload not available |
| `File.ReadAllTextAsync` | API not available (sync fallback provided) |

All other functionality — CSV/PSV parsing, XML serialization, template rendering, date extraction, collections, caching, SSH, reflection mapping, etc. — is fully available on netstandard2.0.

---

## 9. Build & Test Results

```
Build succeeded.
    0 Warning(s)
    0 Error(s)

Targets built: netstandard2.0, net8.0, net9.0, net10.0
```

```
Test run: 81 passed, 0 failed, 0 skipped
```

---

## 10. Post-Consolidation Cleanup (Completed)

- **`NetStandard2.0/` folder deleted** — removed from the solution and from disk.
- **Version bumped** — `10.0.0.1` → `10.1.0.0` in `src/Com.H.csproj`.
- **`nuget.md` updated** — replaced old "separate package" guidance with a target framework table.
- **`README.md` updated** — added Installation section, Target Frameworks table, and License section.

---

## 11. Net8/9/10 Behavioral Safety Audit

After the initial consolidation, a full `git diff` audit identified two categories of changes that subtly altered runtime behaviour on net8/9/10 (not just netstandard2.0). Both were corrected using `#if NET5_0_OR_GREATER` directives so that the net8/9/10 code path is **identical** to pre-consolidation.

### 11.1 `TryAdd` → IDictionary Indexer (semantic change)

`ExpandoObject.TryAdd(key, value)` is first-wins for duplicate keys, while `((IDictionary)obj)[key] = value` is last-wins. The initial consolidation replaced `TryAdd` on all TFMs, which changed duplicate-key semantics on net8+.

**Fix:** `#if NET5_0_OR_GREATER` keeps `TryAdd` (original behaviour); `#else` uses IDictionary indexer only on netstandard2.0.

| File | Sites |
|---|---|
| `Text/Csv/CsvExtensions.cs` | 1 |
| `Xml/Linq/XmlLinqExtensions.cs` | 2 |

### 11.2 `Lazy<T>(T value)` → `Lazy<T>(() => value)` (threading change)

The `Lazy<T>(T value)` constructor (introduced in .NET 5) is lock-free and sets `IsValueCreated = true` immediately. The lambda form `Lazy<T>(() => value)` takes a brief lock on first `.Value` access. The initial consolidation replaced all sites with the lambda form on all TFMs.

**Fix:** `#if NET5_0_OR_GREATER` preserves the original `Lazy<T>(value)` constructor; `#else` uses `Lazy<T>(() => value)` for netstandard2.0.

| File | Sites |
|---|---|
| `Collections/Concurrent/LazyConcurrentDictionary.cs` | 4 |
| `Collections/Concurrent/LazyConcurrentSortedDictionary.cs` | 5 |
| `Collections/Concurrent/LazyConcurrentLimitedSortedDictionary.cs` | 4 |
| `Threading/Awaiter.cs` | 2 |

### 11.3 Verified No-Impact Changes

All other consolidation changes were confirmed as behaviourally identical on net8/9/10:

| Change | Why it's safe |
|---|---|
| `ThrowIfNull` → manual `if (x == null) throw` | Identical exception, identical message |
| `string.Join(char)` → `string.Join(string)` | Identical output |
| Range syntax `[n..]` → `Substring(n)` | Identical semantics |
| `CopyToAsync(stream, token)` → `CopyToAsync(stream, 81920, token)` | 81920 is the default buffer size |
| `MatchCollection.Where()` → `.Cast<Match>().Where()` | Cast is a no-op on net8+ (already `IEnumerable<Match>`) |
| `string.Replace(4-arg)` guarded with `#if NET8_0_OR_GREATER` | Net8+ path unchanged |
| `File.ReadAllTextAsync` guarded with `#if NET8_0_OR_GREATER` | Net8+ path unchanged |
| `SmtpClient.SendMailAsync(msg, token)` guarded with `#if NET8_0_OR_GREATER` | Net8+ path unchanged |

---

## 12. Final State

- **Build:** 0 errors across all 4 TFMs (`netstandard2.0`, `net8.0`, `net9.0`, `net10.0`)
- **Tests:** 81/81 passed
- **Net8/9/10 behaviour:** Verified identical to pre-consolidation
- **Version:** 10.1.0.0
