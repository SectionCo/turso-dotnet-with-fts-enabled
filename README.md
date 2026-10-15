# turso-dotnet-with-fts-enabled

Windows builds of Turso's .NET native library (`turso_sdk_kit.dll`) with [full-text search](https://docs.turso.tech/sql-reference/functions/fts) compiled in.

The published `Turso.Data.Native` NuGet package is built without turso_core's optional `fts` feature, and the .NET provider has no connection-string key that enables the experimental index method for a local database. So `CREATE INDEX … USING fts` and `fts_match` fail through `Turso.Data.Sqlite`. These builds change two things and nothing else:

1. `cargo build -p turso_sync_sdk_kit --features turso_core/fts`, following Turso's own Windows recipe (`bindings/dotnet/Makefile`, `release-official` profile).
2. `DatabaseOpts` start with the index method enabled (`sdk-kit/src/rsapi.rs`), so no driver change is needed.

## Using a release

Each release `v<turso>-fts.<n>` carries `turso_sdk_kit-win-arm64.dll`, `turso_sdk_kit-win-x64.dll`, `SHA256SUMS` and Turso's license. Use it with the **same** `Turso.Data.Sqlite.Provider` version (`<turso>`), because the managed layer calls the native library directly. Reference `Turso.Data.Native` with `ExcludeAssets="native"` and copy the matching DLL into the output as `turso_sdk_kit.dll`. Always check the download against a hash you trust.

## Releasing a Turso version

This is automatic apart from one merge:

1. **Turso update** (`.github/workflows/turso-update.yml`) runs daily. When NuGet has a newer stable `Turso.Data.Sqlite.Provider` (and `Turso.Data.Native`) and Turso has the matching `v<version>` tag, it opens a pull request from `turso/v<version>` that bumps `turso.version`, and starts **Build** on it. Run it by hand from the Actions tab to check now.
2. **Build** compiles both DLLs at that tag and smoke-tests each on its own architecture (`smoke/`). A failure means Turso changed something our build depends on; fix it on that branch.
3. **Merge the pull request.** Build runs on `main`, sees that `v<version>-fts.*` has never been released, and publishes `v<version>-fts.1`.

To rebuild a version that is already released (for example after changing the build script), push a tag `v<turso>-fts.<n+1>` by hand. The tag must match `turso.version`. Closing an update pull request without merging keeps its branch, so that version is not proposed again; delete the branch to have it reopened.

Apps don't change on their own: Burvo Windows pins the Turso version and each DLL's SHA-256, and updates them in its own pull request.

## Building locally

Needs rustup and Visual Studio's C++ build tools (x64 and ARM64).

```powershell
./build/Build-TursoNative.ps1 -Version 0.8.1 -Target aarch64-pc-windows-msvc -OutDir out/win-arm64
dotnet run --project smoke/FtsSmoke.csproj -r win-arm64 -p:NativeLibrary=$PWD/out/win-arm64/turso_sdk_kit.dll
```

Turso is © Turso, Inc. and contributors, MIT licensed. The scripts in this repository are MIT licensed.
