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

Whole-document conversion is available through `PdfConverter`:

```csharp
PdfConverter.ConvertMarkdown("# Hello\n\nWorld", "hello.pdf");
PdfConverter.ConvertText("Plain text", "text.pdf");
PdfConverter.ConvertImages(["page1.png", "page2.jpg"], "images.pdf");

PdfConverter.ConvertHtml("<h1>Hello</h1><p>World</p>", "hello-html.pdf");
PdfConverter.ConvertHtml(html, "h1 { color: #b00; }", "styled.pdf");
PdfConverter.ConvertImage("scan.png", "scan.pdf");
PdfConverter.Merge(["a.pdf", "b.pdf"], "merged.pdf");

byte[] htmlPdf = PdfConverter.ConvertHtmlToBytes(html, css);
byte[] mergedPdf = PdfConverter.MergeToBytes(["a.pdf", "b.pdf"]);
```

`DocumentBuilder` provides multi-element pages with selectable page sizes, metadata, headings, paragraphs, rich text, lists, code blocks, tables, images, rules, links, annotations, form fields, barcodes, and vector graphics. Content is buffered per page and rendered on `Save`/`ToBytes`. Pages can receive content in any order after they have been added.

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

var detail = builder.AddPage(400, 300);
detail.Paragraph("Custom-sized page");

// Any previously added page can still receive content.
builder.AddPage(PdfPageSize.A4)
    .Paragraph("First page")
    .LinkUrl("https://example.com")
    .Highlight(PdfColor.FromRgb(255, 235, 59))
    .Footnote("1", "Footnote body rendered at the page bottom.");

builder.Save("report.pdf");
byte[] bytes = builder.ToBytes();
```

### Links, annotations, watermarks, and stamps

```csharp
page.LinkUrl("https://example.com")
    .LinkPage(2)
    .LinkRect(new PdfRect(72, 500, 120, 18), "https://example.com/rect")
    .Highlight(PdfColor.FromRgb(255, 255, 0))
    .Underline(PdfColor.FromRgb(0, 0, 255))
    .StickyNote("Review this section", PdfTextAnnotationIcon.Note)
    .FreeText(new PdfRect(72, 420, 220, 48), "Free text note")
    .Watermark("DRAFT")
    .Stamp(PdfStampType.Draft)
    .Footnote("2", "Another note.");
```

Read annotations from an existing PDF with `PdfDocument.GetAnnotations(pageIndex)`.

### Form fields and barcodes

```csharp
page.TextField("name", new PdfRect(72, 700, 200, 20), "Jane Doe")
    .Checkbox("agree", new PdfRect(72, 670, 16, 16), isChecked: true)
    .ComboBox("country", new PdfRect(72, 640, 140, 20), ["US", "CA"], "US")
    .RadioGroup("choice", [
        new PdfRadioOption("one", new PdfRect(72, 600, 16, 16)),
        new PdfRadioOption("two", new PdfRect(96, 600, 16, 16)),
    ], "one")
    .SignatureField("signature", new PdfRect(72, 540, 200, 48))
    .QrCode("https://example.com", new PdfRect(72, 460, 96, 96))
    .Barcode(PdfBarcodeType.Code128, "ABC-123", new PdfRect(72, 400, 180, 48));
```

### Vector graphics

```csharp
var style = new PdfLineStyle { Width = 1.5f, Color = PdfColor.FromRgb(0, 0, 0) };

page.Rectangle(new PdfRect(72, 300, 120, 60))
    .FilledRectangle(new PdfRect(210, 300, 120, 60), PdfColor.FromRgb(220, 220, 255))
    .Line(72, 280, 330, 280)
    .StrokeRectangle(new PdfRect(72, 200, 120, 60), style)
    .Circle(260, 230, 30, stroke: style, fill: PdfColor.FromRgb(255, 200, 200))
    .Ellipse(350, 230, 45, 25, stroke: style)
    .Polygon([new PdfPoint(72, 120), new PdfPoint(140, 170), new PdfPoint(200, 120)], stroke: style);

var path = new PdfGraphicsPath()
    .MoveTo(72, 80)
    .LineTo(140, 110)
    .CurveTo(170, 130, 200, 130, 230, 80)
    .Close();
page.Path(path, new PdfPathStyle { StrokeWidth = 2f });
```

### Fonts and tagged PDF

```csharp
builder.RegisterBundledFonts();                 // DejaVu Sans regular + bold
builder.RegisterFont("Brand", "BrandSans.ttf");
builder.RegisterFont("BrandBytes", fontBytes);

builder.SetTaggedPdfUa1()
    .SetLanguage("en-US")
    .AddRoleMap("MyHeading", "H1");

