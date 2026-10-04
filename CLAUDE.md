# Com.H

## Source stepping: fixed, keep it that way

`src/Com.H.csproj` sets `<EmbedAllSources>true</EmbedAllSources>` (applied 2026-10-04), so the
source of every file is inside the package's PDB. Do not remove it.

Without it, Source Link alone does not guarantee downstream developers can step into this
library's source. If the drive-letter casing used to invoke the build differs from what git
reports for the repo root (`c:\` vs `C:\`), the compiler silently skips the source-path rewrite
that Source Link depends on, and stepping then fails for some target frameworks. There is no
warning or error — the package looks fine.

**[SOURCELINK-STEPPING.md](SOURCELINK-STEPPING.md)** has the full explanation, a verified
reproduction, and a small program that checks a built assembly and tells you whether each target
framework will actually step.

Sibling repos: `Com.H.Data.Common` still has the issue and its own copy of the note.
`Com.H.Net.Ssh` was fixed on 2026-08-31.

## Packing

Every Release build sets `ContinuousIntegrationBuild`, and a pack of any other configuration
stops with an error before it builds, so Visual Studio's right-click Pack and this command
produce the same package:

```powershell
dotnet pack .\src\Com.H.csproj -c Release
```

Commit and push before packing, so the commit the PDB points at exists on GitHub.
