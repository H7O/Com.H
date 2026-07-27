# Shared classes Com.H ↔ Com.H.Data.Common — backport audit

**Date:** 2026-04-22
**Trigger:** After shipping Com.H.Data.Common v10.1.0.7, the question came up: which shared-class enhancements from the sibling repo should be backported to Com.H?
**Companion note:** [CHAMBERED-DISPOSAL-HANDOFF.md](CHAMBERED-DISPOSAL-HANDOFF.md) — a separate, unrelated handoff for the `ChamberedEnumerable.Dispose` fix.

---

## TL;DR — the direction is mostly the OPPOSITE of what we assumed

Initial working assumption: "Com.H.Data.Common is the latest; backport its fixes to Com.H."

**Actual finding after diffing all 8 shared files:** Com.H is the more polished copy in 6 of 8 files. Only **DataExtensions.cs** has substantial enhancements flowing the "expected" direction (Com.H.Data.Common → Com.H). One shared file has a **latent dead-code bug in Com.H.Data.Common** that should be backported FROM Com.H.

The sync-over-async `ConfigureAwait(false)` fix we shipped yesterday in Com.H.Data.Common v10.1.0.6 **does not apply to any shared class** — grepping `await` across Com.H.Data.Common/src shows awaits only in the three library-specific files (`AdoNetExt.cs`, `DbAsyncQueryResult.cs`, `DbQueryResult.cs`), which aren't shared. Nothing to backport from that work.

---

## Inventory (confirmed complete)

The 8 shared files the user listed from memory are the complete set — scan of both repos confirms no missing pairs.

| Shared class | Com.H location | Com.H.Data.Common location |
|---|---|---|
| DataMapper | [src/Reflection/DataMapper.cs](src/Reflection/DataMapper.cs) | `../Com.H.Data.Common/src/DataMapper.cs` |
| DataExtensions | [src/Data/DataExtensions.cs](src/Data/DataExtensions.cs) | `../Com.H.Data.Common/src/DataExtensions.cs` |
| DynamicPropertyInfo | [src/Reflection/DynamicPropertyInfo.cs](src/Reflection/DynamicPropertyInfo.cs) | `../Com.H.Data.Common/src/DynamicPropertyInfo.cs` |
| JoinExtensions | [src/Linq/JoinExtensions.cs](src/Linq/JoinExtensions.cs) | `../Com.H.Data.Common/src/JoinExtensions.cs` |
| JsonExtensions | [src/Text/Json/JsonExtensions.cs](src/Text/Json/JsonExtensions.cs) | `../Com.H.Data.Common/src/JsonExtensions.cs` |
| LinqExtensions | [src/Linq/LinqExtensions.cs](src/Linq/LinqExtensions.cs) | `../Com.H.Data.Common/src/LinqExtensions.cs` |
| ReflectionExtensions | [src/Reflection/ReflectionExtensions.cs](src/Reflection/ReflectionExtensions.cs) | `../Com.H.Data.Common/src/ReflectionExtensions.cs` |
| XmlLinqExtensions | [src/Xml/Linq/XmlLinqExtensions.cs](src/Xml/Linq/XmlLinqExtensions.cs) | `../Com.H.Data.Common/src/XmlLinqExtensions.cs` |

Reminder: every Com.H.Data.Common copy is `internal` (avoids ambiguous-reference headaches for consumers who import both libs); every Com.H copy is `public` — that's the structural difference to ignore when diffing.

---

## File-by-file status

### DataMapper.cs — **no backport needed**

Both sides are functionally identical. Com.H has richer XML docs (every method has `<summary>`/`<param>`/`<returns>`), which Com.H.Data.Common is missing. Behavior is the same; the only real code difference is the static-call vs extension-method style for `LeftJoin` — semantically equivalent.

**Action for Com.H:** none.
**Opportunistic Com.H.Data.Common hygiene (not urgent):** copy the XML docs in from Com.H so the `.xml` documentation shipped in the nupkg is richer.

### DataExtensions.cs — **this is the big one**

Com.H.Data.Common's copy is ~4× larger (415 vs 109 lines) and has substantially richer `GetDataModelParameters`:

