using System;
using System.Runtime.InteropServices;
using System.Text;

namespace PdfOxide
{
    /// <summary>
    /// Builds PDF documents page by page with text, rich text, lists, code
    /// blocks, tables, and images.
    /// </summary>
    /// <remarks>
    /// Add a page with <see cref="AddPage(PdfPageSize)"/>, write content through
    /// the returned <see cref="PdfPageBuilder"/>, then call <see cref="Save"/> or
    /// <see cref="ToBytes"/>. Content can be added to any page that has already
    /// been added, in any order, before saving. Content is buffered in native
    /// code and rendered when the document is saved, so the builder remains
    /// usable afterwards.
    /// </remarks>
    public sealed unsafe class DocumentBuilder : IDisposable
    {
        private PdfBuilderState* _state;
        private int _pageCount;
        private bool _disposed;

        /// <summary>
        /// Creates a new document builder.
        /// </summary>
        /// <exception cref="PdfOxideException">Thrown when the native builder cannot be created.</exception>
        public DocumentBuilder()
        {
            _state = NativeMethods.pdf_builder_create();
            if (_state == null)
            {
                throw new PdfOxideException("Failed to create DocumentBuilder.");
            }
        }

        /// <summary>
        /// Sets the document metadata.
        /// </summary>
        /// <param name="metadata">The metadata to write.</param>
        /// <returns>The current builder instance for method chaining.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when the builder has been disposed.</exception>
        public DocumentBuilder SetMetadata(PdfMetadata metadata)
        {
            ArgumentNullException.ThrowIfNull(metadata);
            CheckDisposed();

            byte[]? title = Utf8OrNull(metadata.Title);
            byte[]? author = Utf8OrNull(metadata.Author);
            byte[]? subject = Utf8OrNull(metadata.Subject);
            byte[]? keywords = Utf8OrNull(metadata.Keywords);
            byte[]? creator = Utf8OrNull(metadata.Creator);

            fixed (byte* pTitle = title)
            fixed (byte* pAuthor = author)
            fixed (byte* pSubject = subject)
            fixed (byte* pKeywords = keywords)
            fixed (byte* pCreator = creator)
            {
                ThrowIfFailed(NativeMethods.pdf_builder_set_metadata(
                    _state, pTitle, pAuthor, pSubject, pKeywords, pCreator));
            }

            return this;
        }

        /// <summary>
        /// Registers an embedded font from a file.
        /// </summary>
        /// <param name="name">The name used to select the font in text styles.</param>
        /// <param name="fontPath">Path to a TrueType or OpenType font file.</param>
        /// <returns>The current builder instance for method chaining.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> or <paramref name="fontPath"/> is empty.</exception>
        /// <exception cref="PdfOxideException">Thrown when the font file cannot be read or registered.</exception>
        public DocumentBuilder RegisterFont(string name, string fontPath)
        {
            ArgumentException.ThrowIfNullOrEmpty(name);
            ArgumentException.ThrowIfNullOrEmpty(fontPath);
            CheckDisposed();

            fixed (byte* pName = Utf8(name))
            fixed (byte* pPath = Utf8(fontPath))
            {
                ThrowIfFailed(NativeMethods.pdf_builder_register_font_file(_state, pName, pPath));
            }

            return this;
        }

        /// <summary>
        /// Registers an embedded font from memory.
        /// </summary>
        /// <param name="name">The name used to select the font in text styles.</param>
        /// <param name="fontData">Raw TrueType or OpenType font bytes.</param>
        /// <returns>The current builder instance for method chaining.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is empty or <paramref name="fontData"/> is empty.</exception>
        /// <exception cref="PdfOxideException">Thrown when the font cannot be registered.</exception>
        public DocumentBuilder RegisterFont(string name, byte[] fontData)
        {
            ArgumentException.ThrowIfNullOrEmpty(name);
            ArgumentNullException.ThrowIfNull(fontData);
            if (fontData.Length == 0)
            {
                throw new ArgumentException("Font data must not be empty.", nameof(fontData));
            }
            CheckDisposed();

            fixed (byte* pName = Utf8(name))
            fixed (byte* pData = fontData)
            {
                ThrowIfFailed(NativeMethods.pdf_builder_register_font_bytes(
                    _state, pName, pData, fontData.Length));
            }

            return this;
        }

        /// <summary>
        /// Registers the bundled DejaVu Sans and DejaVu Sans Bold fonts.
        /// </summary>
        /// <returns>The current builder instance for method chaining.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when the builder has been disposed.</exception>
        /// <exception cref="PdfOxideException">Thrown when the fonts cannot be registered.</exception>
        public DocumentBuilder RegisterBundledFonts()
        {
            CheckDisposed();
            ThrowIfFailed(NativeMethods.pdf_builder_register_bundled_fonts(_state));
            return this;
        }

        /// <summary>
        /// Enables or disables PDF/UA-1 tagging for the document.
        /// </summary>
        /// <param name="enabled">Whether to write the tagged PDF/UA-1 structure.</param>
        /// <returns>The current builder instance for method chaining.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when the builder has been disposed.</exception>
        public DocumentBuilder SetTaggedPdfUa1(bool enabled = true)
        {
            CheckDisposed();
            ThrowIfFailed(NativeMethods.pdf_builder_set_tagged_pdf_ua1(_state, enabled ? 1 : 0));
            return this;
        }

