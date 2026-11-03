using System;

namespace PdfOxide
{
    /// <summary>
    /// Adds content to a single page created by <see cref="DocumentBuilder"/>.
    /// </summary>
    /// <remarks>
    /// Content can be added to any page that has already been added, and pages
    /// do not need to be completed before adding another page. Content may be
    /// added in any order before saving the document.
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

        /// <summary>
        /// Adds a URL link annotation over the most recent text.
        /// </summary>
        /// <param name="url">The URL to open when the link is clicked.</param>
        /// <returns>The current page builder for method chaining.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Design",
            "CA1054:URI-like parameters should not be strings",
            Justification = "Matches the native string-based annotation API.")]
        public PdfPageBuilder LinkUrl(string url)
        {
            ArgumentException.ThrowIfNullOrEmpty(url);
            _owner.AddLinkUrl(_pageIndex, url);
            return this;
        }

        /// <summary>
        /// Adds an internal link annotation over the most recent text.
        /// </summary>
        /// <param name="pageIndex">The zero-based target page index.</param>
        /// <returns>The current page builder for method chaining.</returns>
        public PdfPageBuilder LinkPage(int pageIndex)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
            _owner.AddLinkPage(_pageIndex, pageIndex);
            return this;
        }

        /// <summary>
        /// Adds a named destination link annotation over the most recent text.
        /// </summary>
        /// <param name="destination">The named destination.</param>
        /// <returns>The current page builder for method chaining.</returns>
        public PdfPageBuilder LinkNamed(string destination)
        {
            ArgumentException.ThrowIfNullOrEmpty(destination);
            _owner.AddLinkNamed(_pageIndex, destination);
            return this;
        }

        /// <summary>
        /// Adds a JavaScript action link annotation over the most recent text.
        /// </summary>
        /// <param name="script">The JavaScript to run when clicked.</param>
        /// <returns>The current page builder for method chaining.</returns>
        public PdfPageBuilder LinkJavaScript(string script)
        {
            ArgumentNullException.ThrowIfNull(script);
            _owner.AddLinkJavaScript(_pageIndex, script);
            return this;
        }

        /// <summary>
        /// Adds a URL link annotation over a rectangle.
        /// </summary>
        /// <param name="rect">The link area in points.</param>
        /// <param name="url">The URL to open when the link is clicked.</param>
        /// <returns>The current page builder for method chaining.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Design",
            "CA1054:URI-like parameters should not be strings",
            Justification = "Matches the native string-based annotation API.")]
        public PdfPageBuilder LinkRect(PdfRect rect, string url)
        {
            ValidateRect(rect);
            ArgumentException.ThrowIfNullOrEmpty(url);
            _owner.AddLinkRect(_pageIndex, rect, url);
            return this;
        }

        /// <summary>
        /// Highlights the most recent text.
        /// </summary>
        /// <param name="color">The highlight color.</param>
        /// <returns>The current page builder for method chaining.</returns>
        public PdfPageBuilder Highlight(PdfColor color)
        {
            _owner.AddTextMarkup(_pageIndex, PdfTextMarkupType.Highlight, color);
            return this;
        }

        /// <summary>
        /// Underlines the most recent text.
        /// </summary>
        /// <param name="color">The underline color.</param>
        /// <returns>The current page builder for method chaining.</returns>
        public PdfPageBuilder Underline(PdfColor color)
        {
            _owner.AddTextMarkup(_pageIndex, PdfTextMarkupType.Underline, color);
            return this;
        }

        /// <summary>
        /// Strikes through the most recent text.
        /// </summary>
        /// <param name="color">The strikeout color.</param>
        /// <returns>The current page builder for method chaining.</returns>
        public PdfPageBuilder Strikeout(PdfColor color)
        {
            _owner.AddTextMarkup(_pageIndex, PdfTextMarkupType.StrikeOut, color);
            return this;
        }

        /// <summary>
        /// Adds a squiggly underline to the most recent text.
        /// </summary>
        /// <param name="color">The squiggly underline color.</param>
        /// <returns>The current page builder for method chaining.</returns>
        public PdfPageBuilder Squiggly(PdfColor color)
        {
            _owner.AddTextMarkup(_pageIndex, PdfTextMarkupType.Squiggly, color);
            return this;
        }

        /// <summary>
        /// Highlights a rectangle.
        /// </summary>
        /// <param name="rect">The area to highlight in points.</param>
        /// <param name="color">The highlight color.</param>
        /// <returns>The current page builder for method chaining.</returns>
        public PdfPageBuilder HighlightRect(PdfRect rect, PdfColor color)
        {
            ValidateRect(rect);
            _owner.AddTextMarkupRect(_pageIndex, PdfTextMarkupType.Highlight, rect, color);
            return this;
        }

        /// <summary>
        /// Underlines a rectangle.
        /// </summary>
        /// <param name="rect">The area to underline in points.</param>
        /// <param name="color">The underline color.</param>
        /// <returns>The current page builder for method chaining.</returns>
        public PdfPageBuilder UnderlineRect(PdfRect rect, PdfColor color)
        {
            ValidateRect(rect);
            _owner.AddTextMarkupRect(_pageIndex, PdfTextMarkupType.Underline, rect, color);
            return this;
        }

        /// <summary>
        /// Strikes through a rectangle.
        /// </summary>
        /// <param name="rect">The area to strike through in points.</param>
        /// <param name="color">The strikeout color.</param>
        /// <returns>The current page builder for method chaining.</returns>
        public PdfPageBuilder StrikeoutRect(PdfRect rect, PdfColor color)
        {
            ValidateRect(rect);
            _owner.AddTextMarkupRect(_pageIndex, PdfTextMarkupType.StrikeOut, rect, color);
            return this;
        }

        /// <summary>
        /// Adds a squiggly underline over a rectangle.
        /// </summary>
        /// <param name="rect">The area in points.</param>
        /// <param name="color">The squiggly underline color.</param>
        /// <returns>The current page builder for method chaining.</returns>
        public PdfPageBuilder SquigglyRect(PdfRect rect, PdfColor color)
        {
            ValidateRect(rect);
            _owner.AddTextMarkupRect(_pageIndex, PdfTextMarkupType.Squiggly, rect, color);
            return this;
        }

        /// <summary>
        /// Adds a sticky note annotation at the current cursor position.
        /// </summary>
        /// <param name="text">The note text.</param>
        /// <param name="icon">The note icon.</param>
        /// <returns>The current page builder for method chaining.</returns>
        public PdfPageBuilder StickyNote(string text, PdfTextAnnotationIcon icon = PdfTextAnnotationIcon.Note)
        {
            ArgumentNullException.ThrowIfNull(text);
            _owner.AddStickyNote(_pageIndex, text, icon);
            return this;
        }

        /// <summary>
        /// Adds a sticky note annotation at an absolute position.
        /// </summary>
        /// <param name="x">Horizontal position in points.</param>
        /// <param name="y">Vertical position in points.</param>
        /// <param name="text">The note text.</param>
        /// <param name="icon">The note icon.</param>
        /// <returns>The current page builder for method chaining.</returns>
        public PdfPageBuilder StickyNoteAt(
            float x,
            float y,
            string text,
            PdfTextAnnotationIcon icon = PdfTextAnnotationIcon.Note)
        {
            ArgumentNullException.ThrowIfNull(text);
            if (!float.IsFinite(x) || !float.IsFinite(y))
            {
                throw new ArgumentOutOfRangeException(nameof(x), "Sticky note position must be finite.");
            }
            _owner.AddStickyNoteAt(_pageIndex, x, y, text, icon);
            return this;
        }

        /// <summary>
        /// Adds a free text annotation over a rectangle using default font settings.
        /// </summary>
        /// <param name="rect">The text box in points.</param>
        /// <param name="text">The annotation text.</param>
        /// <returns>The current page builder for method chaining.</returns>
        public PdfPageBuilder FreeText(PdfRect rect, string text)
        {
            ValidateRect(rect);
            ArgumentNullException.ThrowIfNull(text);
            _owner.AddFreeText(_pageIndex, rect, text, null, 0f);
            return this;
        }

        /// <summary>
        /// Adds a free text annotation over a rectangle with custom font settings.
        /// </summary>
        /// <param name="rect">The text box in points.</param>
        /// <param name="text">The annotation text.</param>
        /// <param name="font">The font name.</param>
        /// <param name="size">The font size in points.</param>
        /// <returns>The current page builder for method chaining.</returns>
        public PdfPageBuilder FreeText(PdfRect rect, string text, string font, float size)
        {
            ValidateRect(rect);
            ArgumentNullException.ThrowIfNull(text);
            ArgumentException.ThrowIfNullOrEmpty(font);
            if (!float.IsFinite(size) || size <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(size), size, "Font size must be positive and finite.");
            }
            _owner.AddFreeText(_pageIndex, rect, text, font, size);
            return this;
        }

        /// <summary>
        /// Adds a text watermark centered on the page.
        /// </summary>
        /// <param name="text">The watermark text.</param>
        /// <returns>The current page builder for method chaining.</returns>
        public PdfPageBuilder Watermark(string text)
        {
            ArgumentException.ThrowIfNullOrEmpty(text);
            _owner.AddWatermark(_pageIndex, text);
            return this;
        }

        /// <summary>
        /// Adds a "CONFIDENTIAL" watermark.
        /// </summary>
        /// <returns>The current page builder for method chaining.</returns>
        public PdfPageBuilder WatermarkConfidential()
        {
            _owner.AddWatermarkPreset(_pageIndex, 0);
            return this;
        }

        /// <summary>
        /// Adds a "DRAFT" watermark.
        /// </summary>
        /// <returns>The current page builder for method chaining.</returns>
        public PdfPageBuilder WatermarkDraft()
        {
            _owner.AddWatermarkPreset(_pageIndex, 1);
            return this;
        }

        /// <summary>
        /// Adds a custom watermark.
        /// </summary>
        /// <param name="options">The watermark options.</param>
        /// <returns>The current page builder for method chaining.</returns>
        public PdfPageBuilder Watermark(PdfWatermarkOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);
            ArgumentException.ThrowIfNullOrEmpty(options.Text);
            ArgumentException.ThrowIfNullOrEmpty(options.Font);
            if (!float.IsFinite(options.Opacity) || options.Opacity < 0f || options.Opacity > 1f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options), options.Opacity, "Watermark opacity must be between 0 and 1.");
            }
            if (!float.IsFinite(options.Rotation))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options), options.Rotation, "Watermark rotation must be finite.");
            }
            if (!float.IsFinite(options.Size) || options.Size <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options), options.Size, "Watermark font size must be positive and finite.");
            }
            if (options.Rect.HasValue)
            {
                ValidateRect(options.Rect.Value);
            }

            _owner.AddWatermarkCustom(_pageIndex, options);
            return this;
        }

        /// <summary>
        /// Adds a stamp annotation at the current cursor position.
        /// </summary>
        /// <param name="type">The stamp type.</param>
        /// <returns>The current page builder for method chaining.</returns>
        public PdfPageBuilder Stamp(PdfStampType type)
        {
            _owner.AddStamp(_pageIndex, type.ToPdfName());
            return this;
        }

        /// <summary>
        /// Adds a custom stamp annotation with an explicit PDF stamp name at the
        /// current cursor position.
        /// </summary>
        /// <param name="customName">The custom stamp name.</param>
        /// <returns>The current page builder for method chaining.</returns>
        public PdfPageBuilder Stamp(string customName)
        {
            ArgumentException.ThrowIfNullOrEmpty(customName);
            _owner.AddStamp(_pageIndex, customName);
            return this;
        }

        /// <summary>
        /// Adds a stamp annotation over a rectangle.
        /// </summary>
        /// <param name="rect">The stamp area in points.</param>
        /// <param name="type">The stamp type.</param>
        /// <returns>The current page builder for method chaining.</returns>
        public PdfPageBuilder StampAt(PdfRect rect, PdfStampType type)
        {
            ValidateRect(rect);
            _owner.AddStampAt(_pageIndex, rect, type.ToPdfName());
            return this;
        }

        /// <summary>
        /// Adds a custom stamp annotation over a rectangle.
        /// </summary>
        /// <param name="rect">The stamp area in points.</param>
        /// <param name="customName">The custom stamp name.</param>
        /// <returns>The current page builder for method chaining.</returns>
        public PdfPageBuilder StampAt(PdfRect rect, string customName)
        {
            ValidateRect(rect);
            ArgumentException.ThrowIfNullOrEmpty(customName);
            _owner.AddStampAt(_pageIndex, rect, customName);
            return this;
        }

        /// <summary>
        /// Adds a footnote at the current cursor position.
        /// </summary>
        /// <param name="referenceMark">The reference mark, such as "1".</param>
        /// <param name="noteText">The footnote body text.</param>
        /// <returns>The current page builder for method chaining.</returns>
        public PdfPageBuilder Footnote(string referenceMark, string noteText)
        {
            ArgumentNullException.ThrowIfNull(referenceMark);
            ArgumentNullException.ThrowIfNull(noteText);
            _owner.AddFootnote(_pageIndex, referenceMark, noteText);
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