- Supports `JsonElement`, JSON strings, `IDictionary<string,string>`, `IEnumerable<KeyValuePair<string,object>>`, `IEnumerable<KeyValuePair<string,string>>`
- `caseSensitive` parameter
- Case-insensitive default via `StringComparer.OrdinalIgnoreCase`
- Nested JSON/XML handled (raw text for arrays/objects)
- Rich XML docs with `<example>` blocks

**However, not all of it is portable:** the `Fill` method and the `ReduceToUnique` method reference `DbQueryParams` / `DbQueryParamsRegex` / `DefaultRegex.DefaultPreviousQueryVariablesPattern` — lib-specific types. Those can't be dropped straight into Com.H.

**Portable subset (safe to backport as-is):**
- Everything inside `GetDataModelParameters(object, bool, bool)` — the richer type-dispatch logic.
- The `_jsonOptions` field.

**Non-portable parts (skip or generalize):**
- `Fill` extension — it's a templating helper driven by `IEnumerable<DbQueryParams>`. If you want templating in Com.H, it's a non-trivial port: you'd need to introduce a Com.H-side `QueryParams` class (Com.H already has a `QueryParams` class at the top of this file with a different shape! Design collision to resolve).
- `ReduceToUnique` extension — tightly coupled to `DbQueryParams`. Same design-collision concern.

**Action for Com.H:** merge the richer `GetDataModelParameters` body only. Leave `Fill` / `ReduceToUnique` for a later design decision (unify `QueryParams` shape between the two repos first, or leave them as a Com.H.Data.Common-only concept).

### DynamicPropertyInfo.cs — **no backport needed**

Identical after normalizing for using-imports, namespace, and access modifier.

### JoinExtensions.cs — **Com.H is ahead, nothing to backport**