        /// <summary>
        /// Sets the document language tag, such as <c>en-US</c>.
        /// </summary>
        /// <param name="languageTag">The BCP 47 language tag.</param>
        /// <returns>The current builder instance for method chaining.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="languageTag"/> is empty.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the builder has been disposed.</exception>
        public DocumentBuilder SetLanguage(string languageTag)
        {
            ArgumentException.ThrowIfNullOrEmpty(languageTag);
            CheckDisposed();

            fixed (byte* pLanguage = Utf8(languageTag))
            {
                ThrowIfFailed(NativeMethods.pdf_builder_set_language(_state, pLanguage));
            }

            return this;
        }

        /// <summary>
        /// Adds a custom-to-standard structure type role map entry.
        /// </summary>
        /// <param name="customType">The custom structure type name.</param>
        /// <param name="standardType">The standard structure type it maps to.</param>
        /// <returns>The current builder instance for method chaining.</returns>
        /// <exception cref="ArgumentException">Thrown when either type name is empty.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the builder has been disposed.</exception>
        public DocumentBuilder AddRoleMap(string customType, string standardType)
        {
            ArgumentException.ThrowIfNullOrEmpty(customType);
            ArgumentException.ThrowIfNullOrEmpty(standardType);
            CheckDisposed();

            fixed (byte* pCustom = Utf8(customType))
            fixed (byte* pStandard = Utf8(standardType))
            {
                ThrowIfFailed(NativeMethods.pdf_builder_add_role_map(_state, pCustom, pStandard));
            }

            return this;
        }

        /// <summary>
        /// Adds an A4 page and returns a builder for its content.
        /// </summary>
        /// <returns>A builder for the new page.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when the builder has been disposed.</exception>
        public PdfPageBuilder AddPage()
        {
            return AddPage(PdfPageSize.A4);
        }

        /// <summary>
        /// Adds a page and returns a builder for its content.
        /// </summary>
        /// <param name="size">The page size.</param>
        /// <returns>A builder for the new page.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when the builder has been disposed.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="size"/> is not a defined value.</exception>
        public PdfPageBuilder AddPage(PdfPageSize size)
        {
            CheckDisposed();
            int kind = (int)size;
            if (kind < 0 || kind > (int)PdfPageSize.Custom)
            {
                throw new ArgumentOutOfRangeException(nameof(size), size, "Unknown page size.");
            }

            return AddPageCore(kind, 0f, 0f);
        }

        /// <summary>
        /// Adds a custom-sized page in points and returns a builder for its content.
        /// </summary>
        /// <param name="width">Page width in points.</param>
        /// <param name="height">Page height in points.</param>
        /// <returns>A builder for the new page.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when the builder has been disposed.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the dimensions are not positive finite numbers.</exception>
        public PdfPageBuilder AddPage(float width, float height)
        {
            CheckDisposed();
            if (!float.IsFinite(width) || !float.IsFinite(height) || width <= 0f || height <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(width), "Page dimensions must be positive finite numbers.");
            }

            return AddPageCore((int)PdfPageSize.Custom, width, height);
        }

        /// <summary>
        /// Adds a single A4 page containing the specified text.
        /// </summary>
        /// <param name="text">The page text.</param>
        /// <returns>The current builder instance for method chaining.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when the builder has been disposed.</exception>
        public DocumentBuilder AddPage(string text)
        {
            ArgumentNullException.ThrowIfNull(text);
            _ = AddPage(PdfPageSize.A4).Text(text);
            return this;
        }

        /// <summary>
        /// Starts a streaming page builder that can insert page breaks while
        /// writing content.
        /// </summary>
        /// <param name="size">The size of each page.</param>
        /// <returns>A streaming page builder whose first page has been added.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when the builder has been disposed.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="size"/> is not a defined value.</exception>
        public PdfStreamingPageBuilder BeginStreamingPage(PdfPageSize size = PdfPageSize.A4)
        {
            CheckDisposed();
            int kind = (int)size;
            if (kind < 0 || kind > (int)PdfPageSize.Custom)
            {
                throw new ArgumentOutOfRangeException(nameof(size), size, "Unknown page size.");
            }

            return new PdfStreamingPageBuilder(this, size);
        }

        /// <summary>
        /// Saves the document to a file.
        /// </summary>
        /// <param name="outputPath">The output file path.</param>
        /// <exception cref="ObjectDisposedException">Thrown when the builder has been disposed.</exception>
        /// <exception cref="PdfOxideException">Thrown when rendering or writing fails.</exception>
        public void Save(string outputPath)
        {
            ArgumentNullException.ThrowIfNull(outputPath);
            CheckDisposed();

            fixed (byte* pPath = Utf8(outputPath))
            {
                ThrowIfFailed(NativeMethods.pdf_builder_save(_state, pPath));
            }
        }

        /// <summary>
        /// Renders the document and returns it as a byte array.
        /// </summary>
        /// <returns>The PDF bytes.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when the builder has been disposed.</exception>
        /// <exception cref="PdfOxideException">Thrown when rendering fails.</exception>
        public byte[] ToBytes()
        {
            CheckDisposed();

            byte* data = null;
            int length = 0;
            ThrowIfFailed(NativeMethods.pdf_builder_build(_state, &data, &length));
            if (length <= 0)
            {
                return [];
            }

            try
            {
                var result = new byte[length];
                Marshal.Copy((IntPtr)data, result, 0, length);
                return result;
            }
            finally
            {
                NativeMethods.pdf_builder_bytes_free(data, length);
            }
        }

        internal void AddText(int pageIndex, string text, PdfTextStyle? style)
        {
            CheckDisposed();
            byte[]? font = style is null ? null : Utf8(style.Font);

            fixed (byte* pText = Utf8(text))
            fixed (byte* pFont = font)
            {
                ThrowIfFailed(NativeMethods.pdf_builder_add_text(
                    _state, pageIndex, pText, pFont, style?.Size ?? 0f));
            }
        }

