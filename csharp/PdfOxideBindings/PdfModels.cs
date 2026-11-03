using System;
using System.Collections.Generic;

namespace PdfOxide
{
    /// <summary>
    /// Page sizes supported by <see cref="DocumentBuilder"/>.
    /// </summary>
    public enum PdfPageSize
    {
        /// <summary>A4 (210mm x 297mm).</summary>
        A4 = 0,
        /// <summary>US Letter (8.5" x 11").</summary>
        Letter = 1,
        /// <summary>US Legal (8.5" x 14").</summary>
        Legal = 2,
        /// <summary>A3 (297mm x 420mm).</summary>
        A3 = 3,
        /// <summary>Custom page dimensions, supplied in points.</summary>
        Custom = 4,
    }

    /// <summary>
    /// Marker styles for numbered lists.
    /// </summary>
    public enum PdfListStyle
    {
        /// <summary>1. 2. 3.</summary>
        Numeric = 0,
        /// <summary>i. ii. iii.</summary>
        RomanLower = 1,
        /// <summary>a. b. c.</summary>
        AlphaLower = 2,
    }

    /// <summary>
    /// Supported 1-D barcode symbologies for <see cref="PdfPageBuilder.Barcode"/>.
    /// </summary>
    public enum PdfBarcodeType
    {
        /// <summary>Code 128.</summary>
        Code128 = 0,
        /// <summary>Code 39.</summary>
        Code39 = 1,
        /// <summary>EAN-13.</summary>
        Ean13 = 2,
        /// <summary>EAN-8.</summary>
        Ean8 = 3,
        /// <summary>UPC-A.</summary>
        UpcA = 4,
        /// <summary>Interleaved 2 of 5.</summary>
        Itf = 5,
        /// <summary>Code 93.</summary>
        Code93 = 6,
        /// <summary>Codabar.</summary>
        Codabar = 7,
    }

    /// <summary>
    /// One option in a radio-button group.
    /// </summary>
    /// <param name="ExportValue">The value submitted when this option is selected.</param>
    /// <param name="Rect">The clickable rectangle for the radio button.</param>
    public sealed record PdfRadioOption(string ExportValue, PdfRect Rect);

    /// <summary>
    /// Styles for runs inside a rich paragraph.
    /// </summary>
    public enum PdfTextRunStyle
    {
        /// <summary>Normal weight using the current text color.</summary>
        Normal = 0,
        /// <summary>Bold text.</summary>
        Bold = 1,
        /// <summary>Italic text.</summary>
        Italic = 2,
        /// <summary>Normal weight with an explicit color.</summary>
        Color = 3,
    }

    /// <summary>
    /// Built-in table styles.
    /// </summary>
    public enum PdfTableStyle
    {
        /// <summary>The default table style.</summary>
        Default = 0,
        /// <summary>Full cell borders.</summary>
        Bordered = 1,
        /// <summary>Minimal borders.</summary>
        Minimal = 2,
    }

    /// <summary>
    /// Text markup annotation types.
    /// </summary>
    public enum PdfTextMarkupType
    {
        /// <summary>Highlight the text.</summary>
        Highlight = 0,
        /// <summary>Underline the text.</summary>
        Underline = 1,
        /// <summary>Strike through the text.</summary>
        StrikeOut = 2,
        /// <summary>Add a squiggly underline.</summary>
        Squiggly = 3,
    }

    /// <summary>
    /// Icons used for sticky note annotations.
    /// </summary>
    public enum PdfTextAnnotationIcon
    {
        /// <summary>Comment icon.</summary>
        Comment = 0,
        /// <summary>Key icon.</summary>
        Key = 1,
        /// <summary>Note icon.</summary>
        Note = 2,
        /// <summary>Help icon.</summary>
        Help = 3,
        /// <summary>New paragraph icon.</summary>
        NewParagraph = 4,
        /// <summary>Paragraph icon.</summary>
        Paragraph = 5,
        /// <summary>Insert icon.</summary>
        Insert = 6,
    }