Com.H has:
- Typo fix: "outter" → "outer"
- Fully filled-in XML docs (`typeparam` descriptions, `param` descriptions, `returns` descriptions)
- XML docs on the `Merge` method (Com.H.Data.Common's copy has none)

**Action for Com.H:** none — already ahead.
**Opportunistic Com.H.Data.Common hygiene:** copy Com.H's improved docs over.

### JsonExtensions.cs — **Com.H is a superset**

Com.H has a lot of functionality Com.H.Data.Common doesn't need and therefore didn't copy:
- `JsonSerializeAsync` overloads (Stream, IBufferWriter, Utf8JsonWriter)
- `SerializeAsync` extension with streaming/`IAsyncEnumerable` support
- `IsJsonPrimitive` helper
- Commented-out `DeferredWriteAsJsonAsync` sketch

Only two small behavioral differences in the shared subset:
- **Unknown `JsonValueKind` handling** — Com.H throws `ArgumentOutOfRangeException` for unmapped kinds; Com.H.Data.Common returns `null`. Com.H.Data.Common is more lenient; Com.H is stricter. I'd keep Com.H's `throw` — it's the correct public-API behavior, and Com.H.Data.Common's leniency made sense when the code was copied into an internal-only context.

**Action for Com.H:** none.

### LinqExtensions.cs — **⚠ latent bug in Com.H.Data.Common worth fixing**

Com.H has the **correct** implementation for char-delimiter overloads:

```csharp
// Com.H — correct
pathDelimiters.Select(x => x.ToString()).ToArray()
```

Com.H.Data.Common has a **latent bug** that analyzer CA2021 flags:

```csharp
// Com.H.Data.Common — throws InvalidCastException if ever called
pathDelimiters.Cast<string>().ToArray()
```

`char` can't be cast to `string`, so `Cast<string>()` on a `char[]` throws at runtime. It happens to be dead code inside Com.H.Data.Common today (the library only calls the `string[]` overloads internally), so users don't see it — but if anyone ever calls the char-delimiter overload it blows up.

**Action for Com.H:** none.
**Action for Com.H.Data.Common (recommended patch):** backport Com.H's `.Select(x => x.ToString()).ToArray()` fix. Small, zero-risk, silences the CA2021 warnings that already exist in the build output. Worth a 10.1.0.8 patch.

Other minor differences in this file:
- Com.H.Data.Common has the `#if NETSTANDARD2_0` Range-syntax guard I added yesterday. Com.H doesn't — but it still compiles on netstandard2.0 clean (modern SDKs provide an implicit `System.Index`/`System.Range` polyfill for target framework reference assemblies). Not urgent but Com.H may want the explicit guard eventually if it ever downgrades to an older SDK.

### ReflectionExtensions.cs — **Com.H is ahead, nothing to backport**

Com.H has:
- Typo fixes: "Rrturns" → "Returns", "conerted" → "converted", "IDicionary" → "IDictionary"
- Full XML docs on every method
- **Extra method `GetEnumIntValues<TEnum>()`** — a useful enum-reflection helper that doesn't exist in the Com.H.Data.Common copy. If the library ever needs it, we'd copy it over, but right now there's no caller.

**Action for Com.H:** none.
**Opportunistic Com.H.Data.Common hygiene:** if we ever need enum→int mapping inside Com.H.Data.Common, grab Com.H's method.

### XmlLinqExtensions.cs — **essentially equivalent, minor hygiene differences**

- Com.H returns `dynamic?` (nullable-annotated) vs Com.H.Data.Common's `dynamic`. Both compile; Com.H is more precise under Nullable enabled.
- Com.H has a `#if NET5_0_OR_GREATER` fallback for `ExpandoObject.TryAdd` (uses dictionary-index assignment on older TFMs). Com.H.Data.Common relies on the `Polyfills.cs` `IDictionary<,>.TryAdd` extension I added yesterday — different mechanism, same effective outcome.
- Style: Com.H uses extension-method syntax (`x.AsDynamic(true)`); Com.H.Data.Common uses static-call syntax (`AsDynamic(x, true)`).

Functionally equivalent on all targeted TFMs.

**Action for Com.H:** none.

---

## Summary: what to do in the next Com.H session

Ranked by value:

1. **DataExtensions.cs — port the richer `GetDataModelParameters` body.** Highest-value change. Gives Com.H consumers the same JSON-element / string-pair / case-sensitivity support that Com.H.Data.Common consumers already have. Skip `Fill` and `ReduceToUnique` unless/until we unify `QueryParams` shapes.

2. (Optional, no urgency) **Push XML doc improvements & typo fixes from Com.H → Com.H.Data.Common** for DataMapper / JoinExtensions / ReflectionExtensions, so the `.xml` docs shipped with the nupkg are first-class. These are cosmetic but nice.

3. (Recommended, small patch in the *other* repo) **Com.H.Data.Common LinqExtensions char-delimiter bug fix** — backport Com.H's `.Select(x => x.ToString()).ToArray()` replacement for the broken `.Cast<string>().ToArray()`. Dead code today but it's an active CA2021 warning; may as well fix it when we next touch Com.H.Data.Common. Worth a 10.1.0.8 patch.

**Not on the list:**
- No `ConfigureAwait(false)` backport — the shared classes have no `await` calls. The deadlock fix in Com.H.Data.Common v10.1.0.6 is entirely contained within library-specific files and doesn't cross into shared code.
- No auto-close-reader backport — same reason, the iterator that got the `try/finally` is lib-specific (`CreateAsyncEnumerableFromReader` in `AdoNetExt.cs`).

---

## Strategic note (for whenever you pick this back up)

The user mentioned considering either (a) removing the embedded shared classes from Com.H.Data.Common and taking a dependency on Com.H, or (b) moving Com.H.Data.Common's functionality into Com.H entirely now that .NET 10's BCL covers everything.

If (b) happens, this whole backport concern evaporates — there's only one copy. If (a), the duplication stays but at least the copies in Com.H.Data.Common are internal, so consumer-side name collisions are already a non-issue. The main reason the duplication *still* exists is purely packaging (don't force Com.H.Data.Common users to download Com.H for utility classes they'd pay for in transitive weight).

Either migration is a bigger decision than the audit above; this note doesn't attempt to recommend one. Just flagging so next-you doesn't lose that context.

---

## Related external commits

- Com.H.Data.Common `27c21da` (v10.1.0.7) — Auto-close reader in iterator exit
- Com.H.Data.Common `e4a394f` (v10.1.0.6) — `ConfigureAwait(false)` / sync-over-async fix