        internal void AddTextAt(int pageIndex, float x, float y, string text, PdfTextStyle? style)
        {
            CheckDisposed();
            byte[]? font = style is null ? null : Utf8(style.Font);

            fixed (byte* pText = Utf8(text))
            fixed (byte* pFont = font)
            {
                ThrowIfFailed(NativeMethods.pdf_builder_add_text_at(
                    _state, pageIndex, x, y, pText, pFont, style?.Size ?? 0f));
            }
        }

        internal void AddHeading(int pageIndex, int level, string text)
        {
            CheckDisposed();

            fixed (byte* pText = Utf8(text))
            {
                ThrowIfFailed(NativeMethods.pdf_builder_add_heading(_state, pageIndex, level, pText));
            }
        }

        internal void AddParagraph(int pageIndex, string text)
        {
            CheckDisposed();

            fixed (byte* pText = Utf8(text))
            {
                ThrowIfFailed(NativeMethods.pdf_builder_add_paragraph(_state, pageIndex, pText));
            }
        }

        internal void AddList(int pageIndex, string[] items, bool numbered, PdfListStyle style)
        {
            CheckDisposed();

            foreach (string item in items)
            {
                if (item.Contains('\n', StringComparison.Ordinal))
                {
                    throw new ArgumentException("List items must not contain newline characters.", nameof(items));
                }
            }

            fixed (byte* pItems = Utf8(string.Join('\n', items)))
            {
                ThrowIfFailed(NativeMethods.pdf_builder_add_list(
                    _state, pageIndex, pItems, numbered ? 1 : 0, (int)style));
            }
        }

        internal void AddCodeBlock(int pageIndex, string language, string source)
        {
            CheckDisposed();

            fixed (byte* pLanguage = Utf8(language))
            fixed (byte* pSource = Utf8(source))
            {
                ThrowIfFailed(NativeMethods.pdf_builder_add_code_block(_state, pageIndex, pLanguage, pSource));
            }
        }

        internal void AddRichParagraph(int pageIndex, PdfTextRun[] runs)
        {
            CheckDisposed();
            if (runs.Length == 0)
            {
                return;
            }

            var encoded = new byte[runs.Length][];
            var styleCodes = new int[runs.Length];
            var colors = new float[runs.Length * 3];
            for (int index = 0; index < runs.Length; index++)
            {
                PdfTextRun run = runs[index];
                encoded[index] = Utf8(run.Text);
                styleCodes[index] = (int)run.Style;
                int colorIndex = index * 3;
                colors[colorIndex] = run.Color.R;
                colors[colorIndex + 1] = run.Color.G;
                colors[colorIndex + 2] = run.Color.B;
            }

            var handles = new GCHandle[runs.Length];
            var pointers = new byte*[runs.Length];
            try
            {
                for (int index = 0; index < runs.Length; index++)
                {
                    handles[index] = GCHandle.Alloc(encoded[index], GCHandleType.Pinned);
                    pointers[index] = (byte*)handles[index].AddrOfPinnedObject();
                }

                fixed (byte** pTexts = pointers)
                fixed (int* pStyles = styleCodes)
                fixed (float* pColors = colors)
                {
                    ThrowIfFailed(NativeMethods.pdf_builder_add_rich_paragraph(
                        _state, pageIndex, pTexts, pStyles, pColors, runs.Length));
                }
            }
            finally
            {
                foreach (GCHandle handle in handles)
                {
                    if (handle.IsAllocated)
                    {
                        handle.Free();
                    }
                }
            }
        }

        internal void AddSpace(int pageIndex, float points)
        {
            CheckDisposed();
            ThrowIfFailed(NativeMethods.pdf_builder_add_space(_state, pageIndex, points));
        }

        internal void AddHorizontalRule(int pageIndex)
        {
            CheckDisposed();
            ThrowIfFailed(NativeMethods.pdf_builder_add_horizontal_rule(_state, pageIndex));
        }

        internal void AddColumns(int pageIndex, int count, float gap, string text)
        {
            CheckDisposed();

            fixed (byte* pText = Utf8(text))
            {
                ThrowIfFailed(NativeMethods.pdf_builder_add_columns(_state, pageIndex, count, gap, pText));
            }
        }

        internal void AddTable(int pageIndex, PdfTable table)
        {
            CheckDisposed();

            string[] cells = table.GetCells();
            var encoded = new byte[cells.Length][];
            for (int index = 0; index < cells.Length; index++)
            {
                encoded[index] = Utf8(cells[index]);
            }

            var handles = new GCHandle[cells.Length];
            var pointers = new byte*[cells.Length];
            try
            {
                for (int index = 0; index < cells.Length; index++)
                {
                    handles[index] = GCHandle.Alloc(encoded[index], GCHandleType.Pinned);
                    pointers[index] = (byte*)handles[index].AddrOfPinnedObject();
                }

                fixed (byte** pCells = pointers)
                {
                    ThrowIfFailed(NativeMethods.pdf_builder_add_table(
                        _state,
                        pageIndex,
                        pCells,
                        table.RowCount,
                        table.ColumnCount,
                        table.HasHeaderRow ? 1 : 0,
                        (int)table.Style));
                }
            }
            finally
            {
                foreach (GCHandle handle in handles)
                {
                    if (handle.IsAllocated)
                    {
                        handle.Free();
                    }
                }
            }
        }

