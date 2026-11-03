using System;
using System.Collections.Generic;

namespace PdfOxide
{
    /// <summary>
    /// Page dimensions and rotation.
    /// </summary>
    public sealed record PdfPageInfo
    {
        /// <summary>
        /// Gets the page width in points.
        /// </summary>
        public float Width { get; init; }

        /// <summary>
        /// Gets the page height in points.
        /// </summary>
        public float Height { get; init; }

        /// <summary>
        /// Gets the page rotation in degrees.
        /// </summary>
        public int Rotation { get; init; }

        /// <summary>
        /// Gets the MediaBox of the page.
        /// </summary>
        public PdfRect MediaBox { get; init; }

        /// <summary>
        /// Gets the visible (CropBox) area of the page, when available.
        /// </summary>
        public PdfRect? VisibleBox { get; init; }
    }

    /// <summary>
    /// A styled text span extracted from a page.
    /// </summary>
    public sealed record PdfTextSpan
    {
        /// <summary>
        /// Gets the span text.
        /// </summary>
        public string Text { get; init; } = string.Empty;

        /// <summary>
        /// Gets the bounding box in points.
        /// </summary>
        public PdfRect BBox { get; init; }

        /// <summary>
        /// Gets the font name.
        /// </summary>
        public string FontName { get; init; } = string.Empty;

        /// <summary>
        /// Gets the font size in points.
        /// </summary>
        public float FontSize { get; init; }

        /// <summary>
        /// Gets the numeric font weight (100-900).
        /// </summary>
        public int FontWeight { get; init; }

        /// <summary>
        /// Gets a value indicating whether the text is italic.
        /// </summary>
        public bool IsItalic { get; init; }

        /// <summary>
        /// Gets a value indicating whether the font is monospaced.
        /// </summary>
        public bool IsMonospace { get; init; }

        /// <summary>
        /// Gets the text color.
        /// </summary>
        public PdfColor Color { get; init; }

        /// <summary>
        /// Gets the extraction sequence number.
        /// </summary>
        public long Sequence { get; init; }

        /// <summary>
        /// Gets the marked content identifier for tagged PDFs, when present.
        /// </summary>
        public uint? Mcid { get; init; }
    }

    /// <summary>
    /// A single character extracted from a page.
    /// </summary>
    public sealed record PdfTextChar
    {
        /// <summary>
        /// Gets the character text.
        /// </summary>
        public string Text { get; init; } = string.Empty;

        /// <summary>
        /// Gets the bounding box in points.
        /// </summary>
        public PdfRect BBox { get; init; }

        /// <summary>
        /// Gets the font name.
        /// </summary>
        public string FontName { get; init; } = string.Empty;

        /// <summary>
        /// Gets the font size in points.
        /// </summary>
        public float FontSize { get; init; }

        /// <summary>
        /// Gets the numeric font weight (100-900).
        /// </summary>
        public int FontWeight { get; init; }

        /// <summary>
        /// Gets a value indicating whether the text is italic.
        /// </summary>
        public bool IsItalic { get; init; }

        /// <summary>
        /// Gets a value indicating whether the font is monospaced.
        /// </summary>
        public bool IsMonospace { get; init; }

        /// <summary>
        /// Gets the text color.
        /// </summary>
        public PdfColor Color { get; init; }

        /// <summary>
        /// Gets the baseline origin X coordinate.
        /// </summary>
        public float OriginX { get; init; }

        /// <summary>
        /// Gets the baseline origin Y coordinate.
        /// </summary>
        public float OriginY { get; init; }

        /// <summary>
        /// Gets the glyph rotation in degrees.
        /// </summary>
        public float RotationDegrees { get; init; }

        /// <summary>
        /// Gets the marked content identifier for tagged PDFs, when present.
        /// </summary>
        public uint? Mcid { get; init; }
    }

    /// <summary>
    /// A word extracted from a page.
    /// </summary>
    public sealed record PdfWord
    {
        /// <summary>
        /// Gets the word text.
        /// </summary>
        public string Text { get; init; } = string.Empty;

        /// <summary>
        /// Gets the bounding box in points.
        /// </summary>
        public PdfRect BBox { get; init; }

        /// <summary>
        /// Gets the average font size in points.
        /// </summary>
        public float AvgFontSize { get; init; }

