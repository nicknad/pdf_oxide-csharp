using System;
using System.IO;
using PdfOxide;
using Xunit;

namespace PdfOxideTests
{
    /// <summary>
    /// Tests for the extended reading and extraction API.
    /// </summary>
    public class ExtendedExtractionTests
    {
        private static readonly byte[] OnePixelPng = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAYAAAAf8/9hAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAAcSURBVDhPY/jPwPCfEjxqwKgBIDxqwDAwgOE/ADDE/hDMJG6TAAAAAElFTkSuQmCC");

        [Fact]
        public void ExtractsTextAndFormats()
        {
            string path = CreateFixture();

            try
            {
                var document = new PdfDocument(path);

                Assert.Contains("alpha beta gamma", document.ExtractText(0), StringComparison.Ordinal);
                Assert.Contains("second page", document.ExtractText(1), StringComparison.Ordinal);

                Assert.Contains("alpha beta gamma", document.ToPlainText(0), StringComparison.Ordinal);
                Assert.Contains("second page", document.ToPlainText(), StringComparison.Ordinal);

                string html = document.ToHtml(0);
                Assert.Contains("<h1>", html, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("alpha beta gamma", html, StringComparison.Ordinal);
                Assert.Contains("alpha beta gamma", document.ToHtml(), StringComparison.Ordinal);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void ConvertsToOfficeFormats()
        {
            string path = CreateFixture();

            try
            {
                var document = new PdfDocument(path);

                byte[] docx = document.ToDocx();
                byte[] pptx = document.ToPptx();
                byte[] xlsx = document.ToXlsx();

                AssertOfficeZip(docx);
                AssertOfficeZip(pptx);
                AssertOfficeZip(xlsx);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void ExtractsLayoutObjects()
        {
            string path = CreateFixture();

            try
            {
                var document = new PdfDocument(path);

                Assert.NotEmpty(document.ExtractWords(0));
                Assert.NotEmpty(document.ExtractTextLines(0));
                Assert.NotEmpty(document.ExtractChars(0));
                Assert.NotEmpty(document.ExtractSpans(0));
                Assert.NotNull(document.ExtractTables(0));
                Assert.NotNull(document.ExtractPaths(0));
                Assert.NotNull(document.ExtractEmbeddedFiles());
                Assert.NotNull(document.ExtractEmbeddedFonts());

                var images = document.ExtractImages(0);
                Assert.Single(images);
                Assert.NotEmpty(images[0].GetPngData());
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void ReadsPageInformationMetadataAndStructure()
        {
            string path = CreateFixture();

            try
            {
                var document = new PdfDocument(path);

                Assert.Equal("1.7", document.GetVersion());

                PdfPageInfo page = document.GetPageInfo(0);
                Assert.True(page.Width > 0f);
                Assert.True(page.Height > 0f);
                Assert.InRange(page.Rotation, 0, 359);
                Assert.Equal(page.Width, page.MediaBox.Width);

                PdfDocumentMetadata metadata = document.GetMetadata();
                Assert.Equal("Extended extraction", metadata.Title);
                Assert.Equal("pdf-oxide", metadata.Author);
                Assert.Equal("PdfOxideTests", metadata.Creator);

                _ = document.GetXmpMetadata();
                Assert.NotNull(document.GetOutline());
                Assert.Equal(document.GetPageCount(), document.GetPageLabels().Count);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void RejectsNegativePageIndexes()
        {
            string path = CreateFixture();

            try
            {
                var document = new PdfDocument(path);
                Assert.Throws<ArgumentOutOfRangeException>(() => document.ExtractText(-1));
                Assert.Throws<ArgumentOutOfRangeException>(() => document.ToPlainText(-1));
                Assert.Throws<ArgumentOutOfRangeException>(() => document.ToHtml(-1));
                Assert.Throws<ArgumentOutOfRangeException>(() => document.ExtractWords(-1));
                Assert.Throws<ArgumentOutOfRangeException>(() => document.GetPageInfo(-1));
            }
            finally
            {
                File.Delete(path);
            }
        }

        private static string CreateFixture()
        {
            string path = Path.Combine(Path.GetTempPath(), $"pdfoxide-ext-{Guid.NewGuid():N}.pdf");
            string imagePath = Path.ChangeExtension(path, ".png");
            File.WriteAllBytes(imagePath, OnePixelPng);

            try
            {
                using var builder = new DocumentBuilder();
                builder.SetMetadata(new PdfMetadata
                {
                    Title = "Extended extraction",
                    Author = "pdf-oxide",
                    Subject = "Reading API",
                    Keywords = "pdf,extraction",
                    Creator = "PdfOxideTests",
                });

                builder.AddPage(PdfPageSize.A4)
                    .Heading(1, "Heading")
                    .Paragraph("alpha beta gamma")
                    .Image(imagePath, new PdfRect(72, 72, 32, 32), "logo");

                builder.AddPage(PdfPageSize.Letter)
                    .Paragraph("second page");

                builder.Save(path);
            }
            finally
            {
                File.Delete(imagePath);
            }

            return path;
        }

        private static void AssertOfficeZip(byte[] bytes)
        {
            Assert.NotNull(bytes);
            Assert.True(bytes.Length > 4);
            Assert.Equal((byte)'P', bytes[0]);
            Assert.Equal((byte)'K', bytes[1]);
        }
    }
}
