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
}
