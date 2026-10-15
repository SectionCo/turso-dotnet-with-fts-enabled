<#
.SYNOPSIS
Builds Turso's .NET native library (turso_sdk_kit.dll) with full-text search compiled in and the index method enabled.

.DESCRIPTION
Follows Turso's own Windows recipe (bindings/dotnet/Makefile, release-official profile) for one Rust target, with two
changes: turso_core's optional `fts` feature is turned on, and DatabaseOpts start with the index method enabled, because
the .NET provider has no connection-string key that passes experimental features to a local database.

.EXAMPLE
./build/Build-TursoNative.ps1 -Version 0.8.1 -Target aarch64-pc-windows-msvc -OutDir ./out/win-arm64
#>
param(
	# Turso release without the leading "v"; must match the Turso.Data.Sqlite.Provider version the app references.
	[Parameter(Mandatory)] [string] $Version,
	# Rust target triple: aarch64-pc-windows-msvc or x86_64-pc-windows-msvc.
	[Parameter(Mandatory)] [ValidateSet('aarch64-pc-windows-msvc', 'x86_64-pc-windows-msvc')] [string] $Target,
	# Folder that receives turso_sdk_kit.dll.
	[Parameter(Mandatory)] [string] $OutDir,
	# Working folder for the Turso source checkout; reused between runs.
	[string] $SourceDir = (Join-Path $PSScriptRoot '..' 'work' "turso-$Version")
)

# Initialize Variables
$ErrorActionPreference = 'Stop'
$tag = "v$Version"
$profileName = 'release-official'
$rsapi = Join-Path $SourceDir 'sdk-kit' 'src' 'rsapi.rs'
$defaultOpts = 'let mut opts = DatabaseOpts::new();'
$patchedOpts = 'let mut opts = DatabaseOpts::new().with_index_method(true);'
$targetDir = Join-Path $SourceDir 'rs_compiled'
$builtDll = Join-Path $targetDir $Target $profileName 'turso_sync_sdk_kit.dll'

# Fetch the exact release tag, so the native library matches the managed package's ABI.
if (-not (Test-Path (Join-Path $SourceDir '.git'))) {
	git clone --depth 1 --branch $tag https://github.com/tursodatabase/turso.git $SourceDir
	if ($LASTEXITCODE -ne 0) { throw "Cloning Turso $tag failed." }
}

# Enable the index method by default. The edit must match exactly once, so an upstream change fails loudly instead of
# silently shipping a library where CREATE INDEX ... USING fts is refused.
$source = Get-Content $rsapi -Raw
$count = ([regex]::Matches($source, [regex]::Escape($defaultOpts))).Count
if ($count -eq 1) {
	Set-Content -Path $rsapi -Value $source.Replace($defaultOpts, $patchedOpts) -NoNewline
}
elseif (-not $source.Contains($patchedOpts)) {
	throw "Expected one '$defaultOpts' in $rsapi, found $count. Update the patch for Turso $tag."
}

# Build the sync SDK kit (the library the .NET provider loads) with turso_core's fts feature.
rustup target add $Target
if ($LASTEXITCODE -ne 0) { throw "rustup could not add $Target." }
Push-Location $SourceDir
try {
	cargo build -p turso_sync_sdk_kit --target-dir $targetDir --target $Target --profile $profileName --features turso_core/fts
	if ($LASTEXITCODE -ne 0) { throw "cargo build failed for $Target." }
}
finally {
	Pop-Location
}

# The provider loads the library under the sdk-kit name, as Turso's Makefile renames it.
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
Copy-Item -Force $builtDll (Join-Path $OutDir 'turso_sdk_kit.dll')
Write-Output "Wrote $(Join-Path $OutDir 'turso_sdk_kit.dll')"
