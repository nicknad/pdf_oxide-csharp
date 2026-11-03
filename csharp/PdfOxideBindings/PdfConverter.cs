using System;
using System.IO;
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
        private const string DefaultFontResourceName = "PdfOxide.Fonts.DejaVuSans.ttf";

        private static readonly byte[] DefaultFontBytes = LoadDefaultFontBytes();

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
        /// Creates a PDF from HTML content.
        /// </summary>
        /// <param name="html">The HTML content to convert.</param>
        /// <param name="outputPath">The path where the PDF will be saved.</param>
        /// <exception cref="ArgumentException">Thrown when html or outputPath is null or empty.</exception>
        /// <exception cref="PdfOxideException">Thrown when the native conversion or save fails.</exception>
        public static void ConvertHtml(string html, string outputPath)
        {
            ArgumentException.ThrowIfNullOrEmpty(html);
            ArgumentException.ThrowIfNullOrEmpty(outputPath);

            IntPtr handle = CreateHtmlHandle(html, string.Empty);
            SaveHandleToFile(handle, outputPath, "HTML save");
        }

        /// <summary>
        /// Creates a PDF from HTML content with additional CSS.
        /// </summary>
        /// <param name="html">The HTML content to convert.</param>
        /// <param name="css">The CSS applied to the HTML. An empty or null value uses the no-CSS pipeline.</param>
        /// <param name="outputPath">The path where the PDF will be saved.</param>
        /// <exception cref="ArgumentException">Thrown when html or outputPath is null or empty.</exception>
        /// <exception cref="PdfOxideException">Thrown when the native conversion or save fails.</exception>
        public static void ConvertHtml(string html, string css, string outputPath)
        {
            ArgumentException.ThrowIfNullOrEmpty(html);
            ArgumentException.ThrowIfNullOrEmpty(outputPath);

            IntPtr handle = CreateHtmlHandle(html, css);
            SaveHandleToFile(handle, outputPath, "HTML save");
        }

        /// <summary>
        /// Creates a PDF from HTML content with optional CSS and returns it as a byte array.
        /// </summary>
        /// <param name="html">The HTML content to convert.</param>
        /// <param name="css">The CSS applied to the HTML. An empty value uses the no-CSS pipeline.</param>
        /// <returns>The generated PDF bytes.</returns>
        /// <exception cref="ArgumentException">Thrown when html is null or empty.</exception>
        /// <exception cref="PdfOxideException">Thrown when the native conversion fails.</exception>
        public static byte[] ConvertHtmlToBytes(string html, string css = "")
        {
            ArgumentException.ThrowIfNullOrEmpty(html);

            IntPtr handle = CreateHtmlHandle(html, css);
            return SaveHandleToBytes(handle, "HTML conversion");
        }

        /// <summary>
        /// Creates a single-page PDF from an image file.
        /// </summary>
        /// <param name="imagePath">The path to the image file.</param>
        /// <param name="outputPath">The path where the PDF will be saved.</param>
        /// <exception cref="ArgumentException">Thrown when imagePath or outputPath is null or empty.</exception>
        /// <exception cref="PdfOxideException">Thrown when the native conversion or save fails.</exception>
        public static void ConvertImage(string imagePath, string outputPath)
        {
            ArgumentException.ThrowIfNullOrEmpty(imagePath);
            ArgumentException.ThrowIfNullOrEmpty(outputPath);

            IntPtr handle = CreateImageHandle(imagePath);
            SaveHandleToFile(handle, outputPath, "image save");
        }

        /// <summary>
        /// Creates a single-page PDF from image data.
        /// </summary>
        /// <param name="imageData">The image file bytes.</param>
        /// <param name="outputPath">The path where the PDF will be saved.</param>
        /// <exception cref="ArgumentNullException">Thrown when imageData is null.</exception>
        /// <exception cref="ArgumentException">Thrown when imageData is empty or outputPath is null or empty.</exception>
        /// <exception cref="PdfOxideException">Thrown when the native conversion or save fails.</exception>
        public static void ConvertImage(byte[] imageData, string outputPath)
        {
            ArgumentNullException.ThrowIfNull(imageData);
            if (imageData.Length == 0)
            {
                throw new ArgumentException("Image data must not be empty.", nameof(imageData));
            }

            ArgumentException.ThrowIfNullOrEmpty(outputPath);

            IntPtr handle = CreateImageHandle(imageData);
            SaveHandleToFile(handle, outputPath, "image save");
        }

        /// <summary>
        /// Creates a single-page PDF from image data and returns it as a byte array.
        /// </summary>
        /// <param name="imageData">The image file bytes.</param>
        /// <returns>The generated PDF bytes.</returns>
        /// <exception cref="ArgumentNullException">Thrown when imageData is null.</exception>
        /// <exception cref="ArgumentException">Thrown when imageData is empty.</exception>
        /// <exception cref="PdfOxideException">Thrown when the native conversion fails.</exception>
        public static byte[] ConvertImageToBytes(byte[] imageData)
        {
            ArgumentNullException.ThrowIfNull(imageData);
            if (imageData.Length == 0)
            {
                throw new ArgumentException("Image data must not be empty.", nameof(imageData));
            }

            IntPtr handle = CreateImageHandle(imageData);
            return SaveHandleToBytes(handle, "image conversion");
        }

        /// <summary>
        /// Merges multiple PDF files into a single PDF file.
        /// </summary>
        /// <param name="inputPaths">The PDF files to merge, in order.</param>
        /// <param name="outputPath">The path where the merged PDF will be saved.</param>
        /// <exception cref="ArgumentNullException">Thrown when inputPaths is null.</exception>
        /// <exception cref="ArgumentException">Thrown when inputPaths is empty, contains a null or empty path, or outputPath is null or empty.</exception>
        /// <exception cref="PdfOxideException">Thrown when the native merge fails.</exception>
        public static void Merge(string[] inputPaths, string outputPath)
        {
            ArgumentNullException.ThrowIfNull(inputPaths);
            ArgumentException.ThrowIfNullOrEmpty(outputPath);

            byte[] bytes = MergeToBytes(inputPaths);
            File.WriteAllBytes(outputPath, bytes);
        }

        /// <summary>
        /// Merges multiple PDF files and returns the result as a byte array.
        /// </summary>
        /// <param name="inputPaths">The PDF files to merge, in order. At least one path is required.</param>
        /// <returns>The merged PDF bytes.</returns>
        /// <exception cref="ArgumentNullException">Thrown when inputPaths is null.</exception>
        /// <exception cref="ArgumentException">Thrown when inputPaths is empty or contains a null or empty path.</exception>
        /// <exception cref="PdfOxideException">Thrown when the native merge fails.</exception>
        public static byte[] MergeToBytes(string[] inputPaths)
        {
            ArgumentNullException.ThrowIfNull(inputPaths);
            if (inputPaths.Length == 0)
            {
                throw new ArgumentException("At least one input path is required.", nameof(inputPaths));
            }

            foreach (string path in inputPaths)
            {
                if (string.IsNullOrEmpty(path))
                {
                    throw new ArgumentException("Input paths must not be null or empty.", nameof(inputPaths));
                }
            }

            unsafe
            {
                int count = inputPaths.Length;
                byte*[] pointers = new byte*[count];
                GCHandle[] handles = new GCHandle[count];

                try
                {
                    // Pin each input path string and get pointer
                    for (int index = 0; index < count; index++)
                    {
                        byte[] encoded = GetUtf8Bytes(inputPaths[index]);
                        handles[index] = GCHandle.Alloc(encoded, GCHandleType.Pinned);
                        pointers[index] = (byte*)handles[index].AddrOfPinnedObject();
                    }

                    int dataLength = 0;
                    int errorCode = 0;
                    byte* data;
                    fixed (byte** pPaths = pointers)
                    {
                        data = UpstreamNativeMethods.pdf_merge(pPaths, count, &dataLength, &errorCode);
                    }

                    ThrowIfError(errorCode, "merge");
                    if (data == null)
                    {
                        throw new PdfOxideException("Native merge returned no data.");
                    }

                    try
                    {
                        var result = new byte[dataLength];
                        if (dataLength > 0)
                        {
                            Marshal.Copy((IntPtr)data, result, 0, dataLength);
                        }

                        return result;
                    }
                    finally
                    {
                        UpstreamNativeMethods.free_bytes(data);
                    }
                }
                finally
                {
                    // Free all pinned handles to prevent GC issues
                    for (int index = 0; index < count; index++)
                    {
                        if (handles[index].IsAllocated) handles[index].Free();
                    }
                }
            }
        }

        /// <summary>
        /// Encrypts an existing PDF file with AES-256.
        /// </summary>
        /// <param name="inputPath">Path to the PDF file to encrypt.</param>
        /// <param name="outputPath">The path where the encrypted PDF will be saved.</param>
        /// <param name="userPassword">The user password required to open the encrypted PDF.</param>
        /// <param name="ownerPassword">The owner password. When null or empty, <paramref name="userPassword"/> is used.</param>
        /// <exception cref="ArgumentException">Thrown when a path or the user password is null or empty.</exception>
        /// <exception cref="PdfOxideException">Thrown when the document cannot be opened or saved.</exception>
        public static void Encrypt(
            string inputPath,
            string outputPath,
            string userPassword,
            string? ownerPassword = null)
        {
            ArgumentException.ThrowIfNullOrEmpty(inputPath);
            ArgumentException.ThrowIfNullOrEmpty(outputPath);
            ArgumentException.ThrowIfNullOrEmpty(userPassword);

            using var editor = PdfEditor.Open(inputPath);
            editor.SaveEncrypted(outputPath, userPassword, ownerPassword);
        }

        /// <summary>
        /// Decrypts an existing password-protected PDF file.
        /// </summary>
        /// <param name="inputPath">Path to the encrypted PDF file.</param>
        /// <param name="outputPath">The path where the decrypted PDF will be saved.</param>
        /// <param name="password">The password used to decrypt the document.</param>
        /// <exception cref="ArgumentException">Thrown when a path is null or empty, or the password is empty.</exception>
        /// <exception cref="PdfOxideException">Thrown when the document cannot be opened, the password is incorrect, or the save fails.</exception>
        public static void Decrypt(string inputPath, string outputPath, string password)
        {
            ArgumentException.ThrowIfNullOrEmpty(inputPath);
            ArgumentException.ThrowIfNullOrEmpty(outputPath);
            ArgumentException.ThrowIfNullOrEmpty(password);

            using var editor = PdfEditor.Open(inputPath, password);
            editor.Save(outputPath);
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

        private static unsafe IntPtr CreateHtmlHandle(string html, string css)
        {
            int errorCode = 0;
            if (string.IsNullOrEmpty(css))
            {
                fixed (byte* pHtml = GetUtf8Bytes(html))
                {
                    IntPtr handle = UpstreamNativeMethods.pdf_from_html(pHtml, &errorCode);
                    return EnsureHandle(handle, errorCode, "HTML conversion");
                }
            }

            fixed (byte* pHtml = GetUtf8Bytes(html))
            fixed (byte* pCss = GetUtf8Bytes(css))
            fixed (byte* pFont = DefaultFontBytes)
            {
                IntPtr handle = UpstreamNativeMethods.pdf_from_html_css(
                    pHtml,
                    pCss,
                    pFont,
                    (nuint)DefaultFontBytes.Length,
                    &errorCode);
                return EnsureHandle(handle, errorCode, "HTML conversion");
            }
        }

        private static unsafe IntPtr CreateImageHandle(string imagePath)
        {
            int errorCode = 0;
            fixed (byte* pPath = GetUtf8Bytes(imagePath))
            {
                IntPtr handle = UpstreamNativeMethods.pdf_from_image(pPath, &errorCode);
                return EnsureHandle(handle, errorCode, "image conversion");
            }
        }

        private static unsafe IntPtr CreateImageHandle(byte[] imageData)
        {
            int errorCode = 0;
            fixed (byte* pData = imageData)
            {
                IntPtr handle = UpstreamNativeMethods.pdf_from_image_bytes(
                    pData,
                    imageData.Length,
                    &errorCode);
                return EnsureHandle(handle, errorCode, "image conversion");
            }
        }

        private static IntPtr EnsureHandle(IntPtr handle, int errorCode, string operation)
        {
            if (handle == IntPtr.Zero)
            {
                ThrowIfError(errorCode, operation);
                throw new PdfOxideException($"Native {operation} returned no document.");
            }

            return handle;
        }

        private static unsafe void SaveHandleToFile(IntPtr handle, string outputPath, string operation)
        {
            try
            {
                int errorCode = -1;
                fixed (byte* pOut = GetUtf8Bytes(outputPath))
                {
                    int result = UpstreamNativeMethods.pdf_save(handle, pOut, &errorCode);
                    ThrowIfError(errorCode, operation);
                    ThrowIfError(result, operation);
                }
            }
            finally
            {
                UpstreamNativeMethods.pdf_free(handle);
            }
        }

        private static unsafe byte[] SaveHandleToBytes(IntPtr handle, string operation)
        {
            try
            {
                int dataLength = 0;
                int errorCode = 0;
                byte* data = UpstreamNativeMethods.pdf_save_to_bytes(handle, &dataLength, &errorCode);
                ThrowIfError(errorCode, operation);
                if (data == null)
                {
                    throw new PdfOxideException($"Native {operation} returned no data.");
                }

                try
                {
                    var result = new byte[dataLength];
                    if (dataLength > 0)
                    {
                        Marshal.Copy((IntPtr)data, result, 0, dataLength);
                    }

                    return result;
                }
                finally
                {
                    UpstreamNativeMethods.free_bytes(data);
                }
            }
            finally
            {
                UpstreamNativeMethods.pdf_free(handle);
            }
        }

        private static void ThrowIfError(int errorCode, string operation)
        {
            if (errorCode != 0)
            {
                throw new PdfOxideException(FormattableString.Invariant(
                    $"Native {operation} failed (error code {errorCode})."));
            }
        }

        private static byte[] LoadDefaultFontBytes()
        {
            using Stream? stream = typeof(PdfConverter).Assembly.GetManifestResourceStream(DefaultFontResourceName);
            if (stream is null)
            {
                throw new PdfOxideException("The bundled default font resource could not be found.");
            }

            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            return buffer.ToArray();
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
