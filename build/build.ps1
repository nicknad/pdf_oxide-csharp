param(
    [ValidateSet("Windows", "Linux", "All")]
    [string] $Target = "All",
    [string] $LinuxImage = "rust:1-bookworm"
)

$ErrorActionPreference = "Stop"

$Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$RustManifest = Join-Path $Root "rust/Cargo.toml"
$RuntimeRoot = Join-Path $Root "runtimes"
$BindingProject = Join-Path $Root "csharp/PdfOxideBindings/PdfOxideBindings.csproj"
$TestProject = Join-Path $Root "csharp/PdfOxideTests/PdfOxideTests.csproj"

function Invoke-NativeBuild {
    param(
        [Parameter(Mandatory)] [string] $Rid,
        [Parameter(Mandatory)] [string] $Source,
        [Parameter(Mandatory)] [string] $FileName
    )

    $Destination = Join-Path $RuntimeRoot "$Rid/native"
    New-Item -ItemType Directory -Force -Path $Destination | Out-Null
    if (-not (Test-Path $Source -PathType Leaf)) {
        throw "Native build did not produce expected artifact: $Source"
    }

    Copy-Item -LiteralPath $Source -Destination (Join-Path $Destination $FileName) -Force
    Write-Host "Packaged $Rid/$FileName"
}

if ($Target -in @("Windows", "All")) {
    $Architecture = [System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture
    $WindowsArch = switch ($Architecture) {
        "X64" { "x64" }
        "Arm64" { "arm64" }
        default { throw "Windows native builds are supported for x64 and arm64, not $Architecture." }
    }

    Write-Host "--- Building Windows native library ($WindowsArch) ---" -ForegroundColor Cyan
    & cargo build --release --manifest-path $RustManifest
    if ($LASTEXITCODE -ne 0) { throw "cargo build failed for Windows." }

    $WindowsDll = Join-Path $Root "rust/target/release/pdfoxide_bindings.dll"
    Invoke-NativeBuild -Rid "win-$WindowsArch" -Source $WindowsDll -FileName "pdfoxide_bindings.dll"
}

if ($Target -in @("Linux", "All")) {
    Write-Host "--- Building Linux native library in $LinuxImage via wslc ---" -ForegroundColor Cyan
    $Volume = "${Root}:/workspace"
    & wslc run --rm --volume $Volume --workdir /workspace/rust $LinuxImage cargo build --release --target x86_64-unknown-linux-gnu
    if ($LASTEXITCODE -ne 0) { throw "wslc Linux cargo build failed." }

    $LinuxSo = Join-Path $Root "rust/target/x86_64-unknown-linux-gnu/release/libpdfoxide_bindings.so"
    Invoke-NativeBuild -Rid "linux-x64" -Source $LinuxSo -FileName "libpdfoxide_bindings.so"
}

Write-Host "--- Building managed bindings and tests ---" -ForegroundColor Cyan
& dotnet build $BindingProject --configuration Release
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed for the bindings project." }

& dotnet build $TestProject --configuration Release
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed for the test project." }

Write-Host "--- Packing RID-specific NuGet package ---" -ForegroundColor Cyan
& dotnet pack $BindingProject --configuration Release --no-build
if ($LASTEXITCODE -ne 0) { throw "dotnet pack failed." }

Write-Host "--- Build complete ---" -ForegroundColor Green
