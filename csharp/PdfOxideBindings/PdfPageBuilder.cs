using System;

namespace PdfOxide
{
    /// <summary>
    /// Adds content to a single page created by <see cref="DocumentBuilder"/>.
    /// </summary>
    /// <remarks>
    /// Content can only be added to the most recently added page. Add all
    /// content for a page before calling <see cref="DocumentBuilder.AddPage(PdfPageSize)"/>
    /// again.
    /// </remarks>
    public sealed class PdfPageBuilder
    {
        private readonly DocumentBuilder _owner;
        private readonly int _pageIndex;

        internal PdfPageBuilder(DocumentBuilder owner, int pageIndex)
        {
            _owner = owner;
            _pageIndex = pageIndex;
        }

        /// <summary>
        /// Adds text at the current cursor position.
        /// </summary>
        /// <param name="text">The text to add.</param>
        /// <param name="style">The font style, or <see langword="null"/> for the default.</param>
        /// <returns>The current page builder for method chaining.</returns>
        public PdfPageBuilder Text(string text, PdfTextStyle? style = null)
        {
            ArgumentNullException.ThrowIfNull(text);
            _owner.AddText(_pageIndex, text, style);
            return this;
        }

        /// <summary>
        /// Adds text at an absolute position.
        /// </summary>
        /// <param name="x">Horizontal position in points.</param>
        /// <param name="y">Vertical position in points.</param>
        /// <param name="text">The text to add.</param>
        /// <param name="style">The font style, or <see langword="null"/> for the default.</param>
        /// <returns>The current page builder for method chaining.</returns>
        public PdfPageBuilder TextAt(float x, float y, string text, PdfTextStyle? style = null)
        {
            ArgumentNullException.ThrowIfNull(text);
            _owner.AddTextAt(_pageIndex, x, y, text, style);
            return this;
        }

        /// <summary>
        /// Adds a heading at the current cursor position.
        /// </summary>
        /// <param name="level">Heading level from 1 to 6.</param>
        /// <param name="text">The heading text.</param>
        /// <returns>The current page builder for method chaining.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="level"/> is outside 1-6.</exception>
        public PdfPageBuilder Heading(int level, string text)
        {
            ArgumentNullException.ThrowIfNull(text);
            if (level is < 1 or > 6)
            {
                throw new ArgumentOutOfRangeException(nameof(level), level, "Heading level must be between 1 and 6.");
            }

            _owner.AddHeading(_pageIndex, level, text);
            return this;
        }

        /// <summary>
        /// Adds a word-wrapped paragraph at the current cursor position.
        /// </summary>
        /// <param name="text">The paragraph text.</param>
        /// <returns>The current page builder for method chaining.</returns>
        public PdfPageBuilder Paragraph(string text)
        {
            ArgumentNullException.ThrowIfNull(text);
            _owner.AddParagraph(_pageIndex, text);
            return this;
        }

        /// <summary>
        /// Adds a bullet list at the current cursor position.
        /// </summary>
        /// <param name="items">The list items.</param>
        /// <returns>The current page builder for method chaining.</returns>
        public PdfPageBuilder BulletList(params string[] items)
        {
            ArgumentNullException.ThrowIfNull(items);
            ValidateListItems(items);
            _owner.AddList(_pageIndex, items, numbered: false, PdfListStyle.Numeric);
            return this;
        }

        /// <summary>
        /// Adds a numbered list at the current cursor position.
        /// </summary>
        /// <param name="items">The list items.</param>
        /// <param name="style">The numbering style.</param>
        /// <returns>The current page builder for method chaining.</returns>
        public PdfPageBuilder NumberedList(string[] items, PdfListStyle style = PdfListStyle.Numeric)
        {
            ArgumentNullException.ThrowIfNull(items);
            ValidateListItems(items);
            _owner.AddList(_pageIndex, items, numbered: true, style);
            return this;
        }

        /// <summary>
        /// Adds a monospace code block at the current cursor position.
        /// </summary>
        /// <param name="source">The code source, including line breaks.</param>
        /// <param name="language">An optional language label.</param>
        /// <returns>The current page builder for method chaining.</returns>
        public PdfPageBuilder CodeBlock(string source, string language = "")
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(language);
            _owner.AddCodeBlock(_pageIndex, language, source);
            return this;
        }