builder.AddPage().Text("Unicode: héllo Ω", new PdfTextStyle { Font = "DejaVuSans", Size = 14 });
```

### Streaming tables and pages

```csharp
var table = page.StreamingTable(new PdfStreamingTableOptions
{
    Columns =
    [
        new PdfStreamingColumn { Header = "ID", Width = 60 },
        new PdfStreamingColumn { Header = "Name", Width = 220 },
        new PdfStreamingColumn { Header = "Value", Width = 100, Alignment = PdfCellAlignment.Right },
    ],
    RepeatHeader = true,
});

for (int i = 0; i < 500; i++)
{
    table.AddRow($"row-{i:000}", $"name-{i}", $"{i * 3}");
}

table.Finish();
```

`PdfStreamingPageBuilder` provides page-break conveniences; `PdfPageBuilder.NewPageSameSize()` starts another page with the same dimensions.

## Reading PDFs

`PdfDocument` exposes text, layout, metadata, annotations, and conversion APIs:

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
IReadOnlyList<PdfAnnotationInfo> annotations = document.GetAnnotations(0);

PdfDocumentMetadata metadata = document.GetMetadata(); // Info + XMP
PdfXmpMetadata? xmp = document.GetXmpMetadata();
IReadOnlyList<PdfOutlineItem> outline = document.GetOutline();
IReadOnlyList<PdfPageLabel> labels = document.GetPageLabels();
```

Structured results are deserialized from JSON returned by the native library. Images and embedded files expose base64 payloads through `GetPngData()` and `GetData()`.

## Editing PDFs

`PdfEditor` modifies existing documents and saves either to a file or to bytes:

```csharp
using var editor = PdfEditor.Open("input.pdf");

editor.DeletePage(3);
editor.MovePage(0, 2);
editor.RotatePage(0, 90);
editor.RotateAllPages(180);
editor.SetCropBox(0, new PdfRect(18, 18, 559, 756));
editor.CropMargins(12, 12, 12, 12);

byte[] extracted = editor.ExtractPages(0, 2, 4);
editor.SaveExtracted("selected.pdf", 0, 2, 4);
editor.Split("split-output", "page");

editor.MergeFrom("appendix.pdf");
editor.Save("edited.pdf");

foreach (PdfFormFieldInfo field in editor.GetFormFields())
{
    Console.WriteLine($"{field.Name} ({field.Type}) = {field.Value}");
}

editor.SetFormFieldValue("name", "Jane Doe");
editor.FlattenForms();
editor.FlattenAnnotations();
editor.Save("flattened.pdf");
```

`PdfEditor.FromBytes` works the same way. Form-field enumeration requires a file-backed editor (use `Open`, not `FromBytes`).

### Encryption and redaction

```csharp
using var editor = PdfEditor.Open("input.pdf");
editor.SaveEncrypted("protected.pdf", userPassword: "user", ownerPassword: "owner");

using var protectedDoc = PdfEditor.Open("protected.pdf", "user");
protectedDoc.Save("decrypted.pdf");

PdfConverter.Encrypt("input.pdf", "protected.pdf", "user", "owner");
PdfConverter.Decrypt("protected.pdf", "decrypted.pdf", "user");

editor.AddRedaction(0, new PdfRect(72, 400, 220, 40));
editor.ApplyRedactions();
editor.ScrubMetadata();
editor.Save("redacted.pdf");
```

`PdfDocument` reads unencrypted files. To read a password-protected file with the extraction APIs, decrypt it first with `PdfConverter.Decrypt` or `PdfEditor.Open(path, password).Save(...)`.

## Digital signatures and timestamps

```csharp
using var certificate = PdfSigningCertificate.FromPkcs12(pfxBytes, "pfx-password");
// or: PdfSigningCertificate.FromPem(certificatePem, privateKeyPem);

byte[] signed = PdfSigning.Sign(pdfBytes, certificate, new PdfSigningOptions
{
    Reason = "Approval",
    Location = "Berlin",
});

byte[] pades = PdfSigning.SignPades(pdfBytes, certificate, tsaUrl: "https://tsa.example.com");

int count = PdfSigning.GetSignatureCount(signed);
IReadOnlyList<PdfSignatureInfo> signatures = PdfSigning.GetSignatures(signed);
bool valid = PdfSigning.Verify(signed);

using var tsa = new PdfTimestampClient("https://tsa.example.com");
byte[] token = tsa.RequestTimestamp(data);
```

Timestamp requests require network access to the configured RFC 3161 authority. PAdES signing with a `tsaUrl` embeds the timestamp during signing.

## Dependencies and sources

- [pdf_oxide](https://github.com/yfedoseev/pdf_oxide) — MIT/Apache-2.0
- [csbindgen](https://github.com/Cysharp/csbindgen) — MIT