    /// <summary>
    /// Standard rubber stamp annotation types.
    /// </summary>
    public enum PdfStampType
    {
        /// <summary>Approved stamp.</summary>
        Approved,
        /// <summary>Experimental stamp.</summary>
        Experimental,
        /// <summary>Not approved stamp.</summary>
        NotApproved,
        /// <summary>As-is stamp.</summary>
        AsIs,
        /// <summary>Expired stamp.</summary>
        Expired,
        /// <summary>Not for public release stamp.</summary>
        NotForPublicRelease,
        /// <summary>Confidential stamp.</summary>
        Confidential,
        /// <summary>Final stamp.</summary>
        Final,
        /// <summary>Sold stamp.</summary>
        Sold,
        /// <summary>Departmental stamp.</summary>
        Departmental,
        /// <summary>For comment stamp.</summary>
        ForComment,
        /// <summary>Top secret stamp.</summary>
        TopSecret,
        /// <summary>Draft stamp.</summary>
        Draft,
        /// <summary>For public release stamp.</summary>
        ForPublicRelease,
        /// <summary>Custom stamp name.</summary>
        Custom,
    }

    internal static class PdfStampTypeExtensions
    {
        internal static string ToPdfName(this PdfStampType type)
        {
            return type switch
            {
                PdfStampType.Approved => "Approved",
                PdfStampType.Experimental => "Experimental",
                PdfStampType.NotApproved => "NotApproved",
                PdfStampType.AsIs => "AsIs",
                PdfStampType.Expired => "Expired",
                PdfStampType.NotForPublicRelease => "NotForPublicRelease",
                PdfStampType.Confidential => "Confidential",
                PdfStampType.Final => "Final",
                PdfStampType.Sold => "Sold",
                PdfStampType.Departmental => "Departmental",
                PdfStampType.ForComment => "ForComment",
                PdfStampType.TopSecret => "TopSecret",
                PdfStampType.Draft => "Draft",
                PdfStampType.ForPublicRelease => "ForPublicRelease",
                _ => throw new ArgumentOutOfRangeException(
                    nameof(type), type, "Custom stamps require an explicit name."),
            };
        }
    }

    /// <summary>
    /// Options for a custom watermark annotation.
    /// </summary>
    public sealed record PdfWatermarkOptions
    {
        /// <summary>
        /// Gets the watermark text.
        /// </summary>
        public required string Text { get; init; }

        /// <summary>
        /// Gets the watermark opacity from 0 to 1. Defaults to 0.3.
        /// </summary>
        public float Opacity { get; init; } = 0.3f;

        /// <summary>
        /// Gets the rotation in degrees. Defaults to 45.
        /// </summary>
        public float Rotation { get; init; } = 45f;

        /// <summary>
        /// Gets the font name. Defaults to Helvetica.
        /// </summary>
        public string Font { get; init; } = "Helvetica";

        /// <summary>
        /// Gets the font size in points. Defaults to 72.
        /// </summary>
        public float Size { get; init; } = 72f;

        /// <summary>
        /// Gets the watermark color. Defaults to medium gray.
        /// </summary>
        public PdfColor Color { get; init; } = new(0.5f, 0.5f, 0.5f);

        /// <summary>
        /// Gets the optional bounding rectangle in points. When omitted, the
        /// writer uses a default rectangle sized to the page.
        /// </summary>
        public PdfRect? Rect { get; init; }
    }

    /// <summary>
    /// An RGB color with components in the range 0 to 1.
    /// </summary>
    /// <param name="R">Red component.</param>
    /// <param name="G">Green component.</param>
    /// <param name="B">Blue component.</param>
    public readonly record struct PdfColor(float R, float G, float B)
    {
        /// <summary>
        /// Creates a color from 8-bit RGB components.
        /// </summary>
        /// <param name="r">Red component (0-255).</param>
        /// <param name="g">Green component (0-255).</param>
        /// <param name="b">Blue component (0-255).</param>
        /// <returns>The normalized color.</returns>
        public static PdfColor FromRgb(byte r, byte g, byte b)
        {
            return new PdfColor(r / 255f, g / 255f, b / 255f);
        }
    }

    /// <summary>
    /// A rectangle in PDF points, with the origin at the bottom-left corner.
    /// </summary>
    /// <param name="X">Left coordinate.</param>
    /// <param name="Y">Bottom coordinate.</param>
    /// <param name="Width">Width in points.</param>
    /// <param name="Height">Height in points.</param>
    public readonly record struct PdfRect(float X, float Y, float Width, float Height);

    /// <summary>
    /// A point in PDF coordinate space, in points.
    /// </summary>
    /// <param name="X">Horizontal coordinate.</param>
    /// <param name="Y">Vertical coordinate.</param>
    public readonly record struct PdfPoint(float X, float Y);

    /// <summary>
    /// Stroke settings for lines, shape outlines, and paths.
    /// </summary>
    public sealed record PdfLineStyle
    {
        /// <summary>
        /// Gets the stroke width in points. Defaults to 1.
        /// </summary>
        public float Width { get; init; } = 1f;

