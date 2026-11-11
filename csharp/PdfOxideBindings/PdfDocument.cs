using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

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
                        throw new PdfOxideException(GetLastError());
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
                        throw new PdfOxideException(GetLastError());
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
                        throw new PdfOxideException(GetLastError());
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
                        throw new PdfOxideException(GetLastError());
                    }
                    return PtrToStringAndFree(result);
                }
            }
        }

        /// <summary>
        /// Extracts the text content of a single page.
        /// </summary>
        /// <param name="pageIndex">Zero-based index of the page.</param>
        /// <returns>The extracted text.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="pageIndex"/> is negative.</exception>
        /// <exception cref="PdfOxideException">Thrown when extraction fails.</exception>
        public string ExtractText(int pageIndex)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
            return ReadString(StringOperation.ExtractTextPage, pageIndex);
        }

        /// <summary>
        /// Converts all pages in the PDF to plain text.
        /// </summary>
        /// <returns>The plain text content.</returns>
        /// <exception cref="PdfOxideException">Thrown when conversion fails.</exception>
        public string ToPlainText()
        {
            return ReadString(StringOperation.ToPlainTextAll);
        }

        /// <summary>
        /// Converts a specific page to plain text.
        /// </summary>
        /// <param name="pageIndex">Zero-based index of the page.</param>
        /// <returns>The plain text content.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="pageIndex"/> is negative.</exception>
        /// <exception cref="PdfOxideException">Thrown when conversion fails.</exception>
        public string ToPlainText(int pageIndex)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
            return ReadString(StringOperation.ToPlainTextPage, pageIndex);
        }

        /// <summary>
        /// Converts all pages in the PDF to HTML.
        /// </summary>
        /// <returns>The HTML content.</returns>
        /// <exception cref="PdfOxideException">Thrown when conversion fails.</exception>
        public string ToHtml()
        {
            return ReadString(StringOperation.ToHtmlAll);
        }

        /// <summary>
        /// Converts a specific page to HTML.
        /// </summary>
        /// <param name="pageIndex">Zero-based index of the page.</param>
        /// <returns>The HTML content.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="pageIndex"/> is negative.</exception>
        /// <exception cref="PdfOxideException">Thrown when conversion fails.</exception>
        public string ToHtml(int pageIndex)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
            return ReadString(StringOperation.ToHtmlPage, pageIndex);
        }

        /// <summary>
        /// Converts the document to a DOCX file.
        /// </summary>
        /// <returns>The DOCX bytes.</returns>
        /// <exception cref="PdfOxideException">Thrown when conversion fails.</exception>
        public byte[] ToDocx()
        {
            return ReadBytes(BytesOperation.Docx);
        }

        /// <summary>
        /// Converts the document to a PPTX file.
        /// </summary>
        /// <returns>The PPTX bytes.</returns>
        /// <exception cref="PdfOxideException">Thrown when conversion fails.</exception>
        public byte[] ToPptx()
        {
            return ReadBytes(BytesOperation.Pptx);
        }

        /// <summary>
        /// Converts the document to an XLSX file.
        /// </summary>
        /// <returns>The XLSX bytes.</returns>
        /// <exception cref="PdfOxideException">Thrown when conversion fails.</exception>
        public byte[] ToXlsx()
        {
            return ReadBytes(BytesOperation.Xlsx);
        }

        /// <summary>
        /// Extracts the images from a page.
        /// </summary>
        /// <param name="pageIndex">Zero-based index of the page.</param>
        /// <returns>The extracted images, each with base64-encoded PNG data.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="pageIndex"/> is negative.</exception>
        /// <exception cref="PdfOxideException">Thrown when extraction fails.</exception>
        public IReadOnlyList<PdfExtractedImage> ExtractImages(int pageIndex)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
            return DeserializeList<PdfExtractedImage>(ReadString(StringOperation.ExtractImages, pageIndex));
        }

        /// <summary>
        /// Extracts the files embedded in the document.
        /// </summary>
        /// <returns>The embedded files, each with base64-encoded data.</returns>
        /// <exception cref="PdfOxideException">Thrown when extraction fails.</exception>
        public IReadOnlyList<PdfEmbeddedFile> ExtractEmbeddedFiles()
        {
            return DeserializeList<PdfEmbeddedFile>(ReadString(StringOperation.ExtractEmbeddedFiles));
        }

        /// <summary>
        /// Extracts the fonts embedded in the document.
        /// </summary>
        /// <returns>The embedded fonts, each with base64-encoded data.</returns>
        /// <exception cref="PdfOxideException">Thrown when extraction fails.</exception>
        public IReadOnlyList<PdfEmbeddedFile> ExtractEmbeddedFonts()
        {
            return DeserializeList<PdfEmbeddedFile>(ReadString(StringOperation.ExtractEmbeddedFonts));
        }

        /// <summary>
        /// Extracts the tables from a page.
        /// </summary>
        /// <param name="pageIndex">Zero-based index of the page.</param>
        /// <returns>The extracted tables.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="pageIndex"/> is negative.</exception>
        /// <exception cref="PdfOxideException">Thrown when extraction fails.</exception>
        public IReadOnlyList<PdfExtractedTable> ExtractTables(int pageIndex)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
            return DeserializeList<PdfExtractedTable>(ReadString(StringOperation.ExtractTables, pageIndex));
        }

        /// <summary>
        /// Extracts the words from a page.
        /// </summary>
        /// <param name="pageIndex">Zero-based index of the page.</param>
        /// <returns>The extracted words.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="pageIndex"/> is negative.</exception>
        /// <exception cref="PdfOxideException">Thrown when extraction fails.</exception>
        public IReadOnlyList<PdfWord> ExtractWords(int pageIndex)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
            return DeserializeList<PdfWord>(ReadString(StringOperation.ExtractWords, pageIndex));
        }

        /// <summary>
        /// Extracts the text lines from a page.
        /// </summary>
        /// <param name="pageIndex">Zero-based index of the page.</param>
        /// <returns>The extracted text lines.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="pageIndex"/> is negative.</exception>
        /// <exception cref="PdfOxideException">Thrown when extraction fails.</exception>
        public IReadOnlyList<PdfTextLine> ExtractTextLines(int pageIndex)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
            return DeserializeList<PdfTextLine>(ReadString(StringOperation.ExtractTextLines, pageIndex));
        }

        /// <summary>
        /// Extracts the characters from a page.
        /// </summary>
        /// <param name="pageIndex">Zero-based index of the page.</param>
        /// <returns>The extracted characters.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="pageIndex"/> is negative.</exception>
        /// <exception cref="PdfOxideException">Thrown when extraction fails.</exception>
        public IReadOnlyList<PdfTextChar> ExtractChars(int pageIndex)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
            return DeserializeList<PdfTextChar>(ReadString(StringOperation.ExtractChars, pageIndex));
        }

        /// <summary>
        /// Extracts the styled text spans from a page.
        /// </summary>
        /// <param name="pageIndex">Zero-based index of the page.</param>
        /// <returns>The extracted spans.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="pageIndex"/> is negative.</exception>
        /// <exception cref="PdfOxideException">Thrown when extraction fails.</exception>
        public IReadOnlyList<PdfTextSpan> ExtractSpans(int pageIndex)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
            return DeserializeList<PdfTextSpan>(ReadString(StringOperation.ExtractSpans, pageIndex));
        }

        /// <summary>
        /// Extracts the vector paths from a page.
        /// </summary>
        /// <param name="pageIndex">Zero-based index of the page.</param>
        /// <returns>The extracted paths.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="pageIndex"/> is negative.</exception>
        /// <exception cref="PdfOxideException">Thrown when extraction fails.</exception>
        public IReadOnlyList<PdfPath> ExtractPaths(int pageIndex)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
            return DeserializeList<PdfPath>(ReadString(StringOperation.ExtractPaths, pageIndex));
        }

        /// <summary>
        /// Gets the PDF version.
        /// </summary>
        /// <returns>The version string, such as <c>1.7</c>.</returns>
        /// <exception cref="PdfOxideException">Thrown when the version cannot be read.</exception>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1024:Use properties where appropriate", Justification = "Matches the method-based API of this type.")]
        public string GetVersion()
        {
            return ReadString(StringOperation.GetVersion);
        }

        /// <summary>
        /// Gets the dimensions and rotation of a page.
        /// </summary>
        /// <param name="pageIndex">Zero-based index of the page.</param>
        /// <returns>The page information.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="pageIndex"/> is negative.</exception>
        /// <exception cref="PdfOxideException">Thrown when the page cannot be read.</exception>
        public PdfPageInfo GetPageInfo(int pageIndex)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
            return Deserialize<PdfPageInfo>(ReadString(StringOperation.GetPageInfo, pageIndex));
        }

        /// <summary>
        /// Gets the document metadata from the Info dictionary and XMP.
        /// </summary>
        /// <returns>The document metadata.</returns>
        /// <exception cref="PdfOxideException">Thrown when metadata cannot be read.</exception>
        public PdfDocumentMetadata GetMetadata()
        {
            return Deserialize<PdfDocumentMetadata>(ReadString(StringOperation.GetMetadata));
        }

        /// <summary>
        /// Gets the XMP metadata.
        /// </summary>
        /// <returns>The XMP metadata, or <see langword="null"/> when the document has none.</returns>
        /// <exception cref="PdfOxideException">Thrown when metadata cannot be read.</exception>
        public PdfXmpMetadata? GetXmpMetadata()
        {
            string json = ReadString(StringOperation.GetXmpMetadata);
            return JsonSerializer.Deserialize<PdfXmpMetadata?>(json, JsonOptions);
        }

        /// <summary>
        /// Gets the document outline (bookmarks).
        /// </summary>
        /// <returns>The top-level outline items.</returns>
        /// <exception cref="PdfOxideException">Thrown when the outline cannot be read.</exception>
        public IReadOnlyList<PdfOutlineItem> GetOutline()
        {
            return DeserializeList<PdfOutlineItem>(ReadString(StringOperation.GetOutline));
        }

        /// <summary>
        /// Gets the page labels.
        /// </summary>
        /// <returns>The page labels in page order.</returns>
        /// <exception cref="PdfOxideException">Thrown when the labels cannot be read.</exception>
        public IReadOnlyList<PdfPageLabel> GetPageLabels()
        {
            return DeserializeList<PdfPageLabel>(ReadString(StringOperation.GetPageLabels));
        }

        /// <summary>
        /// Gets the annotations on a page.
        /// </summary>
        /// <param name="pageIndex">Zero-based index of the page.</param>
        /// <returns>The annotations on the page.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="pageIndex"/> is negative.</exception>
        /// <exception cref="PdfOxideException">Thrown when annotations cannot be read.</exception>
        public IReadOnlyList<PdfAnnotationInfo> GetAnnotations(int pageIndex)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);

            unsafe
            {
                IntPtr document = IntPtr.Zero;
                IntPtr list = IntPtr.Zero;
                try
                {
                    int errorCode = 0;
                    fixed (byte* pPath = GetUtf8Bytes(_path))
                    {
                        document = UpstreamNativeMethods.pdf_document_open(pPath, &errorCode);
                    }

                    if (document == IntPtr.Zero)
                    {
                        throw new PdfOxideException(
                            $"Native pdf_document_open failed (error code {errorCode}).");
                    }

                    list = UpstreamNativeMethods.pdf_document_get_page_annotations(
                        document, pageIndex, &errorCode);
                    if (list == IntPtr.Zero)
                    {
                        throw new PdfOxideException(
                            $"Native pdf_document_get_page_annotations failed (error code {errorCode}).");
                    }

                    int count = UpstreamNativeMethods.pdf_oxide_annotation_count(list);
                    int safeCount = Math.Max(count, 0);
                    List<PdfAnnotationInfo> annotations =
                    [
                        with(capacity: safeCount),
                        .. Enumerable.Range(0, safeCount).Select(index => ReadAnnotation(list, index)),
                    ];

                    return annotations;
                }
                finally
                {
                    if (list != IntPtr.Zero)
                    {
                        UpstreamNativeMethods.pdf_oxide_annotation_list_free(list);
                    }
                    if (document != IntPtr.Zero)
                    {
                        UpstreamNativeMethods.pdf_document_free(document);
                    }
                }
            }
        }

        private static unsafe PdfAnnotationInfo ReadAnnotation(IntPtr list, int index)
        {
            string type = ReadAnnotationString(list, index, AnnotationStringField.Type) ?? string.Empty;
            string subtype = ReadAnnotationString(list, index, AnnotationStringField.Subtype) ?? string.Empty;
            string? contents = ReadAnnotationString(list, index, AnnotationStringField.Content);
            string? author = ReadAnnotationString(list, index, AnnotationStringField.Author);
            string? uri = ReadAnnotationString(list, index, AnnotationStringField.Uri);
            string? iconName = ReadAnnotationString(list, index, AnnotationStringField.IconName);

            int errorCode = 0;
            float x = 0f;
            float y = 0f;
            float width = 0f;
            float height = 0f;
            UpstreamNativeMethods.pdf_oxide_annotation_get_rect(
                list, index, &x, &y, &width, &height, &errorCode);

            uint packedColor = UpstreamNativeMethods.pdf_oxide_annotation_get_color(list, index, &errorCode);
            PdfColor? color = packedColor == 0
                ? null
                : PdfColor.FromRgb(
                    (byte)((packedColor >> 16) & 0xFF),
                    (byte)((packedColor >> 8) & 0xFF),
                    (byte)(packedColor & 0xFF));

            return new PdfAnnotationInfo
            {
                Type = type,
                Subtype = subtype,
                Contents = contents,
                Author = author,
                Rect = new PdfRect(x, y, width, height),
                Color = color,
                IsHidden = UpstreamNativeMethods.pdf_oxide_annotation_is_hidden(list, index, &errorCode) != 0,
                IsPrintable = UpstreamNativeMethods.pdf_oxide_annotation_is_printable(list, index, &errorCode) != 0,
                IsReadOnly = UpstreamNativeMethods.pdf_oxide_annotation_is_read_only(list, index, &errorCode) != 0,
                Uri = uri,
                IconName = iconName,
            };
        }

        private static unsafe string? ReadAnnotationString(
            IntPtr list,
            int index,
            AnnotationStringField field)
        {
            int errorCode = 0;
            byte* ptr = field switch
            {
                AnnotationStringField.Type => UpstreamNativeMethods.pdf_oxide_annotation_get_type(
                    list, index, &errorCode),
                AnnotationStringField.Subtype => UpstreamNativeMethods.pdf_oxide_annotation_get_subtype(
                    list, index, &errorCode),
                AnnotationStringField.Content => UpstreamNativeMethods.pdf_oxide_annotation_get_content(
                    list, index, &errorCode),
                AnnotationStringField.Author => UpstreamNativeMethods.pdf_oxide_annotation_get_author(
                    list, index, &errorCode),
                AnnotationStringField.Uri => UpstreamNativeMethods.pdf_oxide_link_annotation_get_uri(
                    list, index, &errorCode),
                AnnotationStringField.IconName => UpstreamNativeMethods.pdf_oxide_text_annotation_get_icon_name(
                    list, index, &errorCode),
                _ => null,
            };

            if (ptr == null)
            {
                return null;
            }

            try
            {
                return Utf8PtrToString(ptr);
            }
            finally
            {
                UpstreamNativeMethods.free_string(ptr);
            }
        }

        private enum AnnotationStringField
        {
            Type,
            Subtype,
            Content,
            Author,
            Uri,
            IconName,
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

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
        };

        private enum StringOperation
        {
            ExtractTextPage,
            ToPlainTextPage,
            ToPlainTextAll,
            ToHtmlPage,
            ToHtmlAll,
            ExtractImages,
            ExtractEmbeddedFiles,
            ExtractEmbeddedFonts,
            ExtractTables,
            ExtractWords,
            ExtractTextLines,
            ExtractChars,
            ExtractSpans,
            ExtractPaths,
            GetVersion,
            GetPageInfo,
            GetMetadata,
            GetXmpMetadata,
            GetOutline,
            GetPageLabels,
        }

        private enum BytesOperation
        {
            Docx,
            Pptx,
            Xlsx,
        }

        private unsafe string ReadString(StringOperation operation, int pageIndex = 0)
        {
            fixed (byte* pPath = GetUtf8Bytes(_path))
            {
                byte* result = operation switch
                {
                    StringOperation.ExtractTextPage => NativeMethods.pdf_extract_text_page(pPath, pageIndex),
                    StringOperation.ToPlainTextPage => NativeMethods.pdf_to_plain_text(pPath, pageIndex),
                    StringOperation.ToPlainTextAll => NativeMethods.pdf_to_plain_text_all(pPath),
                    StringOperation.ToHtmlPage => NativeMethods.pdf_to_html(pPath, pageIndex),
                    StringOperation.ToHtmlAll => NativeMethods.pdf_to_html_all(pPath),
                    StringOperation.ExtractImages => NativeMethods.pdf_extract_images(pPath, pageIndex),
                    StringOperation.ExtractEmbeddedFiles => NativeMethods.pdf_extract_embedded_files(pPath),
                    StringOperation.ExtractEmbeddedFonts => NativeMethods.pdf_extract_embedded_fonts(pPath),
                    StringOperation.ExtractTables => NativeMethods.pdf_extract_tables(pPath, pageIndex),
                    StringOperation.ExtractWords => NativeMethods.pdf_extract_words(pPath, pageIndex),
                    StringOperation.ExtractTextLines => NativeMethods.pdf_extract_text_lines(pPath, pageIndex),
                    StringOperation.ExtractChars => NativeMethods.pdf_extract_chars(pPath, pageIndex),
                    StringOperation.ExtractSpans => NativeMethods.pdf_extract_spans(pPath, pageIndex),
                    StringOperation.ExtractPaths => NativeMethods.pdf_extract_paths(pPath, pageIndex),
                    StringOperation.GetVersion => NativeMethods.pdf_get_version(pPath),
                    StringOperation.GetPageInfo => NativeMethods.pdf_get_page_info(pPath, pageIndex),
                    StringOperation.GetMetadata => NativeMethods.pdf_get_metadata(pPath),
                    StringOperation.GetXmpMetadata => NativeMethods.pdf_get_xmp_metadata(pPath),
                    StringOperation.GetOutline => NativeMethods.pdf_get_outline(pPath),
                    StringOperation.GetPageLabels => NativeMethods.pdf_get_page_labels(pPath),
                    _ => null,
                };

                if (result == null)
                {
                    throw new PdfOxideException(GetLastError());
                }

                return PtrToStringAndFree(result);
            }
        }

        private unsafe byte[] ReadBytes(BytesOperation operation)
        {
            fixed (byte* pPath = GetUtf8Bytes(_path))
            {
                byte* data = null;
                int length = 0;
                int result = operation switch
                {
                    BytesOperation.Docx => NativeMethods.pdf_to_docx_bytes(pPath, &data, &length),
                    BytesOperation.Pptx => NativeMethods.pdf_to_pptx_bytes(pPath, &data, &length),
                    BytesOperation.Xlsx => NativeMethods.pdf_to_xlsx_bytes(pPath, &data, &length),
                    _ => -1,
                };

                if (result != 0)
                {
                    throw new PdfOxideException(GetLastError());
                }

                if (length <= 0)
                {
                    return [];
                }

                try
                {
                    var bytes = new byte[length];
                    Marshal.Copy((IntPtr)data, bytes, 0, length);
                    return bytes;
                }
                finally
                {
                    NativeMethods.pdf_bytes_free(data, length);
                }
            }
        }

        private static T Deserialize<T>(string json)
            where T : class
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions)
                ?? throw new PdfOxideException("The native library returned an empty JSON response.");
        }

        private static List<T> DeserializeList<T>(string json)
            where T : class
        {
            return JsonSerializer.Deserialize<List<T>>(json, JsonOptions) ?? [];
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