        /// <summary>
        /// Gets the dominant font name.
        /// </summary>
        public string DominantFont { get; init; } = string.Empty;

        /// <summary>
        /// Gets a value indicating whether the word is bold.
        /// </summary>
        public bool IsBold { get; init; }

        /// <summary>
        /// Gets a value indicating whether the word is italic.
        /// </summary>
        public bool IsItalic { get; init; }

        /// <summary>
        /// Gets the extraction sequence number.
        /// </summary>
        public long Sequence { get; init; }

        /// <summary>
        /// Gets the word rotation in degrees.
        /// </summary>
        public float RotationDegrees { get; init; }
    }

    /// <summary>
    /// A line of text extracted from a page.
    /// </summary>
    public sealed record PdfTextLine
    {
        /// <summary>
        /// Gets the line text.
        /// </summary>
        public string Text { get; init; } = string.Empty;

        /// <summary>
        /// Gets the bounding box in points.
        /// </summary>
        public PdfRect BBox { get; init; }

        /// <summary>
        /// Gets the number of words in the line.
        /// </summary>
        public int WordCount { get; init; }

        /// <summary>
        /// Gets the words in the line.
        /// </summary>
        public IReadOnlyList<PdfWord> Words { get; init; } = Array.Empty<PdfWord>();
    }

    /// <summary>
    /// A vector path extracted from a page.
    /// </summary>
    public sealed record PdfPath
    {
        /// <summary>
        /// Gets the bounding box in points.
        /// </summary>
        public PdfRect BBox { get; init; }

        /// <summary>
        /// Gets the stroke color, or <see langword="null"/> when the path is not stroked.
        /// </summary>
        public PdfColor? StrokeColor { get; init; }

        /// <summary>
        /// Gets the fill color, or <see langword="null"/> when the path is not filled.
        /// </summary>
        public PdfColor? FillColor { get; init; }

        /// <summary>
        /// Gets the stroke width in points.
        /// </summary>
        public float StrokeWidth { get; init; }

        /// <summary>
        /// Gets the number of path operations.
        /// </summary>
        public int OperationCount { get; init; }

        /// <summary>
        /// Gets the optional transformation matrix.
        /// </summary>
        public IReadOnlyList<float>? Matrix { get; init; }

        /// <summary>
        /// Gets the optional layer name.
        /// </summary>
        public string? Layer { get; init; }
    }

    /// <summary>
    /// A table cell extracted from a page.
    /// </summary>
    public sealed record PdfTableCell
    {
        /// <summary>
        /// Gets the cell text.
        /// </summary>
        public string Text { get; init; } = string.Empty;

        /// <summary>
        /// Gets the number of columns spanned by the cell.
        /// </summary>
        public int Colspan { get; init; }

        /// <summary>
        /// Gets the number of rows spanned by the cell.
        /// </summary>
        public int Rowspan { get; init; }

        /// <summary>
        /// Gets the cell bounding box in points, when available.
        /// </summary>
        public PdfRect? BBox { get; init; }

        /// <summary>
        /// Gets a value indicating whether the cell belongs to a header row.
        /// </summary>
        public bool IsHeader { get; init; }
    }

    /// <summary>
    /// A row of an extracted table.
    /// </summary>
    public sealed record PdfTableRow
    {
        /// <summary>
        /// Gets a value indicating whether the row is a header row.
        /// </summary>
        public bool IsHeader { get; init; }

        /// <summary>
        /// Gets the cells in the row.
        /// </summary>
        public IReadOnlyList<PdfTableCell> Cells { get; init; } = Array.Empty<PdfTableCell>();
    }

    /// <summary>
    /// A table extracted from a page.
    /// </summary>
    public sealed record PdfExtractedTable
    {
        /// <summary>
        /// Gets a value indicating whether the table has an explicit header.
        /// </summary>
        public bool HasHeader { get; init; }

        /// <summary>
        /// Gets the number of columns.
        /// </summary>
        public int ColCount { get; init; }

        /// <summary>
        /// Gets the table bounding box in points, when available.
        /// </summary>
        public PdfRect? BBox { get; init; }

        /// <summary>
        /// Gets the table rows.
        /// </summary>
        public IReadOnlyList<PdfTableRow> Rows { get; init; } = Array.Empty<PdfTableRow>();
    }

