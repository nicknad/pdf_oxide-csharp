using System;
using System.Runtime.InteropServices;
using System.Text;

namespace PdfOxide
{
    /// <summary>
    /// Provides methods for converting various formats to PDF.
    /// </summary>
    /// <remarks>
    /// All methods in this class call into the native pdf_oxide library via FFI.
    /// String parameters are converted to UTF-8 before being passed to native code.
    /// Error messages from failed operations can be retrieved using GetLastError().
    /// </remarks>
    public static class PdfConverter
    {
        /// <summary>
        /// Creates a PDF from Markdown content.
        /// </summary>
        /// <param name="markdown">The Markdown content to convert.</param>
        /// <param name="outputPath">The path where the PDF will be saved.</param>
        /// <exception cref="ArgumentNullException">Thrown when markdown or outputPath is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the conversion fails.</exception>
        public static void ConvertMarkdown(string markdown, string outputPath)
        {
            ArgumentNullException.ThrowIfNull(markdown);
            ArgumentNullException.ThrowIfNull(outputPath);

            unsafe
            {
                // Convert strings to UTF-8 byte arrays with null terminator
                fixed (byte* pMd = GetUtf8Bytes(markdown))
                fixed (byte* pOut = GetUtf8Bytes(outputPath))
                {
                    if (NativeMethods.pdf_from_markdown(pMd, pOut) != 0)
                    {
                        throw new PdfOxideException(GetLastError());
                    }
                }
            }
        }

        /// <summary>
        /// Creates a PDF from plain text content.
        /// </summary>
        /// <param name="text">The text content to convert.</param>
        /// <param name="outputPath">The path where the PDF will be saved.</param>
        /// <exception cref="ArgumentNullException">Thrown when text or outputPath is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the conversion fails.</exception>
        public static void ConvertText(string text, string outputPath)
        {
            ArgumentNullException.ThrowIfNull(text);
            ArgumentNullException.ThrowIfNull(outputPath);

            unsafe
            {
                fixed (byte* pText = GetUtf8Bytes(text))
                fixed (byte* pOut = GetUtf8Bytes(outputPath))
                {
                    if (NativeMethods.pdf_from_text(pText, pOut) != 0)
                    {
                        throw new PdfOxideException(GetLastError());
                    }
                }
            }
        }

        /// <summary>
        /// Creates a multi-page PDF from an array of image files.
        /// Each image becomes a separate page in the PDF.
        /// </summary>
        /// <param name="imagePaths">Array of paths to image files.</param>
        /// <param name="outputPath">The path where the PDF will be saved.</param>
        /// <exception cref="ArgumentNullException">Thrown when imagePaths or outputPath is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the conversion fails.</exception>
        public static void ConvertImages(string[] imagePaths, string outputPath)
        {
            ArgumentNullException.ThrowIfNull(imagePaths);
            ArgumentNullException.ThrowIfNull(outputPath);

            unsafe
            {
                int len = imagePaths.Length;
                byte*[] ptrs = new byte*[len];
                GCHandle[] handles = new GCHandle[len];

                try
                {
                    // Pin each image path string and get pointer
                    for (int i = 0; i < len; i++)
                    {
                        byte[] bytes = GetUtf8Bytes(imagePaths[i]);
                        handles[i] = GCHandle.Alloc(bytes, GCHandleType.Pinned);
                        ptrs[i] = (byte*)handles[i].AddrOfPinnedObject();
                    }

                    // Call native function with array of pointers
                    fixed (byte** pPaths = ptrs)
                    fixed (byte* pOut = GetUtf8Bytes(outputPath))
                    {
                        if (NativeMethods.pdf_images_to_pdf(pPaths, len, pOut) != 0)
                        {
                            throw new PdfOxideException(GetLastError());
                        }
                    }
                }
                finally
                {
                    // Free all pinned handles to prevent GC issues
                    for (int i = 0; i < len; i++)
                    {
                        if (handles[i].IsAllocated) handles[i].Free();
                    }
                }
            }
        }

        /// <summary>
        /// Retrieves the last error message from a failed native operation.
        /// </summary>
        /// <returns>The error message string, or "Unknown error" if no message available.</returns>
        /// <remarks>
        /// The returned string is allocated by native code and must be freed
        /// by calling pdf_string_free. This method handles that automatically.
        /// </remarks>
        private static unsafe string GetLastError()
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
                // Always free the native string to prevent memory leaks
                NativeMethods.pdf_string_free(ptr);
            }
        }

        /// <summary>
        /// Converts a .NET string to a UTF-8 encoded byte array with null terminator.
        /// </summary>
        /// <param name="s">The string to convert.</param>
        /// <returns>UTF-8 byte array with trailing null byte.</returns>
        private static byte[] GetUtf8Bytes(string s)
        {
            return Encoding.UTF8.GetBytes(s + "\0");
        }
    }
}