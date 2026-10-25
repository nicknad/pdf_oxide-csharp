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
    /// <see cref="ToBytes"/>. Content is buffered in native code and rendered
    /// when the document is saved, so the builder remains usable afterwards.
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

        internal void EnsureActivePage(int pageIndex)
        {
            CheckDisposed();
            if (pageIndex != _pageCount - 1)
            {
                throw new PdfOxideException(
                    "Only the most recently added page can receive content. Finish each page before adding the next one.");
            }
        }

        internal void AddText(int pageIndex, string text, PdfTextStyle? style)
        {
            EnsureActivePage(pageIndex);
            byte[]? font = style is null ? null : Utf8(style.Font);

            fixed (byte* pText = Utf8(text))
            fixed (byte* pFont = font)
            {
                ThrowIfFailed(NativeMethods.pdf_builder_add_text(
                    _state, pText, pFont, style?.Size ?? 0f));
            }
        }

        internal void AddTextAt(int pageIndex, float x, float y, string text, PdfTextStyle? style)
        {
            EnsureActivePage(pageIndex);
            byte[]? font = style is null ? null : Utf8(style.Font);

            fixed (byte* pText = Utf8(text))
            fixed (byte* pFont = font)
            {
                ThrowIfFailed(NativeMethods.pdf_builder_add_text_at(
                    _state, x, y, pText, pFont, style?.Size ?? 0f));
            }
        }

        internal void AddHeading(int pageIndex, int level, string text)
        {
            EnsureActivePage(pageIndex);

            fixed (byte* pText = Utf8(text))
            {
                ThrowIfFailed(NativeMethods.pdf_builder_add_heading(_state, level, pText));
            }
        }

        internal void AddParagraph(int pageIndex, string text)
        {
            EnsureActivePage(pageIndex);

            fixed (byte* pText = Utf8(text))
            {
                ThrowIfFailed(NativeMethods.pdf_builder_add_paragraph(_state, pText));
            }
        }

        internal void AddList(int pageIndex, string[] items, bool numbered, PdfListStyle style)
        {
            EnsureActivePage(pageIndex);

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
                    _state, pItems, numbered ? 1 : 0, (int)style));
            }
        }

        internal void AddCodeBlock(int pageIndex, string language, string source)
        {
            EnsureActivePage(pageIndex);

            fixed (byte* pLanguage = Utf8(language))
            fixed (byte* pSource = Utf8(source))
            {
                ThrowIfFailed(NativeMethods.pdf_builder_add_code_block(_state, pLanguage, pSource));
            }
        }

        internal void AddRichParagraph(int pageIndex, PdfTextRun[] runs)
        {
            EnsureActivePage(pageIndex);
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
                        _state, pTexts, pStyles, pColors, runs.Length));
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
            EnsureActivePage(pageIndex);
            ThrowIfFailed(NativeMethods.pdf_builder_add_space(_state, points));
        }

        internal void AddHorizontalRule(int pageIndex)
        {
            EnsureActivePage(pageIndex);
            ThrowIfFailed(NativeMethods.pdf_builder_add_horizontal_rule(_state));
        }

        internal void AddColumns(int pageIndex, int count, float gap, string text)
        {
            EnsureActivePage(pageIndex);

            fixed (byte* pText = Utf8(text))
            {
                ThrowIfFailed(NativeMethods.pdf_builder_add_columns(_state, count, gap, pText));
            }
        }

        internal void AddTable(int pageIndex, PdfTable table)
        {
            EnsureActivePage(pageIndex);

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
            EnsureActivePage(pageIndex);
            byte[]? alt = Utf8OrNull(altText);

            fixed (byte* pPath = Utf8(path))
            fixed (byte* pAlt = alt)
            {
                ThrowIfFailed(NativeMethods.pdf_builder_add_image_file(
                    _state, pPath, rect.X, rect.Y, rect.Width, rect.Height, pAlt));
            }
        }

        internal void AddImage(int pageIndex, byte[] imageData, PdfRect rect, string? altText)
        {
            EnsureActivePage(pageIndex);
            byte[]? alt = Utf8OrNull(altText);

            fixed (byte* pData = imageData)
            fixed (byte* pAlt = alt)
            {
                ThrowIfFailed(NativeMethods.pdf_builder_add_image_bytes(
                    _state, pData, imageData.Length, rect.X, rect.Y, rect.Width, rect.Height, pAlt));
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