    /// <summary>
    /// An image extracted from a page.
    /// </summary>
    public sealed record PdfExtractedImage
    {
        /// <summary>
        /// Gets the image width in pixels.
        /// </summary>
        public uint Width { get; init; }

        /// <summary>
        /// Gets the image height in pixels.
        /// </summary>
        public uint Height { get; init; }

        /// <summary>
        /// Gets the source color space name.
        /// </summary>
        public string ColorSpace { get; init; } = string.Empty;

        /// <summary>
        /// Gets the source bits per color component.
        /// </summary>
        public int BitsPerComponent { get; init; }

        /// <summary>
        /// Gets the image bounding box in points, when available.
        /// </summary>
        public PdfRect? BBox { get; init; }

        /// <summary>
        /// Gets the image rotation in degrees.
        /// </summary>
        public int Rotation { get; init; }

        /// <summary>
        /// Gets the image transformation matrix.
        /// </summary>
        public IReadOnlyList<float> Matrix { get; init; } = Array.Empty<float>();

        /// <summary>
        /// Gets the PNG image data encoded as base64.
        /// </summary>
        public string PngBase64 { get; init; } = string.Empty;

        /// <summary>
        /// Decodes the PNG image data.
        /// </summary>
        /// <returns>The PNG bytes.</returns>
        public byte[] GetPngData()
        {
            return Convert.FromBase64String(PngBase64);
        }
    }

    /// <summary>
    /// An embedded file or font extracted from a document.
    /// </summary>
    public sealed record PdfEmbeddedFile
    {
        /// <summary>
        /// Gets the embedded file name.
        /// </summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>
        /// Gets the embedded data size in bytes.
        /// </summary>
        public long Size { get; init; }

        /// <summary>
        /// Gets the embedded data encoded as base64.
        /// </summary>
        public string DataBase64 { get; init; } = string.Empty;

        /// <summary>
        /// Decodes the embedded data.
        /// </summary>
        /// <returns>The raw bytes.</returns>
        public byte[] GetData()
        {
            return Convert.FromBase64String(DataBase64);
        }
    }

    /// <summary>
    /// A page label entry.
    /// </summary>
    public sealed record PdfPageLabel
    {
        /// <summary>
        /// Gets the zero-based page index.
        /// </summary>
        public int Page { get; init; }

        /// <summary>
        /// Gets the label for the page.
        /// </summary>
        public string Label { get; init; } = string.Empty;
    }

    /// <summary>
    /// A bookmark entry in the document outline.
    /// </summary>
    public sealed record PdfOutlineItem
    {
        /// <summary>
        /// Gets the bookmark title.
        /// </summary>
        public string Title { get; init; } = string.Empty;

        /// <summary>
        /// Gets the target page index, when the destination is a page.
        /// </summary>
        public int? Page { get; init; }

        /// <summary>
        /// Gets the named destination, when the destination is a name.
        /// </summary>
        public string? NamedDestination { get; init; }

        /// <summary>
        /// Gets the child bookmarks.
        /// </summary>
        public IReadOnlyList<PdfOutlineItem> Children { get; init; } = Array.Empty<PdfOutlineItem>();
    }

    /// <summary>
    /// XMP metadata read from a document.
    /// </summary>
    public sealed record PdfXmpMetadata
    {
        /// <summary>
        /// Gets the document title.
        /// </summary>
        public string? Title { get; init; }

        /// <summary>
        /// Gets the document creators.
        /// </summary>
        public IReadOnlyList<string> Creators { get; init; } = Array.Empty<string>();

        /// <summary>
        /// Gets the document description.
        /// </summary>
        public string? Description { get; init; }

        /// <summary>
        /// Gets the document subjects.
        /// </summary>
        public IReadOnlyList<string> Subjects { get; init; } = Array.Empty<string>();

        /// <summary>
        /// Gets the document language.
        /// </summary>
        public string? Language { get; init; }

        /// <summary>
        /// Gets the copyright information.
        /// </summary>
        public string? Rights { get; init; }

        /// <summary>
        /// Gets the document format.
        /// </summary>
        public string? Format { get; init; }

