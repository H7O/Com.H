# Com.H

## Known issue: source stepping can ship broken, silently

Before packing or publishing this package, read **[SOURCELINK-STEPPING.md](SOURCELINK-STEPPING.md)**.

Short version: Source Link is configured correctly here, but that alone does not guarantee
downstream developers can step into this library's source. If the drive-letter casing used to
invoke the build differs from what git reports for the repo root (`c:\` vs `C:\`), the compiler
silently skips the source-path rewrite that Source Link depends on, and stepping then fails for
some target frameworks. There is no warning or error — the package looks fine.

The fix is one line in `src/Com.H.csproj`, not yet applied here:

```xml
<EmbedAllSources>true</EmbedAllSources>
```

`SOURCELINK-STEPPING.md` has the full explanation, a verified reproduction, and a small program
that checks a built assembly and tells you whether each target framework will actually step.

Sibling repos: `Com.H.Data.Common` has the same issue and the same note. `Com.H.Net.Ssh` was
fixed on 2026-08-31 and is the working reference.

## Packing

Local packs need this flag, or the build is not deterministic and paths are not normalised
(CI sets it automatically via `GITHUB_ACTIONS`):

```powershell
dotnet pack .\src\Com.H.csproj -c Release -p:ContinuousIntegrationBuild=true
```

Commit and push before packing, so the commit the PDB points at exists on GitHub.
