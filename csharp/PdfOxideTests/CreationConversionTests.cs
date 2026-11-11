using System;
using System.IO;
using PdfOxide;
using Xunit;

namespace PdfOxideTests
{
    /// <summary>
    /// Tests for HTML, single-image, and merge conversion APIs.
    /// </summary>
    public class CreationConversionTests
    {
        private const string OnePixelPngBase64 =
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=";

        [Fact]
        public void ConvertsHtmlWithCssToPdf()
        {
            string path = TemporaryPath();

            try
            {
                PdfConverter.ConvertHtml(
                    "<h1>Styled heading</h1><p>body text</p>",
                    "h1 { color: #ff0000; font-size: 24pt; }",
                    path);

                var document = new PdfDocument(path);
                Assert.True(document.GetPageCount() >= 1);

                string text = document.ExtractText();
                Assert.Contains("Styled heading", text, StringComparison.Ordinal);
                Assert.Contains("body text", text, StringComparison.Ordinal);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void ConvertsHtmlToBytes()
        {
            string path = TemporaryPath();

            try
            {
                byte[] bytes = PdfConverter.ConvertHtmlToBytes(
                    "<h1>Bytes heading</h1><p>bytes body</p>");
                Assert.True(bytes.Length > 0);

                File.WriteAllBytes(path, bytes);
                var document = new PdfDocument(path);
                Assert.True(document.GetPageCount() >= 1);

                string text = document.ExtractText();
                Assert.Contains("Bytes heading", text, StringComparison.Ordinal);
                Assert.Contains("bytes body", text, StringComparison.Ordinal);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void ConvertsSingleImageFileAndBytesToPdf()
        {
            string imagePath = TemporaryPath("png");
            string fromFilePath = TemporaryPath();
            string fromBytesPath = TemporaryPath();

            try
            {
                byte[] image = Convert.FromBase64String(OnePixelPngBase64);
                File.WriteAllBytes(imagePath, image);

                PdfConverter.ConvertImage(imagePath, fromFilePath);
                PdfConverter.ConvertImage(image, fromBytesPath);

                var fileDocument = new PdfDocument(fromFilePath);
                Assert.Equal(1, fileDocument.GetPageCount());

                var bytesDocument = new PdfDocument(fromBytesPath);
                Assert.Equal(1, bytesDocument.GetPageCount());
            }
            finally
            {
                File.Delete(imagePath);
                File.Delete(fromFilePath);
                File.Delete(fromBytesPath);
            }
        }

        [Fact]
        public void MergesPdfFilesToFileAndBytes()
        {
            string firstPath = TemporaryPath();
            string secondPath = TemporaryPath();
            string mergedPath = TemporaryPath();
            string mergedBytesPath = TemporaryPath();

            try
            {
                PdfConverter.ConvertText("first", firstPath);
                PdfConverter.ConvertText("second", secondPath);

                PdfConverter.Merge([firstPath, secondPath], mergedPath);
                var mergedDocument = new PdfDocument(mergedPath);
                Assert.Equal(2, mergedDocument.GetPageCount());
                string mergedText = mergedDocument.ExtractText();
                Assert.Contains("first", mergedText, StringComparison.Ordinal);
                Assert.Contains("second", mergedText, StringComparison.Ordinal);

                byte[] mergedBytes = PdfConverter.MergeToBytes([firstPath, secondPath]);
                Assert.True(mergedBytes.Length > 0);

                File.WriteAllBytes(mergedBytesPath, mergedBytes);
                var mergedBytesDocument = new PdfDocument(mergedBytesPath);
                Assert.Equal(2, mergedBytesDocument.GetPageCount());
                string mergedBytesText = mergedBytesDocument.ExtractText();
                Assert.Contains("first", mergedBytesText, StringComparison.Ordinal);
                Assert.Contains("second", mergedBytesText, StringComparison.Ordinal);
            }
            finally
            {
                File.Delete(firstPath);
                File.Delete(secondPath);
                File.Delete(mergedPath);
                File.Delete(mergedBytesPath);
            }
        }

        [Fact]
        public void ConvertsImageToBytes()
        {
            string path = TemporaryPath();

            try
            {
                byte[] image = Convert.FromBase64String(OnePixelPngBase64);
                byte[] pdf = PdfConverter.ConvertImageToBytes(image);
                Assert.True(pdf.Length > 0);

                File.WriteAllBytes(path, pdf);
                var document = new PdfDocument(path);
                Assert.Equal(1, document.GetPageCount());
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void ConvertsImageSourceUnionToFileAndBytes()
        {
            string imagePath = TemporaryPath("png");
            string fromPath = TemporaryPath();
            string fromBytes = TemporaryPath();

            try
            {
                byte[] image = Convert.FromBase64String(OnePixelPngBase64);
                File.WriteAllBytes(imagePath, image);

                PdfImageSource pathSource = imagePath;
                PdfImageSource bytesSource = image;

                PdfConverter.ConvertImage(pathSource, fromPath);
                PdfConverter.ConvertImage(bytesSource, fromBytes);

                Assert.Equal(1, new PdfDocument(fromPath).GetPageCount());
                Assert.Equal(1, new PdfDocument(fromBytes).GetPageCount());

                byte[] pdf = PdfConverter.ConvertImageToBytes(bytesSource);
                Assert.True(pdf.Length > 0);
            }
            finally
            {
                File.Delete(imagePath);
                File.Delete(fromPath);
                File.Delete(fromBytes);
            }
        }

        private static string TemporaryPath(string extension = "pdf")
        {
            return Path.Combine(Path.GetTempPath(), $"pdfoxide-creation-{Guid.NewGuid():N}.{extension}");
        }
    }
}
