# PdfOxide.Bindings

Cross-platform .NET bindings for PDF Oxide. The managed API calls a Rust `cdylib`; NuGet selects the native binary for the consuming app's runtime identifier (RID).

## Supported native targets

The build currently produces:

| RID | Native asset |
| --- | --- |
| `win-x64` | `pdfoxide_bindings.dll` |
| `linux-x64` | `libpdfoxide_bindings.so` |

The C# API is the same on both platforms. The native libraries are separate builds and are packaged under `runtimes/<rid>/native/` in the NuGet package.

## Build prerequisites

- Rust stable and the .NET 10 SDK on Windows.
- `wslc` with the `rust:1-bookworm` image available for the Debian Linux build. The build script runs the Linux build inside that container and mounts this checkout at `/workspace`.

## Build and pack

From PowerShell at the repository root:

```powershell
./build/build.ps1              # Windows + Linux native assets, managed build, and NuGet pack
./build/build.ps1 -Target Windows
./build/build.ps1 -Target Linux
```

The package is written to `csharp/PdfOxideBindings/bin/Release/`. The Linux target can also be built with another compatible Rust container image by passing `-LinuxImage`.

## Creating PDFs

The bindings expose conversion from Markdown, text, and images, plus a `DocumentBuilder` for adding text pages. The current builder defaults to A4 pages.

## Dependencies and sources

- [pdf_oxide](https://github.com/yfedoseev/pdf_oxide) — MIT/Apache-2.0
- [csbindgen](https://github.com/Cysharp/csbindgen) — MIT