        /// <summary>
        /// Adds a paragraph composed of styled text runs.
        /// </summary>
        /// <param name="runs">The text runs.</param>
        /// <returns>The current page builder for method chaining.</returns>
        public PdfPageBuilder RichParagraph(params PdfTextRun[] runs)
        {
            ArgumentNullException.ThrowIfNull(runs);
            foreach (PdfTextRun? run in runs)
            {
                if (run is null)
                {
                    throw new ArgumentException("Text runs must not be null.", nameof(runs));
                }
            }

            _owner.AddRichParagraph(_pageIndex, runs);
            return this;
        }

        /// <summary>
        /// Adds vertical space at the current cursor position.
        /// </summary>
        /// <param name="points">The amount of space in points.</param>
        /// <returns>The current page builder for method chaining.</returns>
        public PdfPageBuilder Space(float points)
        {
            if (!float.IsFinite(points) || points < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(points), points, "Space must be a non-negative finite number.");
            }

            _owner.AddSpace(_pageIndex, points);
            return this;
        }

        /// <summary>
        /// Adds a horizontal rule at the current cursor position.
        /// </summary>
        /// <returns>The current page builder for method chaining.</returns>
        public PdfPageBuilder HorizontalRule()
        {
            _owner.AddHorizontalRule(_pageIndex);
            return this;
        }

        /// <summary>
        /// Lays text out across balanced columns.
        /// </summary>
        /// <param name="count">The number of columns.</param>
        /// <param name="gap">The gap between columns in points.</param>
        /// <param name="text">The text to lay out.</param>
        /// <returns>The current page builder for method chaining.</returns>
        public PdfPageBuilder Columns(int count, float gap, string text)
        {
            ArgumentNullException.ThrowIfNull(text);
            if (count <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "Column count must be positive.");
            }
            if (!float.IsFinite(gap) || gap < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(gap), gap, "Column gap must be a non-negative finite number.");
            }

            _owner.AddColumns(_pageIndex, count, gap, text);
            return this;
        }

        /// <summary>
        /// Adds a table at the current cursor position.
        /// </summary>
        /// <param name="table">The table to add.</param>
        /// <returns>The current page builder for method chaining.</returns>
        public PdfPageBuilder Table(PdfTable table)
        {
            ArgumentNullException.ThrowIfNull(table);
            _owner.AddTable(_pageIndex, table);
            return this;
        }

        /// <summary>
        /// Adds an image from a file at a rectangle.
        /// </summary>
        /// <param name="path">The image file path (PNG or JPEG).</param>
        /// <param name="rect">The destination rectangle in points.</param>
        /// <param name="altText">Optional alternative text.</param>
        /// <returns>The current page builder for method chaining.</returns>
        public PdfPageBuilder Image(string path, PdfRect rect, string? altText = null)
        {
            ArgumentException.ThrowIfNullOrEmpty(path);
            ValidateRect(rect);
            _owner.AddImage(_pageIndex, path, rect, altText);
            return this;
        }

        /// <summary>
        /// Adds an image from memory at a rectangle.
        /// </summary>
        /// <param name="imageData">The image data (PNG or JPEG).</param>
        /// <param name="rect">The destination rectangle in points.</param>
        /// <param name="altText">Optional alternative text.</param>
        /// <returns>The current page builder for method chaining.</returns>
        public PdfPageBuilder Image(byte[] imageData, PdfRect rect, string? altText = null)
        {
            ArgumentNullException.ThrowIfNull(imageData);
            if (imageData.Length == 0)
            {
                throw new ArgumentException("Image data must not be empty.", nameof(imageData));
            }

            ValidateRect(rect);
            _owner.AddImage(_pageIndex, imageData, rect, altText);
            return this;
        }

        private static void ValidateListItems(string[] items)
        {
            if (items.Length == 0)
            {
                throw new ArgumentException("List must contain at least one item.", nameof(items));
            }

            foreach (string? item in items)
            {
                if (item is null)
                {
                    throw new ArgumentException("List items must not be null.", nameof(items));
                }
            }
        }

        private static void ValidateRect(PdfRect rect)
        {
            if (!float.IsFinite(rect.X)
                || !float.IsFinite(rect.Y)
                || !float.IsFinite(rect.Width)
                || !float.IsFinite(rect.Height)
                || rect.Width <= 0f
                || rect.Height <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(rect), rect, "Rectangle values must be finite and dimensions must be positive.");
            }
        }
    }
}
