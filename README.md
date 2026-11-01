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

## Reading PDFs

`PdfDocument` exposes text, layout, metadata, and conversion APIs:

```csharp
var document = new PdfDocument("input.pdf");

string version = document.GetVersion();          // "1.7"
PdfPageInfo page = document.GetPageInfo(0);      // size, rotation, boxes

string text = document.ExtractText(0);           // per-page text
string plain = document.ToPlainText();           // all pages
string html = document.ToHtml(0);                // per-page HTML

File.WriteAllBytes("out.docx", document.ToDocx());
File.WriteAllBytes("out.pptx", document.ToPptx());
File.WriteAllBytes("out.xlsx", document.ToXlsx());

IReadOnlyList<PdfTextSpan> spans = document.ExtractSpans(0);
IReadOnlyList<PdfWord> words = document.ExtractWords(0);
IReadOnlyList<PdfTextLine> lines = document.ExtractTextLines(0);
IReadOnlyList<PdfTextChar> chars = document.ExtractChars(0);
IReadOnlyList<PdfExtractedTable> tables = document.ExtractTables(0);
IReadOnlyList<PdfPath> paths = document.ExtractPaths(0);
IReadOnlyList<PdfExtractedImage> images = document.ExtractImages(0); // GetPngData()
IReadOnlyList<PdfEmbeddedFile> files = document.ExtractEmbeddedFiles();
IReadOnlyList<PdfEmbeddedFile> fonts = document.ExtractEmbeddedFonts();

PdfDocumentMetadata metadata = document.GetMetadata(); // Info + XMP
PdfXmpMetadata? xmp = document.GetXmpMetadata();
IReadOnlyList<PdfOutlineItem> outline = document.GetOutline();
IReadOnlyList<PdfPageLabel> labels = document.GetPageLabels();
```

Structured results are deserialized from JSON returned by the native library. Images and embedded files expose base64 payloads through `GetPngData()` and `GetData()`.

## Dependencies and sources

- [pdf_oxide](https://github.com/yfedoseev/pdf_oxide) — MIT/Apache-2.0
- [csbindgen](https://github.com/Cysharp/csbindgen) — MIT
