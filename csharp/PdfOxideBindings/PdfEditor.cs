using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace PdfOxide
{
    /// <summary>
    /// Edits an existing PDF document, including page merging, extraction,
    /// deletion, reordering, rotation, and media/crop box changes.
    /// </summary>
    /// <remarks>
    /// Instances hold a native editor handle and must be disposed when they are
    /// no longer needed. All operations on a single instance are serialized.
    /// Page indices are zero-based. Changes are kept in memory until
    /// <see cref="Save(string)"/> or <see cref="ToBytes"/> is called.
    /// </remarks>
    public sealed unsafe class PdfEditor : IDisposable
    {
        private readonly object _sync = new();
        private readonly string? _sourcePath;
        private readonly List<int> _pendingFlattenAnnotationPages = new();
        private bool _flattenAllAnnotationsPending;
        private bool _flattenAnnotationsPending;
        private IntPtr _handle;
        private bool _disposed;

        private PdfEditor(IntPtr handle, string? sourcePath = null)
        {
            _handle = handle;
            _sourcePath = sourcePath;
        }

        /// <summary>
        /// Opens an existing PDF file for editing.
        /// </summary>
        /// <param name="path">Path to the PDF file.</param>
        /// <returns>A new editor for the document.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is null or empty.</exception>
        /// <exception cref="PdfOxideException">Thrown when the document cannot be opened.</exception>
        public static PdfEditor Open(string path)
        {
            ArgumentException.ThrowIfNullOrEmpty(path);

            int errorCode = 0;
            IntPtr handle;
            fixed (byte* pPath = Utf8(path))
            {
                handle = UpstreamNativeMethods.document_editor_open(pPath, &errorCode);
            }

            if (handle == IntPtr.Zero)
            {
                throw new PdfOxideException(
                    $"Native document_editor_open failed (error code {errorCode}).");
            }

            return new PdfEditor(handle, path);
        }

        /// <summary>
        /// Opens an existing PDF file for editing, optionally supplying a password.
        /// </summary>
        /// <param name="path">Path to the PDF file.</param>
        /// <param name="password">The password used to decrypt the document, or <see langword="null"/> / empty for an unencrypted document.</param>
        /// <returns>A new editor for the document.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is null or empty.</exception>
        /// <exception cref="PdfOxideException">Thrown when the document cannot be opened or the password is incorrect.</exception>
        public static PdfEditor Open(string path, string? password)
        {
            ArgumentException.ThrowIfNullOrEmpty(path);
            if (string.IsNullOrEmpty(password))
            {
                return Open(path);
            }

            int errorCode = 0;
            IntPtr handle;
            fixed (byte* pPath = Utf8(path))
            fixed (byte* pPassword = Utf8(password))
            {
                handle = UpstreamNativeMethods.pdfoxide_editor_open_with_password(
                    pPath,
                    pPassword,
                    &errorCode);
            }

            if (handle == IntPtr.Zero)
            {
                throw new PdfOxideException(
                    $"Native pdfoxide_editor_open_with_password failed (error code {errorCode}).");
            }

            return new PdfEditor(handle, path);
        }

        /// <summary>
        /// Opens a PDF from an in-memory byte array for editing.
        /// </summary>
        /// <param name="data">The PDF bytes.</param>
        /// <returns>A new editor for the document.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="data"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="data"/> is empty.</exception>
        /// <exception cref="PdfOxideException">Thrown when the document cannot be opened.</exception>
        public static PdfEditor FromBytes(byte[] data)
        {
            ArgumentNullException.ThrowIfNull(data);
            if (data.Length == 0)
            {
                throw new ArgumentException("PDF data must not be empty.", nameof(data));
            }

            int errorCode = 0;
            IntPtr handle;
            fixed (byte* pData = data)
            {
                handle = UpstreamNativeMethods.document_editor_open_from_bytes(
                    pData,
                    (nuint)data.Length,
                    &errorCode);
            }

            if (handle == IntPtr.Zero)
            {
                throw new PdfOxideException(
                    $"Native document_editor_open_from_bytes failed (error code {errorCode}).");
            }

            return new PdfEditor(handle);
        }

        /// <summary>
        /// Opens a PDF from an in-memory byte array for editing, optionally supplying a password.
        /// </summary>
        /// <param name="data">The PDF bytes.</param>
        /// <param name="password">The password used to decrypt the document, or <see langword="null"/> / empty for an unencrypted document.</param>
        /// <returns>A new editor for the document.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="data"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="data"/> is empty.</exception>
        /// <exception cref="PdfOxideException">Thrown when the document cannot be opened or the password is incorrect.</exception>
        public static PdfEditor FromBytes(byte[] data, string? password)
        {
            ArgumentNullException.ThrowIfNull(data);
            if (data.Length == 0)
            {
                throw new ArgumentException("PDF data must not be empty.", nameof(data));
            }

            if (string.IsNullOrEmpty(password))
            {
                return FromBytes(data);
            }

            int errorCode = 0;
            IntPtr handle;
            fixed (byte* pData = data)
            fixed (byte* pPassword = Utf8(password))
            {
                handle = UpstreamNativeMethods.pdfoxide_editor_open_from_bytes_with_password(
                    pData,
                    (nuint)data.Length,
                    pPassword,
                    &errorCode);
            }

            if (handle == IntPtr.Zero)
            {
                throw new PdfOxideException(
                    $"Native pdfoxide_editor_open_from_bytes_with_password failed (error code {errorCode}).");
            }

            return new PdfEditor(handle);
        }

        /// <summary>
        /// Gets the current number of pages in the document.
        /// </summary>
        /// <returns>The page count.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when the editor has been disposed.</exception>
        /// <exception cref="PdfOxideException">Thrown when the native operation fails.</exception>
        public int GetPageCount()
        {
            lock (_sync)
            {
                ThrowIfDisposed();
                return GetPageCountCore();
            }
        }

        /// <summary>
        /// Gets a value indicating whether the document has pending modifications.
        /// </summary>
        /// <returns><see langword="true"/> when the document has been modified.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when the editor has been disposed.</exception>
        public bool IsModified()
        {
            lock (_sync)
            {
                ThrowIfDisposed();
                return UpstreamNativeMethods.document_editor_is_modified(_handle) != 0;
            }
        }

        /// <summary>
        /// Saves the edited document to a file.
        /// </summary>
        /// <param name="outputPath">The output file path.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="outputPath"/> is null or empty.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the editor has been disposed.</exception>
        /// <exception cref="PdfOxideException">Thrown when the native operation fails.</exception>
        public void Save(string outputPath)
        {
            ArgumentException.ThrowIfNullOrEmpty(outputPath);

            lock (_sync)
            {
                ThrowIfDisposed();
                int errorCode = 0;
                fixed (byte* pPath = Utf8(outputPath))
                {
                    int result = UpstreamNativeMethods.document_editor_save(_handle, pPath, &errorCode);
                    ThrowIfFailed(result, errorCode, "document_editor_save");
                }

                FinalizePendingAnnotationFlatten(outputPath);
            }
        }

        /// <summary>
        /// Saves the edited document to a byte array.
        /// </summary>
        /// <returns>The edited PDF bytes.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when the editor has been disposed.</exception>
        /// <exception cref="PdfOxideException">Thrown when the native operation fails.</exception>
        public byte[] ToBytes()
        {
            lock (_sync)
            {
                ThrowIfDisposed();
                int errorCode = 0;
                nuint outLength = 0;
                byte* data = UpstreamNativeMethods.document_editor_save_to_bytes(
                    _handle,
                    &outLength,
                    &errorCode);

                if (data == null)
                {
                    throw new PdfOxideException(
                        $"Native document_editor_save_to_bytes failed (error code {errorCode}).");
                }

                try
                {
                    byte[] bytes = CopyBytes(data, outLength);
                    return _flattenAnnotationsPending
                        ? FinalizePendingAnnotationFlatten(bytes)
                        : bytes;
                }
                finally
                {
                    UpstreamNativeMethods.free_bytes(data);
                }
            }
        }

        /// <summary>
        /// Saves the edited document with AES-256 encryption.
        /// </summary>
        /// <param name="outputPath">The output file path.</param>
        /// <param name="userPassword">The user password required to open the document.</param>
        /// <param name="ownerPassword">The owner password. When null or empty, <paramref name="userPassword"/> is used.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="outputPath"/> or <paramref name="userPassword"/> is null or empty.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the editor has been disposed.</exception>
        /// <exception cref="PdfOxideException">Thrown when the native operation fails.</exception>
        public void SaveEncrypted(string outputPath, string userPassword, string? ownerPassword = null)
        {
            ArgumentException.ThrowIfNullOrEmpty(outputPath);
            ArgumentException.ThrowIfNullOrEmpty(userPassword);
            ownerPassword = string.IsNullOrEmpty(ownerPassword) ? userPassword : ownerPassword;

            lock (_sync)
            {
                ThrowIfDisposed();
                int errorCode = 0;
                fixed (byte* pPath = Utf8(outputPath))
                fixed (byte* pUser = Utf8(userPassword))
                fixed (byte* pOwner = Utf8(ownerPassword))
                {
                    int result = UpstreamNativeMethods.document_editor_save_encrypted(
                        _handle,
                        pPath,
                        pUser,
                        pOwner,
                        &errorCode);
                    ThrowIfFailed(result, errorCode, "document_editor_save_encrypted");
                }
            }
        }

        /// <summary>
        /// Saves the edited document as an AES-256 encrypted byte array.
        /// </summary>
        /// <param name="userPassword">The user password required to open the document.</param>
        /// <param name="ownerPassword">The owner password. When null or empty, <paramref name="userPassword"/> is used.</param>
        /// <returns>The encrypted PDF bytes.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="userPassword"/> is null or empty.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the editor has been disposed.</exception>
        /// <exception cref="PdfOxideException">Thrown when the native operation fails.</exception>
        public byte[] ToBytesEncrypted(string userPassword, string? ownerPassword = null)
        {
            ArgumentException.ThrowIfNullOrEmpty(userPassword);
            ownerPassword = string.IsNullOrEmpty(ownerPassword) ? userPassword : ownerPassword;

            lock (_sync)
            {
                ThrowIfDisposed();
                int errorCode = 0;
                nuint outLength = 0;
                byte* data;
                fixed (byte* pUser = Utf8(userPassword))
                fixed (byte* pOwner = Utf8(ownerPassword))
                {
                    data = UpstreamNativeMethods.document_editor_save_encrypted_to_bytes(
                        _handle,
                        pUser,
                        pOwner,
                        &outLength,
                        &errorCode);
                }

                if (data == null)
                {
                    throw new PdfOxideException(
                        $"Native document_editor_save_encrypted_to_bytes failed (error code {errorCode}).");
                }

                try
                {
                    return CopyBytes(data, outLength);
                }
                finally
                {
                    UpstreamNativeMethods.free_bytes(data);
                }
            }
        }

        /// <summary>
        /// Queues a rectangular redaction on a page. Call <see cref="ApplyRedactions"/>
        /// to destructively apply queued redactions.
        /// </summary>
        /// <param name="pageIndex">Zero-based index of the page.</param>
        /// <param name="rect">The redaction rectangle in page coordinates.</param>
        /// <param name="fill">The overlay fill color. Defaults to black when null.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="pageIndex"/> is outside the document or <paramref name="rect"/> is invalid.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the editor has been disposed.</exception>
        /// <exception cref="PdfOxideException">Thrown when the native operation fails.</exception>
        public void AddRedaction(int pageIndex, PdfRect rect, PdfColor? fill = null)
        {
            ValidateRect(rect, nameof(rect));

            lock (_sync)
            {
                ThrowIfDisposed();
                EnsurePageIndex(pageIndex, nameof(pageIndex));

                PdfColor color = fill ?? new PdfColor(0f, 0f, 0f);
                int errorCode = 0;
                int result = UpstreamNativeMethods.pdf_redaction_add(
                    _handle,
                    (nuint)pageIndex,
                    rect.X,
                    rect.Y,
                    rect.X + rect.Width,
                    rect.Y + rect.Height,
                    color.R,
                    color.G,
                    color.B,
                    &errorCode);
                ThrowIfFailed(result, errorCode, "pdf_redaction_add");
            }
        }

        /// <summary>
        /// Gets the number of redaction regions queued for a page.
        /// </summary>
        /// <param name="pageIndex">Zero-based index of the page.</param>
        /// <returns>The number of queued redaction regions.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="pageIndex"/> is outside the document.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the editor has been disposed.</exception>
        /// <exception cref="PdfOxideException">Thrown when the native operation fails.</exception>
        public int GetRedactionCount(int pageIndex)
        {
            lock (_sync)
            {
                ThrowIfDisposed();
                EnsurePageIndex(pageIndex, nameof(pageIndex));

                int errorCode = 0;
                int count = UpstreamNativeMethods.pdf_redaction_count(
                    _handle,
                    (nuint)pageIndex,
                    &errorCode);
                ThrowIfFailed(count, errorCode, "pdf_redaction_count");
                return count;
            }
        }

        /// <summary>
        /// Marks a page for the upstream legacy redaction overlay pass.
        /// Use <see cref="ApplyRedactions"/> for destructive content removal of
        /// rectangles queued with <see cref="AddRedaction"/>.
        /// </summary>
        /// <param name="pageIndex">Zero-based index of the page.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="pageIndex"/> is outside the document.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the editor has been disposed.</exception>
        /// <exception cref="PdfOxideException">Thrown when the native operation fails.</exception>
        public void ApplyPageRedactions(int pageIndex)
        {
            lock (_sync)
            {
                ThrowIfDisposed();
                EnsurePageIndex(pageIndex, nameof(pageIndex));

                int errorCode = 0;
                int result = UpstreamNativeMethods.document_editor_apply_page_redactions(
                    _handle,
                    (nuint)pageIndex,
                    &errorCode);
                ThrowIfFailed(result, errorCode, "document_editor_apply_page_redactions");
            }
        }

        /// <summary>
        /// Destructively applies all queued redactions. Removed glyphs cannot be recovered.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Thrown when the editor has been disposed.</exception>
        /// <exception cref="PdfOxideException">Thrown when the native operation fails.</exception>
        public void ApplyRedactions()
        {
            lock (_sync)
            {
                ThrowIfDisposed();
                int errorCode = 0;
                int result = UpstreamNativeMethods.pdf_redaction_apply(
                    _handle,
                    0,
                    0d,
                    0d,
                    0d,
                    &errorCode);
                ThrowIfFailed(result, errorCode, "pdf_redaction_apply");
            }
        }

        /// <summary>
        /// Removes document metadata and other sensitive constructs.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Thrown when the editor has been disposed.</exception>
        /// <exception cref="PdfOxideException">Thrown when the native operation fails.</exception>
        public void ScrubMetadata()
        {
            lock (_sync)
            {
                ThrowIfDisposed();
                int errorCode = 0;
                int result = UpstreamNativeMethods.pdf_redaction_scrub_metadata(_handle, &errorCode);
                ThrowIfFailed(result, errorCode, "pdf_redaction_scrub_metadata");
            }
        }

        /// <summary>
        /// Deletes a page from the document.
        /// </summary>
        /// <param name="pageIndex">Zero-based index of the page to delete.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="pageIndex"/> is outside the document.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the editor has been disposed.</exception>
        /// <exception cref="PdfOxideException">Thrown when the native operation fails.</exception>
        public void DeletePage(int pageIndex)
        {
            lock (_sync)
            {
                ThrowIfDisposed();
                EnsurePageIndex(pageIndex, nameof(pageIndex));

                int errorCode = 0;
                int result = UpstreamNativeMethods.document_editor_delete_page(
                    _handle,
                    pageIndex,
                    &errorCode);
                ThrowIfFailed(result, errorCode, "document_editor_delete_page");
            }
        }

        /// <summary>
        /// Moves a page to a new position in the document.
        /// </summary>
        /// <param name="fromIndex">Zero-based index of the page to move.</param>
        /// <param name="toIndex">Zero-based destination index.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when either index is outside the document.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the editor has been disposed.</exception>
        /// <exception cref="PdfOxideException">Thrown when the native operation fails.</exception>
        public void MovePage(int fromIndex, int toIndex)
        {
            lock (_sync)
            {
                ThrowIfDisposed();
                EnsurePageIndex(fromIndex, nameof(fromIndex));
                EnsurePageIndex(toIndex, nameof(toIndex));

                int errorCode = 0;
                int result = UpstreamNativeMethods.document_editor_move_page(
                    _handle,
                    fromIndex,
                    toIndex,
                    &errorCode);
                ThrowIfFailed(result, errorCode, "document_editor_move_page");
            }
        }

        /// <summary>
        /// Gets the rotation of a page in degrees.
        /// </summary>
        /// <param name="pageIndex">Zero-based index of the page.</param>
        /// <returns>The page rotation in degrees.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="pageIndex"/> is outside the document.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the editor has been disposed.</exception>
        /// <exception cref="PdfOxideException">Thrown when the native operation fails.</exception>
        public int GetPageRotation(int pageIndex)
        {
            lock (_sync)
            {
                ThrowIfDisposed();
                EnsurePageIndex(pageIndex, nameof(pageIndex));

                int errorCode = 0;
                int rotation = UpstreamNativeMethods.document_editor_get_page_rotation(
                    _handle,
                    pageIndex,
                    &errorCode);
                ThrowIfFailed(rotation, errorCode, "document_editor_get_page_rotation");
                return rotation;
            }
        }

        /// <summary>
        /// Sets the absolute rotation of a page in degrees.
        /// </summary>
        /// <param name="pageIndex">Zero-based index of the page.</param>
        /// <param name="degrees">The absolute rotation in degrees.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="pageIndex"/> is outside the document.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the editor has been disposed.</exception>
        /// <exception cref="PdfOxideException">Thrown when the native operation fails.</exception>
        public void SetPageRotation(int pageIndex, int degrees)
        {
            lock (_sync)
            {
                ThrowIfDisposed();
                EnsurePageIndex(pageIndex, nameof(pageIndex));

                int errorCode = 0;
                int result = UpstreamNativeMethods.document_editor_set_page_rotation(
                    _handle,
                    pageIndex,
                    degrees,
                    &errorCode);
                ThrowIfFailed(result, errorCode, "document_editor_set_page_rotation");
            }
        }

        /// <summary>
        /// Rotates a page by adding the specified number of degrees.
        /// </summary>
        /// <param name="pageIndex">Zero-based index of the page.</param>
        /// <param name="degrees">The rotation to add, in degrees.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="pageIndex"/> is outside the document.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the editor has been disposed.</exception>
        /// <exception cref="PdfOxideException">Thrown when the native operation fails.</exception>
        public void RotatePage(int pageIndex, int degrees)
        {
            lock (_sync)
            {
                ThrowIfDisposed();
                EnsurePageIndex(pageIndex, nameof(pageIndex));

                int errorCode = 0;
                int result = UpstreamNativeMethods.document_editor_rotate_page_by(
                    _handle,
                    (nuint)pageIndex,
                    degrees,
                    &errorCode);
                ThrowIfFailed(result, errorCode, "document_editor_rotate_page_by");
            }
        }

        /// <summary>
        /// Rotates every page by adding the specified number of degrees.
        /// </summary>
        /// <param name="degrees">The rotation to add to every page, in degrees.</param>
        /// <exception cref="ObjectDisposedException">Thrown when the editor has been disposed.</exception>
        /// <exception cref="PdfOxideException">Thrown when the native operation fails.</exception>
        public void RotateAllPages(int degrees)
        {
            lock (_sync)
            {
                ThrowIfDisposed();
                int errorCode = 0;
                int result = UpstreamNativeMethods.document_editor_rotate_all_pages(
                    _handle,
                    degrees,
                    &errorCode);
                ThrowIfFailed(result, errorCode, "document_editor_rotate_all_pages");
            }
        }

        /// <summary>
        /// Gets the media box of a page.
        /// </summary>
        /// <param name="pageIndex">Zero-based index of the page.</param>
        /// <returns>The page media box.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="pageIndex"/> is outside the document.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the editor has been disposed.</exception>
        /// <exception cref="PdfOxideException">Thrown when the native operation fails.</exception>
        public PdfRect GetMediaBox(int pageIndex)
        {
            lock (_sync)
            {
                ThrowIfDisposed();
                EnsurePageIndex(pageIndex, nameof(pageIndex));

                return GetBoxCore(
                    pageIndex,
                    UpstreamNativeMethods.document_editor_get_page_media_box,
                    "document_editor_get_page_media_box") ?? default;
            }
        }

        /// <summary>
        /// Sets the media box of a page.
        /// </summary>
        /// <param name="pageIndex">Zero-based index of the page.</param>
        /// <param name="rect">The new media box.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="pageIndex"/> is outside the document or <paramref name="rect"/> is invalid.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the editor has been disposed.</exception>
        /// <exception cref="PdfOxideException">Thrown when the native operation fails.</exception>
        public void SetMediaBox(int pageIndex, PdfRect rect)
        {
            ValidateRect(rect, nameof(rect));

            lock (_sync)
            {
                ThrowIfDisposed();
                EnsurePageIndex(pageIndex, nameof(pageIndex));

                int errorCode = 0;
                int result = UpstreamNativeMethods.document_editor_set_page_media_box(
                    _handle,
                    (nuint)pageIndex,
                    rect.X,
                    rect.Y,
                    rect.X + rect.Width,
                    rect.Y + rect.Height,
                    &errorCode);
                ThrowIfFailed(result, errorCode, "document_editor_set_page_media_box");
            }
        }

        /// <summary>
        /// Gets the crop box of a page.
        /// </summary>
        /// <param name="pageIndex">Zero-based index of the page.</param>
        /// <returns>The crop box, or <see langword="null"/> when the page has no explicit crop box.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="pageIndex"/> is outside the document.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the editor has been disposed.</exception>
        /// <exception cref="PdfOxideException">Thrown when the native operation fails.</exception>
        public PdfRect? GetCropBox(int pageIndex)
        {
            lock (_sync)
            {
                ThrowIfDisposed();
                EnsurePageIndex(pageIndex, nameof(pageIndex));

                return GetBoxCore(
                    pageIndex,
                    UpstreamNativeMethods.document_editor_get_page_crop_box,
                    "document_editor_get_page_crop_box");
            }
        }

        /// <summary>
        /// Sets the crop box of a page.
        /// </summary>
        /// <param name="pageIndex">Zero-based index of the page.</param>
        /// <param name="rect">The new crop box.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="pageIndex"/> is outside the document or <paramref name="rect"/> is invalid.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the editor has been disposed.</exception>
        /// <exception cref="PdfOxideException">Thrown when the native operation fails.</exception>
        public void SetCropBox(int pageIndex, PdfRect rect)
        {
            ValidateRect(rect, nameof(rect));

            lock (_sync)
            {
                ThrowIfDisposed();
                EnsurePageIndex(pageIndex, nameof(pageIndex));

                int errorCode = 0;
                int result = UpstreamNativeMethods.document_editor_set_page_crop_box(
                    _handle,
                    (nuint)pageIndex,
                    rect.X,
                    rect.Y,
                    rect.X + rect.Width,
                    rect.Y + rect.Height,
                    &errorCode);
                ThrowIfFailed(result, errorCode, "document_editor_set_page_crop_box");
            }
        }

        /// <summary>
        /// Crops every page by removing the specified margins.
        /// </summary>
        /// <param name="left">Left margin in points.</param>
        /// <param name="right">Right margin in points.</param>
        /// <param name="top">Top margin in points.</param>
        /// <param name="bottom">Bottom margin in points.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a margin is negative or not finite.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the editor has been disposed.</exception>
        /// <exception cref="PdfOxideException">Thrown when the native operation fails.</exception>
        public void CropMargins(float left, float right, float top, float bottom)
        {
            ValidateMargin(left, nameof(left));
            ValidateMargin(right, nameof(right));
            ValidateMargin(top, nameof(top));
            ValidateMargin(bottom, nameof(bottom));

            lock (_sync)
            {
                ThrowIfDisposed();
                int errorCode = 0;
                int result = UpstreamNativeMethods.document_editor_crop_margins(
                    _handle,
                    left,
                    right,
                    top,
                    bottom,
                    &errorCode);
                ThrowIfFailed(result, errorCode, "document_editor_crop_margins");
            }
        }

        /// <summary>
        /// Extracts the specified pages into a new PDF.
        /// </summary>
        /// <param name="pageIndices">Zero-based indices of the pages to extract, in output order.</param>
        /// <returns>The extracted PDF bytes.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="pageIndices"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="pageIndices"/> is empty.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a page index is outside the document.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the editor has been disposed.</exception>
        /// <exception cref="PdfOxideException">Thrown when the native operation fails.</exception>
        public byte[] ExtractPages(params int[] pageIndices)
        {
            ArgumentNullException.ThrowIfNull(pageIndices);
            if (pageIndices.Length == 0)
            {
                throw new ArgumentException("At least one page index is required.", nameof(pageIndices));
            }

            lock (_sync)
            {
                ThrowIfDisposed();
                foreach (int pageIndex in pageIndices)
                {
                    EnsurePageIndex(pageIndex, nameof(pageIndices));
                }

                int errorCode = 0;
                nuint outLength = 0;
                byte* data;
                fixed (int* pPages = pageIndices)
                {
                    data = UpstreamNativeMethods.document_editor_extract_pages_to_bytes(
                        _handle,
                        pPages,
                        (nuint)pageIndices.Length,
                        &outLength,
                        &errorCode);
                }

                if (data == null)
                {
                    throw new PdfOxideException(
                        $"Native document_editor_extract_pages_to_bytes failed (error code {errorCode}).");
                }

                try
                {
                    return CopyBytes(data, outLength);
                }
                finally
                {
                    UpstreamNativeMethods.free_bytes(data);
                }
            }
        }

        /// <summary>
        /// Extracts the specified pages and writes them to a new PDF file.
        /// </summary>
        /// <param name="outputPath">The output file path.</param>
        /// <param name="pageIndices">Zero-based indices of the pages to extract, in output order.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="outputPath"/> is null or empty, or no page indices are supplied.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a page index is outside the document.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the editor has been disposed.</exception>
        /// <exception cref="PdfOxideException">Thrown when the native operation fails.</exception>
        public void SaveExtracted(string outputPath, params int[] pageIndices)
        {
            ArgumentException.ThrowIfNullOrEmpty(outputPath);
            byte[] bytes = ExtractPages(pageIndices);
            File.WriteAllBytes(outputPath, bytes);
        }

        /// <summary>
        /// Splits the document into one PDF file per page.
        /// </summary>
        /// <param name="outputDirectory">The directory that receives the split files.</param>
        /// <param name="fileNamePrefix">The prefix used for each output file name.</param>
        /// <remarks>
        /// Files are named <c>{fileNamePrefix}-{pageNumber}.pdf</c>, where
        /// <c>pageNumber</c> is one-based.
        /// </remarks>
        /// <exception cref="ArgumentException">Thrown when the directory or prefix is null or empty.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the editor has been disposed.</exception>
        /// <exception cref="PdfOxideException">Thrown when a native operation fails.</exception>
        public void Split(string outputDirectory, string fileNamePrefix)
        {
            ArgumentException.ThrowIfNullOrEmpty(outputDirectory);
            ArgumentException.ThrowIfNullOrEmpty(fileNamePrefix);

            Directory.CreateDirectory(outputDirectory);

            int pageCount;
            lock (_sync)
            {
                ThrowIfDisposed();
                pageCount = GetPageCountCore();
            }

            for (int pageIndex = 0; pageIndex < pageCount; pageIndex++)
            {
                byte[] bytes = ExtractPages(pageIndex);
                string fileName = $"{fileNamePrefix}-{pageIndex + 1}.pdf";
                File.WriteAllBytes(Path.Combine(outputDirectory, fileName), bytes);
            }
        }

        /// <summary>
        /// Merges all pages from another PDF file into this document.
        /// </summary>
        /// <param name="sourcePath">Path to the PDF file to merge.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="sourcePath"/> is null or empty.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the editor has been disposed.</exception>
        /// <exception cref="PdfOxideException">Thrown when the native operation fails.</exception>
        public void MergeFrom(string sourcePath)
        {
            ArgumentException.ThrowIfNullOrEmpty(sourcePath);

            lock (_sync)
            {
                ThrowIfDisposed();
                int errorCode = 0;
                fixed (byte* pPath = Utf8(sourcePath))
                {
                    int result = UpstreamNativeMethods.document_editor_merge_from(
                        _handle,
                        pPath,
                        &errorCode);
                    ThrowIfFailed(result, errorCode, "document_editor_merge_from");
                }
            }
        }

        /// <summary>
        /// Merges all pages from an in-memory PDF into this document.
        /// </summary>
        /// <param name="sourceData">The PDF bytes to merge.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="sourceData"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="sourceData"/> is empty.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the editor has been disposed.</exception>
        /// <exception cref="PdfOxideException">Thrown when the native operation fails.</exception>
        public void MergeFrom(byte[] sourceData)
        {
            ArgumentNullException.ThrowIfNull(sourceData);
            if (sourceData.Length == 0)
            {
                throw new ArgumentException("PDF data must not be empty.", nameof(sourceData));
            }

            lock (_sync)
            {
                ThrowIfDisposed();
                int errorCode = 0;
                fixed (byte* pData = sourceData)
                {
                    int result = UpstreamNativeMethods.document_editor_merge_from_bytes(
                        _handle,
                        pData,
                        (nuint)sourceData.Length,
                        &errorCode);
                    ThrowIfFailed(result, errorCode, "document_editor_merge_from_bytes");
                }
            }
        }

        /// <summary>
        /// Gets the form fields in the source document.
        /// </summary>
        /// <returns>The form fields in document order.</returns>
        /// <remarks>
        /// This reads the file-backed source document, so pending unsaved
        /// modifications are not reflected. The <see cref="PdfFormFieldInfo.Type"/>
        /// and <see cref="PdfFormFieldInfo.Value"/> strings are upstream debug
        /// representations and should be treated as opaque.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when the editor was created from bytes rather than a file.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the editor has been disposed.</exception>
        /// <exception cref="PdfOxideException">Thrown when the native operation fails.</exception>
        public IReadOnlyList<PdfFormFieldInfo> GetFormFields()
        {
            lock (_sync)
            {
                ThrowIfDisposed();
                string? sourcePath = _sourcePath;
                if (sourcePath is null)
                {
                    throw new InvalidOperationException(
                        "Form field enumeration requires a file-backed editor.");
                }

                int errorCode = 0;
                IntPtr document = IntPtr.Zero;
                IntPtr list = IntPtr.Zero;

                fixed (byte* pPath = Utf8(sourcePath))
                {
                    document = UpstreamNativeMethods.pdf_document_open(pPath, &errorCode);
                }

                if (document == IntPtr.Zero)
                {
                    throw new PdfOxideException(
                        $"Native pdf_document_open failed (error code {errorCode}).");
                }

                try
                {
                    list = UpstreamNativeMethods.pdf_document_get_form_fields(document, &errorCode);
                    if (list == IntPtr.Zero)
                    {
                        throw new PdfOxideException(
                            $"Native pdf_document_get_form_fields failed (error code {errorCode}).");
                    }

                    int count = UpstreamNativeMethods.pdf_oxide_form_field_count(list);
                    int safeCount = Math.Max(count, 0);
                    List<PdfFormFieldInfo> fields =
                    [
                        with(capacity: safeCount),
                        .. Enumerable.Range(0, safeCount).Select(index => ReadFormField(list, index)),
                    ];

                    return fields;
                }
                finally
                {
                    if (list != IntPtr.Zero)
                    {
                        UpstreamNativeMethods.pdf_oxide_form_field_list_free(list);
                    }
                    if (document != IntPtr.Zero)
                    {
                        UpstreamNativeMethods.pdf_document_free(document);
                    }
                }
            }
        }

        /// <summary>
        /// Sets the value of a form field.
        /// </summary>
        /// <param name="name">The field name.</param>
        /// <param name="value">The new textual value.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null or empty.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is null.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the editor has been disposed.</exception>
        /// <exception cref="PdfOxideException">Thrown when the native operation fails.</exception>
        public void SetFormFieldValue(string name, string value)
        {
            ArgumentException.ThrowIfNullOrEmpty(name);
            ArgumentNullException.ThrowIfNull(value);

            lock (_sync)
            {
                ThrowIfDisposed();
                int errorCode = 0;
                fixed (byte* pName = Utf8(name))
                fixed (byte* pValue = Utf8(value))
                {
                    int result = UpstreamNativeMethods.document_editor_set_form_field_value(
                        _handle, pName, pValue, &errorCode);
                    ThrowIfFailed(result, errorCode, "document_editor_set_form_field_value");
                }
            }
        }

        /// <summary>
        /// Flattens every form in the document into page content.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Thrown when the editor has been disposed.</exception>
        /// <exception cref="PdfOxideException">Thrown when the native operation fails.</exception>
        public void FlattenForms()
        {
            lock (_sync)
            {
                ThrowIfDisposed();
                int errorCode = 0;
                int result = UpstreamNativeMethods.document_editor_flatten_forms(_handle, &errorCode);
                ThrowIfFailed(result, errorCode, "document_editor_flatten_forms");
            }
        }

        /// <summary>
        /// Flattens the forms on a single page into page content.
        /// </summary>
        /// <param name="pageIndex">Zero-based index of the page.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="pageIndex"/> is outside the document.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the editor has been disposed.</exception>
        /// <exception cref="PdfOxideException">Thrown when the native operation fails.</exception>
        public void FlattenForms(int pageIndex)
        {
            lock (_sync)
            {
                ThrowIfDisposed();
                EnsurePageIndex(pageIndex, nameof(pageIndex));

                int errorCode = 0;
                int result = UpstreamNativeMethods.document_editor_flatten_forms_on_page(
                    _handle, pageIndex, &errorCode);
                ThrowIfFailed(result, errorCode, "document_editor_flatten_forms_on_page");
            }
        }

        /// <summary>
        /// Flattens every annotation in the document into page content.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Thrown when the editor has been disposed.</exception>
        /// <exception cref="PdfOxideException">Thrown when the native operation fails.</exception>
        public void FlattenAnnotations()
        {
            lock (_sync)
            {
                ThrowIfDisposed();
                int errorCode = 0;
                int result = UpstreamNativeMethods.document_editor_flatten_all_annotations(
                    _handle, &errorCode);
                ThrowIfFailed(result, errorCode, "document_editor_flatten_all_annotations");

                _flattenAllAnnotationsPending = true;
                _flattenAnnotationsPending = true;
            }
        }

        /// <summary>
        /// Flattens the annotations on a single page into page content.
        /// </summary>
        /// <param name="pageIndex">Zero-based index of the page.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="pageIndex"/> is outside the document.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the editor has been disposed.</exception>
        /// <exception cref="PdfOxideException">Thrown when the native operation fails.</exception>
        public void FlattenAnnotations(int pageIndex)
        {
            lock (_sync)
            {
                ThrowIfDisposed();
                EnsurePageIndex(pageIndex, nameof(pageIndex));

                int errorCode = 0;
                int result = UpstreamNativeMethods.document_editor_flatten_annotations(
                    _handle, pageIndex, &errorCode);
                ThrowIfFailed(result, errorCode, "document_editor_flatten_annotations");

                if (!_pendingFlattenAnnotationPages.Contains(pageIndex))
                {
                    _pendingFlattenAnnotationPages.Add(pageIndex);
                }
                _flattenAnnotationsPending = true;
            }
        }

        private void FinalizePendingAnnotationFlatten(string outputPath)
        {
            if (!_flattenAnnotationsPending)
            {
                return;
            }

            int pageCount = _flattenAllAnnotationsPending
                ? GetPageCountCore()
                : _pendingFlattenAnnotationPages.Count;

            List<int> pages = _flattenAllAnnotationsPending
                ? [with(capacity: pageCount), .. Enumerable.Range(0, pageCount)]
                : [with(capacity: pageCount), .. _pendingFlattenAnnotationPages];

            var document = new PdfDocument(outputPath);
            using (PdfEditor post = PdfEditor.Open(outputPath))
            {
                bool hasRemainingAnnotations = false;
                foreach (int page in pages)
                {
                    if (page < 0 || page >= document.GetPageCount())
                    {
                        continue;
                    }
                    if (document.GetAnnotations(page).Count == 0)
                    {
                        continue;
                    }

                    post.AddRedaction(page, new PdfRect(-10000f, -10000f, 1f, 1f));
                    hasRemainingAnnotations = true;
                }

                if (hasRemainingAnnotations)
                {
                    post.ApplyRedactions();
                    post.Save(outputPath);
                }
            }

            _flattenAnnotationsPending = false;
            _flattenAllAnnotationsPending = false;
            _pendingFlattenAnnotationPages.Clear();
        }

        private byte[] FinalizePendingAnnotationFlatten(byte[] bytes)
        {
            string temporaryPath = Path.Combine(
                Path.GetTempPath(),
                $"pdfoxide-flatten-{Guid.NewGuid():N}.pdf");
            try
            {
                File.WriteAllBytes(temporaryPath, bytes);
                FinalizePendingAnnotationFlatten(temporaryPath);
                return File.ReadAllBytes(temporaryPath);
            }
            finally
            {
                File.Delete(temporaryPath);
            }
        }

        /// <summary>
        /// Releases the native editor handle.
        /// </summary>
        public void Dispose()
        {
            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }

                if (_handle != IntPtr.Zero)
                {
                    UpstreamNativeMethods.document_editor_free(_handle);
                    _handle = IntPtr.Zero;
                }

                _disposed = true;
            }

            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Finalizer that releases the native editor handle.
        /// </summary>
        ~PdfEditor()
        {
            Dispose();
        }

        private delegate int BoxGetter(
            IntPtr handle,
            nuint page,
            double* x,
            double* y,
            double* w,
            double* h,
            int* errorCode);

        private PdfRect? GetBoxCore(int pageIndex, BoxGetter getter, string operation)
        {
            int errorCode = 0;
            double x;
            double y;
            double w;
            double h;
            int result = getter(_handle, (nuint)pageIndex, &x, &y, &w, &h, &errorCode);
            ThrowIfFailed(result, errorCode, operation);

            if (x == 0d && y == 0d && w == 0d && h == 0d)
            {
                return null;
            }

            return new PdfRect((float)x, (float)y, (float)(w - x), (float)(h - y));
        }

        private int GetPageCountCore()
        {
            int errorCode = 0;
            int count = UpstreamNativeMethods.document_editor_get_page_count(_handle, &errorCode);
            ThrowIfFailed(count, errorCode, "document_editor_get_page_count");
            return count;
        }

        private static unsafe PdfFormFieldInfo ReadFormField(IntPtr list, int index)
        {
            int errorCode = 0;
            return new PdfFormFieldInfo
            {
                Name = ReadFormString(list, index, FormStringField.Name) ?? string.Empty,
                Type = ReadFormString(list, index, FormStringField.Type) ?? string.Empty,
                Value = ReadFormString(list, index, FormStringField.Value) ?? string.Empty,
                IsReadOnly = UpstreamNativeMethods.pdf_oxide_form_field_is_readonly(
                    list, index, &errorCode) != 0,
                IsRequired = UpstreamNativeMethods.pdf_oxide_form_field_is_required(
                    list, index, &errorCode) != 0,
            };
        }

        private static unsafe string? ReadFormString(
            IntPtr list,
            int index,
            FormStringField field)
        {
            int errorCode = 0;
            byte* ptr = field switch
            {
                FormStringField.Name => UpstreamNativeMethods.pdf_oxide_form_field_get_name(
                    list, index, &errorCode),
                FormStringField.Type => UpstreamNativeMethods.pdf_oxide_form_field_get_type(
                    list, index, &errorCode),
                FormStringField.Value => UpstreamNativeMethods.pdf_oxide_form_field_get_value(
                    list, index, &errorCode),
                _ => null,
            };

            if (ptr == null)
            {
                return null;
            }

            try
            {
                int length = 0;
                while (ptr[length] != 0)
                {
                    length++;
                }
                return Encoding.UTF8.GetString(ptr, length);
            }
            finally
            {
                UpstreamNativeMethods.free_string(ptr);
            }
        }

        private enum FormStringField
        {
            Name,
            Type,
            Value,
        }

        private void EnsurePageIndex(int pageIndex, string paramName)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(pageIndex, paramName);
            if (pageIndex >= GetPageCountCore())
            {
                throw new ArgumentOutOfRangeException(
                    paramName,
                    pageIndex,
                    "Page index is outside the document.");
            }
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
        }

        private static void ThrowIfFailed(int result, int errorCode, string operation)
        {
            if (result < 0 || errorCode != 0)
            {
                throw new PdfOxideException($"Native {operation} failed (error code {errorCode}).");
            }
        }

        private static byte[] CopyBytes(byte* data, nuint length)
        {
            int intLength = checked((int)length);
            if (intLength == 0)
            {
                return [];
            }

            var result = new byte[intLength];
            Marshal.Copy((IntPtr)data, result, 0, intLength);
            return result;
        }

        private static void ValidateRect(PdfRect rect, string paramName)
        {
            if (!float.IsFinite(rect.X)
                || !float.IsFinite(rect.Y)
                || !float.IsFinite(rect.Width)
                || !float.IsFinite(rect.Height)
                || rect.Width <= 0f
                || rect.Height <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    paramName,
                    rect,
                    "Rectangle values must be finite and dimensions must be positive.");
            }
        }

        private static void ValidateMargin(float value, string paramName)
        {
            if (!float.IsFinite(value) || value < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    paramName,
                    value,
                    "Margins must be non-negative finite numbers.");
            }
        }

        private static byte[] Utf8(string value)
        {
            return Encoding.UTF8.GetBytes(value + "\0");
        }
    }
}
