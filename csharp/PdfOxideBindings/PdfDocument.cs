using System;
using System.Runtime.InteropServices;
using System.Text;

namespace PdfOxide
{
    /// <summary>
    /// Represents a PDF document for reading and extracting content.
    /// </summary>
    /// <remarks>
    /// This class provides methods to read existing PDF files and extract
    /// text content or convert pages to Markdown format. The document is
    /// opened in read-only mode - use PdfConverter for creating new PDFs.
    /// </remarks>
    public class PdfDocument
    {
        /// <summary>
        /// Path to the PDF file.
        /// </summary>
        private readonly string _path;

        /// <summary>
        /// Opens a PDF file for reading.
        /// </summary>
        /// <param name="path">Path to the PDF file.</param>
        /// <exception cref="ArgumentNullException">Thrown when path is null.</exception>
        public PdfDocument(string path)
        {
            _path = path ?? throw new ArgumentNullException(nameof(path));
        }

        /// <summary>
        /// Gets the number of pages in the PDF document.
        /// </summary>
        /// <returns>The page count.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the operation fails.</exception>
        public int GetPageCount()
        {
            unsafe
            {
                fixed (byte* pPath = GetUtf8Bytes(_path))
                {
                    int count = NativeMethods.pdf_get_page_count(pPath);
                    if (count < 0)
                    {
                        throw new InvalidOperationException(GetLastError());
                    }
                    return count;
                }
            }
        }

        /// <summary>
        /// Extracts all text content from the PDF document.
        /// </summary>
        /// <returns>A string containing the extracted text from all pages.</returns>
        /// <exception cref="InvalidOperationException">Thrown when extraction fails.</exception>
        /// <remarks>
        /// Text is extracted from all pages and concatenated with newlines
        /// between pages.
        /// </remarks>
        public string ExtractText()
        {
            unsafe
            {
                fixed (byte* pPath = GetUtf8Bytes(_path))
                {
                    byte* result = NativeMethods.pdf_extract_text(pPath);
                    if (result == null)
                    {
                        throw new InvalidOperationException(GetLastError());
                    }
                    return PtrToStringAndFree(result);
                }
            }
        }

        /// <summary>
        /// Converts all pages in the PDF to Markdown format.
        /// </summary>
        /// <returns>A string containing Markdown content for all pages.</returns>
        /// <exception cref="InvalidOperationException">Thrown when conversion fails.</exception>
        public string ToMarkdown()
        {
            unsafe
            {
                fixed (byte* pPath = GetUtf8Bytes(_path))
                {
                    byte* result = NativeMethods.pdf_to_markdown_all(pPath);
                    if (result == null)
                    {
                        throw new InvalidOperationException(GetLastError());
                    }
                    return PtrToStringAndFree(result);
                }
            }
        }

        /// <summary>
        /// Converts a specific page to Markdown format.
        /// </summary>
        /// <param name="pageIndex">Zero-based index of the page to convert.</param>
        /// <returns>A string containing Markdown content for the specified page.</returns>
        /// <exception cref="InvalidOperationException">Thrown when conversion fails.</exception>
        public string ToMarkdown(int pageIndex)
        {
            unsafe
            {
                fixed (byte* pPath = GetUtf8Bytes(_path))
                {
                    byte* result = NativeMethods.pdf_to_markdown(pPath, pageIndex);
                    if (result == null)
                    {
                        throw new InvalidOperationException(GetLastError());
                    }
                    return PtrToStringAndFree(result);
                }
            }
        }

        /// <summary>
        /// Converts a native pointer to a UTF-8 string and frees the native memory.
        /// </summary>
        /// <param name="ptr">Pointer to native string allocated by the FFI.</param>
        /// <returns>The converted .NET string.</returns>
        private static unsafe string PtrToStringAndFree(byte* ptr)
        {
            if (ptr == null) return string.Empty;

            try
            {
                return Utf8PtrToString(ptr);
            }
            finally
            {
                // Always free the native string to prevent memory leaks
                NativeMethods.pdf_string_free(ptr);
            }
        }

        /// <summary>
        /// Retrieves the last error message from the native library.
        /// </summary>
        private static unsafe string GetLastError()
        {
            byte* ptr = NativeMethods.pdf_last_error_message();
            if (ptr == null) return "Unknown error";
            
            try
            {
                return Utf8PtrToString(ptr);
            }
            finally
            {
                NativeMethods.pdf_string_free(ptr);
            }
        }

        /// <summary>
        /// Converts a native UTF-8 pointer to a .NET string.
        /// </summary>
        private static unsafe string Utf8PtrToString(byte* ptr)
        {
            if (ptr == null) return string.Empty;
            int length = NativeMethods.pdf_get_string_len(ptr);
            if (length <= 0) return string.Empty;

            return Encoding.UTF8.GetString(ptr, length);
        }

        /// <summary>
        /// Converts a string to UTF-8 bytes with null terminator.
        /// </summary>
        private static byte[] GetUtf8Bytes(string s)
        {
            return Encoding.UTF8.GetBytes(s + "\0");
        }
    }
}