        internal void BeginStreamingTable(int pageIndex, PdfStreamingTableOptions options)
        {
            CheckDisposed();
            ArgumentNullException.ThrowIfNull(options);
            PdfStreamingColumn[] columns = options.Columns;
            ArgumentNullException.ThrowIfNull(columns);
            if (columns.Length == 0)
            {
                throw new ArgumentException("At least one column is required.", nameof(options));
            }

            var widths = new float[columns.Length];
            var aligns = new int[columns.Length];
            var encoded = new byte[columns.Length][];
            for (int index = 0; index < columns.Length; index++)
            {
                PdfStreamingColumn? column = columns[index];
                if (column is null)
                {
                    throw new ArgumentException("Columns must not be null.", nameof(options));
                }
                ArgumentException.ThrowIfNullOrEmpty(column.Header);
                if (!float.IsFinite(column.Width) || column.Width <= 0f)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(options), column.Width, "Column widths must be positive finite numbers.");
                }
                if (!Enum.IsDefined(column.Alignment))
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(options), column.Alignment, "Unknown cell alignment.");
                }

                widths[index] = column.Width;
                aligns[index] = (int)column.Alignment;
                encoded[index] = Utf8(column.Header);
            }

            if (!float.IsFinite(options.GridWidth) || options.GridWidth < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options), options.GridWidth, "Grid width must be a non-negative finite number.");
            }
            ValidatePadding(options.CellPaddingHorizontal, nameof(options));
            ValidatePadding(options.CellPaddingTop, nameof(options));
            ValidatePadding(options.CellPaddingBottom, nameof(options));
            if (options.MaxRowspan < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options), options.MaxRowspan, "MaxRowspan must be at least 1.");
            }
            if (options.GridColor is { } gridColor)
            {
                ValidateColor(gridColor, nameof(options));
            }
            if (options.HeaderFill is { } headerFillColor)
            {
                ValidateColor(headerFillColor, nameof(options));
            }

            bool hasHeaderFill = options.HeaderFill.HasValue;
            PdfColor headerFill = options.HeaderFill ?? default;
            bool hasGrid = options.GridColor.HasValue;
            PdfColor grid = options.GridColor ?? default;

            var handles = new GCHandle[encoded.Length];
            var pointers = new byte*[encoded.Length];
            try
            {
                for (int index = 0; index < encoded.Length; index++)
                {
                    handles[index] = GCHandle.Alloc(encoded[index], GCHandleType.Pinned);
                    pointers[index] = (byte*)handles[index].AddrOfPinnedObject();
                }

                fixed (byte** pHeaders = pointers)
                fixed (float* pWidths = widths)
                fixed (int* pAligns = aligns)
                {
                    ThrowIfFailed(NativeMethods.pdf_builder_streaming_table_begin(
                        _state,
                        pageIndex,
                        pHeaders,
                        pWidths,
                        pAligns,
                        columns.Length,
                        options.RepeatHeader ? 1 : 0,
                        hasHeaderFill ? 1 : 0,
                        headerFill.R,
                        headerFill.G,
                        headerFill.B,
                        hasGrid ? 1 : 0,
                        grid.R,
                        grid.G,
                        grid.B,
                        options.GridWidth,
                        options.CellPaddingHorizontal,
                        options.CellPaddingTop,
                        options.CellPaddingBottom,
                        options.MaxRowspan));
                }
            }
            finally
            {
                foreach (GCHandle handle in handles)
                {
                    if (handle.IsAllocated)
                    {
                        handle.Free();
                    }
                }
            }
        }

        internal void AddStreamingTableRow(int pageIndex, string[] cells, int[]? rowspans)
        {
            CheckDisposed();
            ArgumentNullException.ThrowIfNull(cells);
            if (cells.Length == 0)
            {
                throw new ArgumentException("A row must contain at least one cell.", nameof(cells));
            }
            if (rowspans is not null && rowspans.Length != cells.Length)
            {
                throw new ArgumentException(
                    "Rowspans must match the number of cells.", nameof(rowspans));
            }

            var encoded = new byte[cells.Length][];
            for (int index = 0; index < cells.Length; index++)
            {
                string? cell = cells[index];
                if (cell is null)
                {
                    throw new ArgumentException("Table cells must not be null.", nameof(cells));
                }
                encoded[index] = Utf8(cell);
            }

            var handles = new GCHandle[encoded.Length];
            var pointers = new byte*[encoded.Length];
            try
            {
                for (int index = 0; index < encoded.Length; index++)
                {
                    handles[index] = GCHandle.Alloc(encoded[index], GCHandleType.Pinned);
                    pointers[index] = (byte*)handles[index].AddrOfPinnedObject();
                }

                fixed (byte** pCells = pointers)
                fixed (int* pRowspans = rowspans)
                {
                    ThrowIfFailed(NativeMethods.pdf_builder_streaming_table_add_row(
                        _state, pCells, pRowspans, cells.Length));
                }
            }
            finally
            {
                foreach (GCHandle handle in handles)
                {
                    if (handle.IsAllocated)
                    {
                        handle.Free();
                    }
                }
            }
        }

        internal void EndStreamingTable(int pageIndex)
        {
            CheckDisposed();
            ThrowIfFailed(NativeMethods.pdf_builder_streaming_table_end(_state));
        }

        internal PdfPageBuilder AddPageLike(int referencePageIndex)
        {
            CheckDisposed();
            ArgumentOutOfRangeException.ThrowIfNegative(referencePageIndex);

            int pageIndex = NativeMethods.pdf_builder_add_page_like(_state, referencePageIndex);
            if (pageIndex < 0)
            {
                throw new PdfOxideException(GetLastError());
            }

            _pageCount = Math.Max(_pageCount, pageIndex + 1);
            return new PdfPageBuilder(this, pageIndex);
        }

        internal void AddImage(int pageIndex, string path, PdfRect rect, string? altText)
        {
            CheckDisposed();
            byte[]? alt = Utf8OrNull(altText);

            fixed (byte* pPath = Utf8(path))
            fixed (byte* pAlt = alt)
            {
                ThrowIfFailed(NativeMethods.pdf_builder_add_image_file(
                    _state, pageIndex, pPath, rect.X, rect.Y, rect.Width, rect.Height, pAlt));
            }
        }

        internal void AddImage(int pageIndex, byte[] imageData, PdfRect rect, string? altText)
        {
            CheckDisposed();
            byte[]? alt = Utf8OrNull(altText);

            fixed (byte* pData = imageData)
            fixed (byte* pAlt = alt)
            {
                ThrowIfFailed(NativeMethods.pdf_builder_add_image_bytes(
                    _state, pageIndex, pData, imageData.Length, rect.X, rect.Y, rect.Width, rect.Height, pAlt));
            }
        }

        internal void AddLinkUrl(int pageIndex, string url)
        {
            CheckDisposed();

            fixed (byte* pUrl = Utf8(url))
            {
                ThrowIfFailed(NativeMethods.pdf_builder_add_link_url(_state, pageIndex, pUrl));
            }
        }

        internal void AddLinkPage(int pageIndex, int targetPage)
        {
            CheckDisposed();
            ThrowIfFailed(NativeMethods.pdf_builder_add_link_page(_state, pageIndex, targetPage));
        }

        internal void AddLinkNamed(int pageIndex, string destination)
        {
            CheckDisposed();

            fixed (byte* pDestination = Utf8(destination))
            {
                ThrowIfFailed(NativeMethods.pdf_builder_add_link_named(_state, pageIndex, pDestination));
            }
        }

        internal void AddLinkJavaScript(int pageIndex, string script)
        {
            CheckDisposed();

            fixed (byte* pScript = Utf8(script))
            {
                ThrowIfFailed(NativeMethods.pdf_builder_add_link_javascript(_state, pageIndex, pScript));
            }
        }

        internal void AddLinkRect(int pageIndex, PdfRect rect, string url)
        {
            CheckDisposed();

            fixed (byte* pUrl = Utf8(url))
            {
                ThrowIfFailed(NativeMethods.pdf_builder_add_link_rect(
                    _state, pageIndex, rect.X, rect.Y, rect.Width, rect.Height, pUrl));
            }
        }

        internal void AddTextMarkup(int pageIndex, PdfTextMarkupType kind, PdfColor color)
        {
            CheckDisposed();
            ThrowIfFailed(NativeMethods.pdf_builder_add_text_markup(
                _state, pageIndex, (int)kind, color.R, color.G, color.B));
        }

        internal void AddTextMarkupRect(int pageIndex, PdfTextMarkupType kind, PdfRect rect, PdfColor color)
        {
            CheckDisposed();
            ThrowIfFailed(NativeMethods.pdf_builder_add_text_markup_rect(
                _state, pageIndex, (int)kind,
                rect.X, rect.Y, rect.Width, rect.Height,
                color.R, color.G, color.B));
        }

        internal void AddStickyNote(int pageIndex, string text, PdfTextAnnotationIcon icon)
        {
            CheckDisposed();

            fixed (byte* pText = Utf8(text))
            {
                ThrowIfFailed(NativeMethods.pdf_builder_add_sticky_note(_state, pageIndex, pText, (int)icon));
            }
        }

        internal void AddStickyNoteAt(int pageIndex, float x, float y, string text, PdfTextAnnotationIcon icon)
        {
            CheckDisposed();

            fixed (byte* pText = Utf8(text))
            {
                ThrowIfFailed(NativeMethods.pdf_builder_add_sticky_note_at(
                    _state, pageIndex, x, y, pText, (int)icon));
            }
        }

        internal void AddFreeText(int pageIndex, PdfRect rect, string text, string? font, float size)
        {
            CheckDisposed();
            byte[]? fontBytes = Utf8OrNull(font);

            fixed (byte* pText = Utf8(text))
            fixed (byte* pFont = fontBytes)
            {
                ThrowIfFailed(NativeMethods.pdf_builder_add_freetext(
                    _state, pageIndex, rect.X, rect.Y, rect.Width, rect.Height, pText, pFont, size));
            }
        }

        internal void AddWatermark(int pageIndex, string text)
        {
            CheckDisposed();

            fixed (byte* pText = Utf8(text))
            {
                ThrowIfFailed(NativeMethods.pdf_builder_add_watermark(_state, pageIndex, pText));
            }
        }

        internal void AddWatermarkPreset(int pageIndex, int kind)
        {
            CheckDisposed();
            ThrowIfFailed(NativeMethods.pdf_builder_add_watermark_preset(_state, pageIndex, kind));
        }

        internal void AddWatermarkCustom(int pageIndex, PdfWatermarkOptions options)
        {
            CheckDisposed();
            PdfRect? rect = options.Rect;

            fixed (byte* pText = Utf8(options.Text))
            fixed (byte* pFont = Utf8(options.Font))
            {
                ThrowIfFailed(NativeMethods.pdf_builder_add_watermark_custom(
                    _state,
                    pageIndex,
                    pText,
                    options.Opacity,
                    options.Rotation,
                    pFont,
                    options.Size,
                    options.Color.R,
                    options.Color.G,
                    options.Color.B,
                    rect.HasValue ? 1 : 0,
                    rect?.X ?? 0f,
                    rect?.Y ?? 0f,
                    rect?.Width ?? 0f,
                    rect?.Height ?? 0f));
            }
        }

        internal void AddStamp(int pageIndex, string typeName)
        {
            CheckDisposed();

            fixed (byte* pTypeName = Utf8(typeName))
            {
                ThrowIfFailed(NativeMethods.pdf_builder_add_stamp(_state, pageIndex, pTypeName));
            }
        }

        internal void AddStampAt(int pageIndex, PdfRect rect, string typeName)
        {
            CheckDisposed();

            fixed (byte* pTypeName = Utf8(typeName))
            {
                ThrowIfFailed(NativeMethods.pdf_builder_add_stamp_at(
                    _state, pageIndex, rect.X, rect.Y, rect.Width, rect.Height, pTypeName));
            }
        }

        internal void AddFootnote(int pageIndex, string referenceMark, string noteText)
        {
            CheckDisposed();

            fixed (byte* pReferenceMark = Utf8(referenceMark))
            fixed (byte* pNoteText = Utf8(noteText))
            {
                ThrowIfFailed(NativeMethods.pdf_builder_add_footnote(
                    _state, pageIndex, pReferenceMark, pNoteText));
            }
        }

        internal void AddTextField(int pageIndex, string name, PdfRect rect, string? defaultValue)
        {
            CheckDisposed();
            byte[]? defaultBytes = Utf8OrNull(defaultValue);

            fixed (byte* pName = Utf8(name))
            fixed (byte* pDefaultValue = defaultBytes)
            {
                ThrowIfFailed(NativeMethods.pdf_builder_add_text_field(
                    _state, pageIndex, pName,
                    rect.X, rect.Y, rect.Width, rect.Height, pDefaultValue));
            }
        }

        internal void AddCheckbox(int pageIndex, string name, PdfRect rect, bool isChecked)
        {
            CheckDisposed();

            fixed (byte* pName = Utf8(name))
            {
                ThrowIfFailed(NativeMethods.pdf_builder_add_checkbox(
                    _state, pageIndex, pName,
                    rect.X, rect.Y, rect.Width, rect.Height, isChecked ? 1 : 0));
            }
        }

        internal void AddComboBox(int pageIndex, string name, PdfRect rect, string[] options, string? selected)
        {
            CheckDisposed();
            byte[]? selectedBytes = Utf8OrNull(selected);

            fixed (byte* pName = Utf8(name))
            fixed (byte* pOptions = Utf8(string.Join('\n', options)))
            fixed (byte* pSelected = selectedBytes)
            {
                ThrowIfFailed(NativeMethods.pdf_builder_add_combo_box(
                    _state, pageIndex, pName,
                    rect.X, rect.Y, rect.Width, rect.Height, pOptions, pSelected));
            }
        }

        internal void AddListBox(
            int pageIndex,
            string name,
            PdfRect rect,
            string[] options,
            string? selected,
            bool multiSelect)
        {
            CheckDisposed();
            byte[]? selectedBytes = Utf8OrNull(selected);

            fixed (byte* pName = Utf8(name))
            fixed (byte* pOptions = Utf8(string.Join('\n', options)))
            fixed (byte* pSelected = selectedBytes)
            {
                ThrowIfFailed(NativeMethods.pdf_builder_add_list_box(
                    _state, pageIndex, pName,
                    rect.X, rect.Y, rect.Width, rect.Height,
                    pOptions, pSelected, multiSelect ? 1 : 0));
            }
        }

        internal void AddRadioGroup(int pageIndex, string name, PdfRadioOption[] options, string? selected)
        {
            CheckDisposed();
            byte[]? selectedBytes = Utf8OrNull(selected);

            var encodedValues = new byte[options.Length][];
            var xs = new float[options.Length];
            var ys = new float[options.Length];
            var widths = new float[options.Length];
            var heights = new float[options.Length];
            for (int index = 0; index < options.Length; index++)
            {
                PdfRadioOption option = options[index];
                encodedValues[index] = Utf8(option.ExportValue);
                xs[index] = option.Rect.X;
                ys[index] = option.Rect.Y;
                widths[index] = option.Rect.Width;
                heights[index] = option.Rect.Height;
            }

            var handles = new GCHandle[options.Length];
            var pointers = new byte*[options.Length];
            try
            {
                for (int index = 0; index < options.Length; index++)
                {
                    handles[index] = GCHandle.Alloc(encodedValues[index], GCHandleType.Pinned);
                    pointers[index] = (byte*)handles[index].AddrOfPinnedObject();
                }

                fixed (byte* pName = Utf8(name))
                fixed (byte** pValues = pointers)
                fixed (float* pXs = xs)
                fixed (float* pYs = ys)
                fixed (float* pWidths = widths)
                fixed (float* pHeights = heights)
                fixed (byte* pSelected = selectedBytes)
                {
                    ThrowIfFailed(NativeMethods.pdf_builder_add_radio_group(
                        _state, pageIndex, pName,
                        pValues, pXs, pYs, pWidths, pHeights,
                        options.Length, pSelected));
                }
            }
            finally
            {
                foreach (GCHandle handle in handles)
                {
                    if (handle.IsAllocated)
                    {
                        handle.Free();
                    }
                }
            }
        }

        internal void AddPushButton(int pageIndex, string name, PdfRect rect, string caption)
        {
            CheckDisposed();

            fixed (byte* pName = Utf8(name))
            fixed (byte* pCaption = Utf8(caption))
            {
                ThrowIfFailed(NativeMethods.pdf_builder_add_push_button(
                    _state, pageIndex, pName,
                    rect.X, rect.Y, rect.Width, rect.Height, pCaption));
            }
        }

        internal void AddSignatureField(int pageIndex, string name, PdfRect rect)
        {
            CheckDisposed();

            fixed (byte* pName = Utf8(name))
            {
                ThrowIfFailed(NativeMethods.pdf_builder_add_signature_field(
                    _state, pageIndex, pName,
                    rect.X, rect.Y, rect.Width, rect.Height));
            }
        }

        internal void AddQrCode(int pageIndex, string data, float x, float y, float size)
        {
            CheckDisposed();

            fixed (byte* pData = Utf8(data))
            {
                ThrowIfFailed(NativeMethods.pdf_builder_add_qr_code(
                    _state, pageIndex, pData, x, y, size));
            }
        }

        internal void AddBarcode(int pageIndex, PdfBarcodeType type, string data, PdfRect rect)
        {
            CheckDisposed();

            fixed (byte* pData = Utf8(data))
            {
                ThrowIfFailed(NativeMethods.pdf_builder_add_barcode(
                    _state, pageIndex, (int)type, pData,
                    rect.X, rect.Y, rect.Width, rect.Height));
            }
        }

        internal void AddRect(int pageIndex, PdfRect rect)
        {
            CheckDisposed();
            ThrowIfFailed(NativeMethods.pdf_builder_add_rect(
                _state, pageIndex, rect.X, rect.Y, rect.Width, rect.Height));
        }

        internal void AddFilledRect(int pageIndex, PdfRect rect, PdfColor color)
        {
            CheckDisposed();
            ThrowIfFailed(NativeMethods.pdf_builder_add_filled_rect(
                _state, pageIndex,
                rect.X, rect.Y, rect.Width, rect.Height,
                color.R, color.G, color.B));
        }

        internal void AddLine(int pageIndex, float x1, float y1, float x2, float y2)
        {
            CheckDisposed();
            ThrowIfFailed(NativeMethods.pdf_builder_add_line(
                _state, pageIndex, x1, y1, x2, y2));
        }

        internal void AddStrokeRect(int pageIndex, PdfRect rect, PdfLineStyle style)
        {
            CheckDisposed();
            ValidateLineStyle(style);

            float[] dash = style.DashPattern ?? [];
            fixed (float* pDash = dash)
            {
                ThrowIfFailed(NativeMethods.pdf_builder_add_stroke_rect(
                    _state, pageIndex,
                    rect.X, rect.Y, rect.Width, rect.Height,
                    style.Width, style.Color.R, style.Color.G, style.Color.B,
                    pDash, dash.Length, style.DashPhase));
            }
        }

        internal void AddStrokeLine(
            int pageIndex,
            float x1,
            float y1,
            float x2,
            float y2,
            PdfLineStyle style)
        {
            CheckDisposed();
            ValidateLineStyle(style);

            float[] dash = style.DashPattern ?? [];
            fixed (float* pDash = dash)
            {
                ThrowIfFailed(NativeMethods.pdf_builder_add_stroke_line(
                    _state, pageIndex, x1, y1, x2, y2,
                    style.Width, style.Color.R, style.Color.G, style.Color.B,
                    pDash, dash.Length, style.DashPhase));
            }
        }

        internal void AddCircle(
            int pageIndex,
            float cx,
            float cy,
            float radius,
            PdfLineStyle? stroke,
            PdfColor? fill)
        {
            CheckDisposed();
            if (stroke is not null)
            {
                ValidateLineStyle(stroke);
            }

            float[] dash = stroke?.DashPattern ?? [];
            fixed (float* pDash = dash)
            {
                ThrowIfFailed(NativeMethods.pdf_builder_add_circle(
                    _state, pageIndex, cx, cy, radius,
                    stroke is null ? 0 : 1,
                    stroke?.Width ?? 0f,
                    stroke?.Color.R ?? 0f,
                    stroke?.Color.G ?? 0f,
                    stroke?.Color.B ?? 0f,
                    pDash, dash.Length, stroke?.DashPhase ?? 0f,
                    fill.HasValue ? 1 : 0,
                    fill?.R ?? 0f,
                    fill?.G ?? 0f,
                    fill?.B ?? 0f));
            }
        }

        internal void AddEllipse(
            int pageIndex,
            float cx,
            float cy,
            float rx,
            float ry,
            PdfLineStyle? stroke,
            PdfColor? fill)
        {
            CheckDisposed();
            if (stroke is not null)
            {
                ValidateLineStyle(stroke);
            }

            float[] dash = stroke?.DashPattern ?? [];
            fixed (float* pDash = dash)
            {
                ThrowIfFailed(NativeMethods.pdf_builder_add_ellipse(
                    _state, pageIndex, cx, cy, rx, ry,
                    stroke is null ? 0 : 1,
                    stroke?.Width ?? 0f,
                    stroke?.Color.R ?? 0f,
                    stroke?.Color.G ?? 0f,
                    stroke?.Color.B ?? 0f,
                    pDash, dash.Length, stroke?.DashPhase ?? 0f,
                    fill.HasValue ? 1 : 0,
                    fill?.R ?? 0f,
                    fill?.G ?? 0f,
                    fill?.B ?? 0f));
            }
        }

        internal void AddPolygon(
            int pageIndex,
            PdfPoint[] points,
            PdfLineStyle? stroke,
            PdfColor? fill)
        {
            CheckDisposed();
            if (stroke is not null)
            {
                ValidateLineStyle(stroke);
            }

            var coords = new float[points.Length * 2];
            for (int index = 0; index < points.Length; index++)
            {
                coords[index * 2] = points[index].X;
                coords[index * 2 + 1] = points[index].Y;
            }

            float[] dash = stroke?.DashPattern ?? [];
            fixed (float* pCoords = coords)
            fixed (float* pDash = dash)
            {
                ThrowIfFailed(NativeMethods.pdf_builder_add_polygon(
                    _state, pageIndex, pCoords, points.Length,
                    stroke is null ? 0 : 1,
                    stroke?.Width ?? 0f,
                    stroke?.Color.R ?? 0f,
                    stroke?.Color.G ?? 0f,
                    stroke?.Color.B ?? 0f,
                    pDash, dash.Length, stroke?.DashPhase ?? 0f,
                    fill.HasValue ? 1 : 0,
                    fill?.R ?? 0f,
                    fill?.G ?? 0f,
                    fill?.B ?? 0f));
            }
        }

        internal void AddArc(
            int pageIndex,
            float cx,
            float cy,
            float radius,
            float startRadians,
            float endRadians,
            PdfLineStyle style)
        {
            CheckDisposed();
            ValidateLineStyle(style);

            float[] dash = style.DashPattern ?? [];
            fixed (float* pDash = dash)
            {
                ThrowIfFailed(NativeMethods.pdf_builder_add_arc(
                    _state, pageIndex, cx, cy, radius,
                    startRadians, endRadians,
                    style.Width, style.Color.R, style.Color.G, style.Color.B,
                    pDash, dash.Length, style.DashPhase));
            }
        }

        internal void AddBezier(
            int pageIndex,
            float x0,
            float y0,
            float c1x,
            float c1y,
            float c2x,
            float c2y,
            float x3,
            float y3,
            PdfLineStyle? stroke,
            PdfColor? fill)
        {
            CheckDisposed();
            stroke ??= new PdfLineStyle();
            ValidateLineStyle(stroke);

            float[] dash = stroke.DashPattern ?? [];
            fixed (float* pDash = dash)
            {
                ThrowIfFailed(NativeMethods.pdf_builder_add_bezier(
                    _state, pageIndex,
                    x0, y0, c1x, c1y, c2x, c2y, x3, y3,
                    1,
                    stroke.Width, stroke.Color.R, stroke.Color.G, stroke.Color.B,
                    pDash, dash.Length, stroke.DashPhase,
                    fill.HasValue ? 1 : 0,
                    fill?.R ?? 0f,
                    fill?.G ?? 0f,
                    fill?.B ?? 0f));
            }
        }

        internal void AddPath(int pageIndex, PdfGraphicsPath path, PdfPathStyle? style)
        {
            CheckDisposed();
            style ??= new PdfPathStyle();

            int[] kinds = path.GetKinds();
            float[] coords = path.GetCoordinates();
            bool hasStroke = style.StrokeColor.HasValue;
            if (hasStroke && (!float.IsFinite(style.StrokeWidth) || style.StrokeWidth <= 0f))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(style), style.StrokeWidth, "Stroke width must be a positive finite number.");
            }

            fixed (int* pKinds = kinds)
            fixed (float* pCoords = coords)
            {
                ThrowIfFailed(NativeMethods.pdf_builder_add_path(
                    _state, pageIndex, pKinds, pCoords, kinds.Length,
                    hasStroke ? 1 : 0,
                    style.StrokeWidth,
                    style.StrokeColor?.R ?? 0f,
                    style.StrokeColor?.G ?? 0f,
                    style.StrokeColor?.B ?? 0f,
                    null, 0, 0f,
                    style.FillColor.HasValue ? 1 : 0,
                    style.FillColor?.R ?? 0f,
                    style.FillColor?.G ?? 0f,
                    style.FillColor?.B ?? 0f));
            }
        }

        private static void ValidatePadding(float value, string parameterName)
        {
            if (!float.IsFinite(value) || value < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName, value, "Cell padding must be a non-negative finite number.");
            }
        }

        private static void ValidateColor(PdfColor color, string parameterName)
        {
            if (!float.IsFinite(color.R) || !float.IsFinite(color.G) || !float.IsFinite(color.B))
            {
                throw new ArgumentOutOfRangeException(
                    parameterName, color, "Color components must be finite numbers.");
            }
        }

        private static void ValidateLineStyle(PdfLineStyle style)
        {
            if (!float.IsFinite(style.Width) || style.Width <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(style), style.Width, "Line width must be a positive finite number.");
            }
            if (!float.IsFinite(style.DashPhase))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(style), style.DashPhase, "Dash phase must be finite.");
            }
            if (style.DashPattern is { } dash)
            {
                foreach (float value in dash)
                {
                    if (!float.IsFinite(value) || value < 0f)
                    {
                        throw new ArgumentOutOfRangeException(
                            nameof(style), value, "Dash pattern values must be non-negative finite numbers.");
                    }
                }
            }
        }

        private PdfPageBuilder AddPageCore(int kind, float width, float height)
        {
            ThrowIfFailed(NativeMethods.pdf_builder_add_page(_state, kind, width, height));
            _pageCount++;
            return new PdfPageBuilder(this, _pageCount - 1);
        }

        private void CheckDisposed()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
        }

        private static void ThrowIfFailed(int result)
        {
            if (result != 0)
            {
                throw new PdfOxideException(GetLastError());
            }
        }

        private static unsafe string GetLastError()
        {
            byte* ptr = NativeMethods.pdf_last_error_message();
            if (ptr == null)
            {
                return "Unknown error";
            }

            try
            {
                int length = NativeMethods.pdf_get_string_len(ptr);
                return length <= 0 ? "Unknown error" : Encoding.UTF8.GetString(ptr, length);
            }
            finally
            {
                NativeMethods.pdf_string_free(ptr);
            }
        }

        private static byte[] Utf8(string value)
        {
            return Encoding.UTF8.GetBytes(value + "\0");
        }

        private static byte[]? Utf8OrNull(string? value)
        {
            return value is null ? null : Utf8(value);
        }

        /// <summary>
        /// Releases the native builder state.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            if (_state != null)
            {
                NativeMethods.pdf_builder_free(_state);
                _state = null;
            }

            _disposed = true;
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Finalizer that releases the native builder state.
        /// </summary>
        ~DocumentBuilder()
        {
            Dispose();
        }
    }
}