        /// <summary>
        /// Gets the stroke color. Defaults to black.
        /// </summary>
        public PdfColor Color { get; init; } = new PdfColor(0f, 0f, 0f);

        /// <summary>
        /// Gets the optional dash pattern. Each entry is an on/off length in
        /// points. <see langword="null"/> or an empty array draws a solid line.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Performance",
            "CA1819:Properties should not return arrays",
            Justification = "A dash pattern is a small value-like option bag; copying on every get would be surprising.")]
        public float[]? DashPattern { get; init; }

        /// <summary>
        /// Gets the starting offset into the dash pattern, in points.
        /// </summary>
        public float DashPhase { get; init; }
    }

    /// <summary>
    /// Stroke and fill settings for a custom graphics path.
    /// </summary>
    public sealed record PdfPathStyle
    {
        /// <summary>
        /// Gets the stroke color. Defaults to black; set to <see langword="null"/>
        /// to disable stroking.
        /// </summary>
        public PdfColor? StrokeColor { get; init; } = new PdfColor(0f, 0f, 0f);

        /// <summary>
        /// Gets the stroke width in points. Defaults to 1.
        /// </summary>
        public float StrokeWidth { get; init; } = 1f;

        /// <summary>
        /// Gets the fill color, or <see langword="null"/> to disable filling.
        /// </summary>
        public PdfColor? FillColor { get; init; }
    }

    /// <summary>
    /// Builds a custom vector graphics path from a sequence of drawing
    /// operations. Coordinates are in PDF points.
    /// </summary>
    public sealed class PdfGraphicsPath
    {
        private readonly List<int> _kinds = [];
        private readonly List<float> _coordinates = [];

        /// <summary>
        /// Starts a new subpath at the specified point.
        /// </summary>
        /// <param name="x">Horizontal coordinate.</param>
        /// <param name="y">Vertical coordinate.</param>
        /// <returns>The current path for method chaining.</returns>
        public PdfGraphicsPath MoveTo(float x, float y)
        {
            Add(0, Finite(x, nameof(x)), Finite(y, nameof(y)));
            return this;
        }

        /// <summary>
        /// Adds a straight line to the specified point.
        /// </summary>
        /// <param name="x">Horizontal coordinate.</param>
        /// <param name="y">Vertical coordinate.</param>
        /// <returns>The current path for method chaining.</returns>
        public PdfGraphicsPath LineTo(float x, float y)
        {
            Add(1, Finite(x, nameof(x)), Finite(y, nameof(y)));
            return this;
        }

        /// <summary>
        /// Adds a cubic Bezier curve to the specified endpoint.
        /// </summary>
        /// <param name="c1x">First control point horizontal coordinate.</param>
        /// <param name="c1y">First control point vertical coordinate.</param>
        /// <param name="c2x">Second control point horizontal coordinate.</param>
        /// <param name="c2y">Second control point vertical coordinate.</param>
        /// <param name="x">End point horizontal coordinate.</param>
        /// <param name="y">End point vertical coordinate.</param>
        /// <returns>The current path for method chaining.</returns>
        public PdfGraphicsPath CurveTo(float c1x, float c1y, float c2x, float c2y, float x, float y)
        {
            Add(2,
                Finite(c1x, nameof(c1x)),
                Finite(c1y, nameof(c1y)),
                Finite(c2x, nameof(c2x)),
                Finite(c2y, nameof(c2y)),
                Finite(x, nameof(x)),
                Finite(y, nameof(y)));
            return this;
        }

        /// <summary>
        /// Adds a rectangle subpath.
        /// </summary>
        /// <param name="x">Left coordinate.</param>
        /// <param name="y">Bottom coordinate.</param>
        /// <param name="width">Width in points.</param>
        /// <param name="height">Height in points.</param>
        /// <returns>The current path for method chaining.</returns>
        public PdfGraphicsPath Rectangle(float x, float y, float width, float height)
        {
            Add(3,
                Finite(x, nameof(x)),
                Finite(y, nameof(y)),
                Finite(width, nameof(width)),
                Finite(height, nameof(height)));
            return this;
        }

        /// <summary>
        /// Closes the current subpath.
        /// </summary>
        /// <returns>The current path for method chaining.</returns>
        public PdfGraphicsPath Close()
        {
            _kinds.Add(4);
            return this;
        }

        internal int[] GetKinds()
        {
            return _kinds.ToArray();
        }

        internal float[] GetCoordinates()
        {
            return _coordinates.ToArray();
        }

        private void Add(int kind, params float[] coordinates)
        {
            _kinds.Add(kind);
            _coordinates.AddRange(coordinates);
        }

        private static float Finite(float value, string parameterName)
        {
            if (!float.IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(
                    parameterName, value, "Path coordinates must be finite numbers.");
            }

            return value;
        }
    }

    /// <summary>
    /// Font settings for text added with <see cref="PdfPageBuilder.Text(string, PdfTextStyle?)"/>.
    /// </summary>
    public sealed record PdfTextStyle
    {
        /// <summary>
        /// Gets the font name. Defaults to Helvetica.
        /// </summary>
        public string Font { get; init; } = "Helvetica";

        /// <summary>
        /// Gets the font size in points. Defaults to 12.
        /// </summary>
        public float Size { get; init; } = 12f;

        /// <summary>
        /// Gets the default text style.
        /// </summary>
        public static PdfTextStyle Default { get; } = new PdfTextStyle();
    }

    /// <summary>
    /// A styled run inside a rich paragraph.
    /// </summary>
    public sealed record PdfTextRun
    {
        /// <summary>
        /// Initializes a new text run.
        /// </summary>
        /// <param name="text">The run text.</param>
        public PdfTextRun(string text)
        {
            Text = text ?? throw new ArgumentNullException(nameof(text));
        }

        /// <summary>
        /// Gets the run text.
        /// </summary>
        public string Text { get; }

        /// <summary>
        /// Gets the run style.
        /// </summary>
        public PdfTextRunStyle Style { get; init; }

        /// <summary>
        /// Gets the run color. Only used when <see cref="Style"/> is <see cref="PdfTextRunStyle.Color"/>.
        /// </summary>
        public PdfColor Color { get; init; }

        /// <summary>
        /// Creates a normal run.
        /// </summary>
        /// <param name="text">The run text.</param>
        /// <returns>The new run.</returns>
        public static PdfTextRun Normal(string text) => new PdfTextRun(text);

        /// <summary>
        /// Creates a bold run.
        /// </summary>
        /// <param name="text">The run text.</param>
        /// <returns>The new run.</returns>
        public static PdfTextRun Bold(string text) => new PdfTextRun(text) { Style = PdfTextRunStyle.Bold };

        /// <summary>
        /// Creates an italic run.
        /// </summary>
        /// <param name="text">The run text.</param>
        /// <returns>The new run.</returns>
        public static PdfTextRun Italic(string text) => new PdfTextRun(text) { Style = PdfTextRunStyle.Italic };

        /// <summary>
        /// Creates a colored run.
        /// </summary>
        /// <param name="text">The run text.</param>
        /// <param name="color">The text color.</param>
        /// <returns>The new run.</returns>
        public static PdfTextRun Colored(string text, PdfColor color) =>
            new PdfTextRun(text) { Style = PdfTextRunStyle.Color, Color = color };
    }

    /// <summary>
    /// Metadata written to the PDF document information dictionary.
    /// </summary>
    public sealed record PdfMetadata
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
    }

    /// <summary>
    /// A rectangular table of plain-text cells.
    /// </summary>
    public sealed class PdfTable
    {
        private readonly string[][] _rows;

        /// <summary>
        /// Initializes a new table from rectangular rows of cells.
        /// </summary>
        /// <param name="rows">The table rows. Every row must have the same number of non-null cells.</param>
        public PdfTable(string[][] rows)
        {
            ArgumentNullException.ThrowIfNull(rows);
            if (rows.Length == 0)
            {
                throw new ArgumentException("Table must contain at least one row.", nameof(rows));
            }

            int columnCount = rows[0]?.Length ?? 0;
            if (columnCount == 0)
            {
                throw new ArgumentException("Table rows must contain at least one cell.", nameof(rows));
            }

            _rows = new string[rows.Length][];
            for (int rowIndex = 0; rowIndex < rows.Length; rowIndex++)
            {
                string[]? row = rows[rowIndex];
                if (row is null)
                {
                    throw new ArgumentException("Table rows must not be null.", nameof(rows));
                }
                if (row.Length != columnCount)
                {
                    throw new ArgumentException("All table rows must have the same number of cells.", nameof(rows));
                }

                _rows[rowIndex] = new string[row.Length];
                for (int columnIndex = 0; columnIndex < row.Length; columnIndex++)
                {
                    _rows[rowIndex][columnIndex] =
                        row[columnIndex] ?? throw new ArgumentException("Table cells must not be null.", nameof(rows));
                }
            }

            var rowViews = new IReadOnlyList<string>[_rows.Length];
            for (int index = 0; index < _rows.Length; index++)
            {
                rowViews[index] = Array.AsReadOnly(_rows[index]);
            }

            Rows = Array.AsReadOnly(rowViews);
        }

        /// <summary>
        /// Gets the table rows.
        /// </summary>
        public IReadOnlyList<IReadOnlyList<string>> Rows { get; }

        /// <summary>
        /// Gets or initializes a value indicating whether the first row is rendered as a header.
        /// </summary>
        public bool HasHeaderRow { get; init; }

        /// <summary>
        /// Gets or initializes the table style.
        /// </summary>
        public PdfTableStyle Style { get; init; }

        internal string[] GetCells()
        {
            int columnCount = _rows[0].Length;
            var cells = new string[_rows.Length * columnCount];
            int index = 0;
            foreach (string[] row in _rows)
            {
                foreach (string cell in row)
                {
                    cells[index++] = cell;
                }
            }
            return cells;
        }

        internal int RowCount => _rows.Length;

        internal int ColumnCount => _rows[0].Length;
    }

    /// <summary>
    /// Horizontal alignment for a streaming-table cell.
    /// </summary>
    public enum PdfCellAlignment
    {
        /// <summary>Align cell content to the left.</summary>
        Left = 0,
        /// <summary>Center cell content.</summary>
        Center = 1,
        /// <summary>Align cell content to the right.</summary>
        Right = 2,
    }

    /// <summary>
    /// One column in a streaming table.
    /// </summary>
    public sealed record PdfStreamingColumn
    {
        /// <summary>
        /// Gets the column heading. Rendered at the top of the table and on
        /// every page when the header is repeated.
        /// </summary>
        public required string Header { get; init; }

        /// <summary>
        /// Gets the column width in points. Defaults to 100.
        /// </summary>
        public float Width { get; init; } = 100f;

        /// <summary>
        /// Gets the default horizontal alignment for cells in this column.
        /// </summary>
        public PdfCellAlignment Alignment { get; init; }
    }

    /// <summary>
    /// A cell in a streaming-table row, optionally spanning multiple rows.
    /// </summary>
    /// <param name="Text">The cell text.</param>
    /// <param name="RowSpan">The number of rows this cell spans. Defaults to 1.</param>
    public sealed record PdfStreamingCell(string Text, int RowSpan = 1);

    /// <summary>
    /// Options for a streaming table. The table paginates automatically and
    /// can repeat its header across pages.
    /// </summary>
    public sealed record PdfStreamingTableOptions
    {
        /// <summary>
        /// Gets the table columns.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Performance",
            "CA1819:Properties should not return arrays",
            Justification = "A column list is a small value-like option bag.")]
        public required PdfStreamingColumn[] Columns { get; init; }

        /// <summary>
        /// Gets a value indicating whether the header row is repeated at the
        /// top of every page the table spans. Defaults to <see langword="true"/>.
        /// </summary>
        public bool RepeatHeader { get; init; } = true;

        /// <summary>
        /// Gets the header background fill, or <see langword="null"/> for no fill.
        /// Defaults to a light gray.
        /// </summary>
        public PdfColor? HeaderFill { get; init; } = new(0.93f, 0.93f, 0.93f);

        /// <summary>
        /// Gets the grid line color, or <see langword="null"/> to suppress
        /// grid lines. Defaults to light gray.
        /// </summary>
        public PdfColor? GridColor { get; init; } = new(0.8f, 0.8f, 0.8f);

        /// <summary>
        /// Gets the grid line width in points. Defaults to 0.5.
        /// </summary>
        public float GridWidth { get; init; } = 0.5f;

        /// <summary>
        /// Gets the horizontal cell padding in points. Defaults to 4.
        /// </summary>
        public float CellPaddingHorizontal { get; init; } = 4f;

        /// <summary>
        /// Gets the top cell padding in points. Defaults to 2.
        /// </summary>
        public float CellPaddingTop { get; init; } = 2f;

        /// <summary>
        /// Gets the bottom cell padding in points. Defaults to 2.
        /// </summary>
        public float CellPaddingBottom { get; init; } = 2f;

        /// <summary>
        /// Gets the maximum rowspan allowed for cells. Defaults to 1 (disabled).
        /// </summary>
        public int MaxRowspan { get; init; } = 1;
    }
}
