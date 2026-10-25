using System;
using System.Runtime.InteropServices;
using System.Text;

namespace PdfOxide
{
    /// <summary>
    /// Provides a programmatic way to build PDF documents by adding pages.
    /// </summary>
    /// <remarks>
    /// This class wraps the native DocumentBuilder API for creating PDFs with
    /// custom content. Each call to AddPage adds a new page to the document.
    /// The Save method writes the document to disk and consumes the builder.
    /// </remarks>
    public sealed unsafe class DocumentBuilder : IDisposable
    {
        /// <summary>
        /// Pointer to the native builder state.
        /// </summary>
        /// <remarks>
        /// This is an opaque pointer to the Rust-side PdfBuilderState struct.
        /// </remarks>
        private PdfBuilderState* _state;
        
        /// <summary>
        /// Tracks whether the builder has been disposed.
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// Creates a new DocumentBuilder instance.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the native builder cannot be created.</exception>
        public DocumentBuilder()
        {
            _state = NativeMethods.pdf_builder_create();
            if (_state == null)
            {
                throw new InvalidOperationException("Failed to create DocumentBuilder");
            }
        }

        /// <summary>
        /// Adds a new page with the specified text content.
        /// </summary>
        /// <param name="text">The text content for the new page.</param>
        /// <returns>The current DocumentBuilder instance for method chaining.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the builder has been disposed.</exception>
        /// <exception cref="ArgumentNullException">Thrown when text is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when adding the page fails.</exception>
        public DocumentBuilder AddPage(string text)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ArgumentNullException.ThrowIfNull(text);

            unsafe
            {
                fixed (byte* pText = GetUtf8Bytes(text))
                {
                    if (NativeMethods.pdf_builder_add_sample_page(_state, pText) != 0)
                    {
                        throw new InvalidOperationException(GetLastError());
                    }
                }
            }
            return this;
        }

        /// <summary>
        /// Saves the document to the specified path and releases resources.
        /// </summary>
        /// <param name="outputPath">The file path where the PDF will be saved.</param>
        /// <exception cref="ObjectDisposedException">Thrown if the builder has been disposed.</exception>
        /// <exception cref="ArgumentNullException">Thrown when outputPath is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when saving fails.</exception>
        /// <remarks>
        /// After calling Save, the DocumentBuilder is consumed and should not be used.
        /// This method will dispose of the builder automatically.
        /// </remarks>
        public void Save(string outputPath)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ArgumentNullException.ThrowIfNull(outputPath);

            unsafe
            {
                fixed (byte* pOut = GetUtf8Bytes(outputPath))
                {
                    int result = NativeMethods.pdf_builder_save(_state, pOut);

                    // The native function consumes the state on every call,
                    // including when saving fails.
                    _state = null;
                    _disposed = true;

                    if (result != 0)
                    {
                        throw new InvalidOperationException(GetLastError());
                    }
                }
            }
        }

        /// <summary>
        /// Retrieves the last error message from the native library.
        /// </summary>
        private static string GetLastError()
        {
            byte* ptr = NativeMethods.pdf_last_error_message();
            if (ptr == null) return "Unknown error";
            
            try
            {
                int length = NativeMethods.pdf_get_string_len(ptr);
                if (length <= 0) return "Unknown error";
                return Encoding.UTF8.GetString(ptr, length);
            }
            finally
            {
                // Free the native string to prevent memory leaks
                NativeMethods.pdf_string_free(ptr);
            }
        }

        /// <summary>
        /// Converts a string to UTF-8 bytes with null terminator.
        /// </summary>
        private static byte[] GetUtf8Bytes(string s)
        {
            return Encoding.UTF8.GetBytes(s + "\0");
        }

        /// <summary>
        /// Releases the native builder resources.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                if (_state != null)
                {
                    // Free the native builder state
                    NativeMethods.pdf_builder_free(_state);
                    _state = null;
                }
                _disposed = true;
                // Prevent finalizer from running since we've cleaned up
                GC.SuppressFinalize(this);
            }
        }

        /// <summary>
        /// Finalizer to ensure native resources are freed.
        /// </summary>
        ~DocumentBuilder()
        {
            Dispose();
        }
    }
}
