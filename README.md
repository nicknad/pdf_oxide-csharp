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

The bindings expose whole-document conversion from Markdown, text, and images:

```csharp
PdfConverter.ConvertMarkdown("# Hello\n\nWorld", "hello.pdf");
PdfConverter.ConvertText("Plain text", "text.pdf");
PdfConverter.ConvertImages(["page1.png", "page2.jpg"], "images.pdf");
```

`DocumentBuilder` provides multi-element pages with selectable page sizes, metadata, headings, paragraphs, rich text, lists, code blocks, tables, images, and rules:

```csharp
using var builder = new DocumentBuilder();

builder.SetMetadata(new PdfMetadata { Title = "Report" });

builder.AddPage(PdfPageSize.A4)
    .Heading(1, "Report")
    .Paragraph("Wrapped body text.")
    .BulletList("First", "Second")
    .RichParagraph(PdfTextRun.Bold("Bold"), PdfTextRun.Normal(" normal"))
    .Table(new PdfTable([["Name", "Value"], ["Alice", "42"]]) { HasHeaderRow = true })
    .Image("logo.png", new PdfRect(72, 200, 64, 64))
    .HorizontalRule();

builder.AddPage(400, 300).Paragraph("Custom-sized page");
builder.Save("report.pdf");

byte[] bytes = builder.ToBytes();
```

Native failures throw `PdfOxideException`. Content is buffered and rendered on `Save`/`ToBytes`, so add all content to a page before adding the next one.

## Dependencies and sources

- [pdf_oxide](https://github.com/yfedoseev/pdf_oxide) — MIT/Apache-2.0
- [csbindgen](https://github.com/Cysharp/csbindgen) — MIT
