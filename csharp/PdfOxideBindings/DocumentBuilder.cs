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