        /// <summary>
        /// Gets the creating tool.
        /// </summary>
        public string? CreatorTool { get; init; }

        /// <summary>
        /// Gets the creation date.
        /// </summary>
        public string? CreateDate { get; init; }

        /// <summary>
        /// Gets the modification date.
        /// </summary>
        public string? ModifyDate { get; init; }

        /// <summary>
        /// Gets the metadata modification date.
        /// </summary>
        public string? MetadataDate { get; init; }

        /// <summary>
        /// Gets the PDF producer.
        /// </summary>
        public string? Producer { get; init; }

        /// <summary>
        /// Gets the PDF keywords.
        /// </summary>
        public string? Keywords { get; init; }

        /// <summary>
        /// Gets the PDF version.
        /// </summary>
        public string? PdfVersion { get; init; }

        /// <summary>
        /// Gets the trapped state.
        /// </summary>
        public string? Trapped { get; init; }

        /// <summary>
        /// Gets the usage terms.
        /// </summary>
        public string? UsageTerms { get; init; }

        /// <summary>
        /// Gets a value indicating whether the document is marked with rights.
        /// </summary>
        public bool? RightsMarked { get; init; }

        /// <summary>
        /// Gets the web statement of rights.
        /// </summary>
        public string? RightsWebStatement { get; init; }

        /// <summary>
        /// Gets unrecognized custom properties.
        /// </summary>
        public IReadOnlyDictionary<string, string> Custom { get; init; } =
            new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>
        /// Gets the raw XMP XML packet, when available.
        /// </summary>
        public string? RawXml { get; init; }
    }

    /// <summary>
    /// Document metadata from the Info dictionary and XMP.
    /// </summary>
    public sealed record PdfDocumentMetadata
    {
        /// <summary>
        /// Gets the document title.
        /// </summary>
        public string? Title { get; init; }

        /// <summary>
        /// Gets the document author.
        /// </summary>
        public string? Author { get; init; }

        /// <summary>
        /// Gets the document subject.
        /// </summary>
        public string? Subject { get; init; }

        /// <summary>
        /// Gets the document keywords.
        /// </summary>
        public string? Keywords { get; init; }

        /// <summary>
        /// Gets the creating application name.
        /// </summary>
        public string? Creator { get; init; }

        /// <summary>
        /// Gets the producing application name.
        /// </summary>
        public string? Producer { get; init; }

        /// <summary>
        /// Gets the XMP metadata, when present.
        /// </summary>
        public PdfXmpMetadata? Xmp { get; init; }
    }

    /// <summary>
    /// An annotation read from a PDF page.
    /// </summary>
    public sealed record PdfAnnotationInfo
    {
        /// <summary>
        /// Gets the PDF annotation object type, typically <c>Annot</c>.
        /// </summary>
        public string Type { get; init; } = string.Empty;

        /// <summary>
        /// Gets the annotation subtype, such as <c>Link</c>, <c>Highlight</c>, or <c>Text</c>.
        /// </summary>
        public string Subtype { get; init; } = string.Empty;

        /// <summary>
        /// Gets the annotation contents, when present.
        /// </summary>
        public string? Contents { get; init; }

        /// <summary>
        /// Gets the annotation author, when present.
        /// </summary>
        public string? Author { get; init; }

        /// <summary>
        /// Gets the annotation rectangle in points.
        /// </summary>
        public PdfRect Rect { get; init; }

        /// <summary>
        /// Gets the annotation color, when present.
        /// </summary>
        public PdfColor? Color { get; init; }

        /// <summary>
        /// Gets a value indicating whether the annotation is hidden.
        /// </summary>
        public bool IsHidden { get; init; }

        /// <summary>
        /// Gets a value indicating whether the annotation is printable.
        /// </summary>
        public bool IsPrintable { get; init; }

        /// <summary>
        /// Gets a value indicating whether the annotation is read-only.
        /// </summary>
        public bool IsReadOnly { get; init; }

        /// <summary>
        /// Gets the URI for link annotations, when present.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Design",
            "CA1056:URI-like properties should not be strings",
            Justification = "Matches the native string-based annotation API.")]
        public string? Uri { get; init; }

        /// <summary>
        /// Gets the icon name for text annotations, when present.
        /// </summary>
        public string? IconName { get; init; }
    }
}
