# turso-dotnet-with-fts-enabled

Windows builds of Turso's .NET native library (`turso_sdk_kit.dll`) with [full-text search](https://docs.turso.tech/sql-reference/functions/fts) compiled in.

The published `Turso.Data.Native` NuGet package is built without turso_core's optional `fts` feature, and the .NET provider has no connection-string key that enables the experimental index method for a local database. So `CREATE INDEX … USING fts` and `fts_match` fail through `Turso.Data.Sqlite`. These builds change two things and nothing else:

1. `cargo build -p turso_sync_sdk_kit --features turso_core/fts`, following Turso's own Windows recipe (`bindings/dotnet/Makefile`, `release-official` profile).
2. `DatabaseOpts` start with the index method enabled (`sdk-kit/src/rsapi.rs`), so no driver change is needed.

## Using a release

Each release `v<turso>-fts.<n>` carries `turso_sdk_kit-win-arm64.dll`, `turso_sdk_kit-win-x64.dll`, `SHA256SUMS` and Turso's license. Use it with the **same** `Turso.Data.Sqlite.Provider` version (`<turso>`), because the managed layer calls the native library directly. Reference `Turso.Data.Native` with `ExcludeAssets="native"` and copy the matching DLL into the output as `turso_sdk_kit.dll`. Always check the download against a hash you trust.

## Releasing a Turso version

1. Set `turso.version` to the Turso release (without `v`) and push. CI builds both DLLs and smoke-tests each on its own architecture (`smoke/`).
2. Tag `v<turso>-fts.1` (bump `.n` to rebuild the same version). The tag must match `turso.version`. CI publishes the release.

## Building locally

Needs rustup and Visual Studio's C++ build tools (x64 and ARM64).

```powershell
./build/Build-TursoNative.ps1 -Version 0.8.1 -Target aarch64-pc-windows-msvc -OutDir out/win-arm64
dotnet run --project smoke/FtsSmoke.csproj -r win-arm64 -p:NativeLibrary=$PWD/out/win-arm64/turso_sdk_kit.dll
```

Turso is © Turso, Inc. and contributors, MIT licensed. The scripts in this repository are MIT licensed.
