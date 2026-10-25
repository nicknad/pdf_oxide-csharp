using System;
using System.IO;
using PdfOxide;
using Xunit;

namespace PdfOxideTests
{
    /// <summary>
    /// Tests for the extended PDF creation API.
    /// </summary>
    public class BuilderTests
    {
        private static readonly byte[] OnePixelPng = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

        [Fact]
        public void BuildsMultiElementPagesWithMetadata()
        {
            string path = TemporaryPath();

            try
            {
                using (var builder = new DocumentBuilder())
                {
                    builder.SetMetadata(new PdfMetadata
                    {
                        Title = "Builder test",
                        Author = "pdf-oxide",
                        Subject = "Extended creation",
                        Keywords = "pdf,test",
                        Creator = "PdfOxideTests",
                    });

                    builder.AddPage(PdfPageSize.A4)
                        .Heading(1, "Document heading")
                        .Paragraph("alpha beta gamma")
                        .BulletList("first bullet", "second bullet")
                        .NumberedList(["first number", "second number"])
                        .CodeBlock("var answer = 42;", "csharp")
                        .HorizontalRule()
                        .Space(12f)
                        .RichParagraph(
                            PdfTextRun.Normal("normal run "),
                            PdfTextRun.Bold("bold run "),
                            PdfTextRun.Italic("italic run "),
                            PdfTextRun.Colored("colored run", PdfColor.FromRgb(255, 0, 0)));

                    builder.AddPage(PdfPageSize.Letter).Paragraph("letter page");
                    builder.AddPage(400f, 300f).Paragraph("custom page");

                    builder.Save(path);
                }

                var document = new PdfDocument(path);
                Assert.Equal(3, document.GetPageCount());
                string text = document.ExtractText();
                Assert.Contains("Document heading", text, StringComparison.Ordinal);
                Assert.Contains("alpha beta gamma", text, StringComparison.Ordinal);
                Assert.Contains("first bullet", text, StringComparison.Ordinal);
                Assert.Contains("first number", text, StringComparison.Ordinal);
                Assert.Contains("var answer = 42;", text, StringComparison.Ordinal);
                Assert.Contains("bold run", text, StringComparison.Ordinal);
                Assert.Contains("letter page", text, StringComparison.Ordinal);
                Assert.Contains("custom page", text, StringComparison.Ordinal);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void BuildsColumnsTablesAndImages()
        {
            string path = TemporaryPath();
            string imagePath = TemporaryPath("png");

            try
            {
                File.WriteAllBytes(imagePath, OnePixelPng);

                using (var builder = new DocumentBuilder())
                {
                    var table = new PdfTable(
                    [
                        ["Name", "Value"],
                        ["Alice", "42"],
                        ["Bob", "7"],
                    ])
                    {
                        HasHeaderRow = true,
                        Style = PdfTableStyle.Bordered,
                    };

                    builder.AddPage(PdfPageSize.A4)
                        .Columns(2, 12f, "first column content second column content")
                        .Table(table)
                        .Image(imagePath, new PdfRect(72f, 200f, 64f, 64f), "file image")
                        .Image(OnePixelPng, new PdfRect(200f, 200f, 64f, 64f), "memory image");

                    builder.Save(path);
                }

                var document = new PdfDocument(path);
                string text = document.ExtractText();
                Assert.Contains("Name", text, StringComparison.Ordinal);
                Assert.Contains("Alice", text, StringComparison.Ordinal);
                Assert.Contains("42", text, StringComparison.Ordinal);
                Assert.Contains("Bob", text, StringComparison.Ordinal);
            }
            finally
            {
                File.Delete(path);
                File.Delete(imagePath);
            }
        }

        [Fact]
        public void ReturnsPdfBytes()
        {
            using var builder = new DocumentBuilder();
            builder.AddPage().Paragraph("bytes content");

            byte[] bytes = builder.ToBytes();

            Assert.True(bytes.Length > 4);
            Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));

            string path = TemporaryPath();
            try
            {
                File.WriteAllBytes(path, bytes);
                var document = new PdfDocument(path);
                Assert.Equal(1, document.GetPageCount());
                Assert.Contains("bytes content", document.ExtractText(), StringComparison.Ordinal);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void ThrowsTypedExceptionWhenNativeRenderingFails()
        {
            using var builder = new DocumentBuilder();
            builder.AddPage().Image([1, 2, 3], new PdfRect(72f, 200f, 64f, 64f));

            Assert.Throws<PdfOxideException>(() => builder.ToBytes());
        }

        [Fact]
        public void RejectsContentForNonCurrentPage()
        {
            using var builder = new DocumentBuilder();
            PdfPageBuilder firstPage = builder.AddPage();
            _ = builder.AddPage();

            Assert.Throws<PdfOxideException>(() => firstPage.Paragraph("late content"));
        }

        private static string TemporaryPath(string extension = "pdf")
        {
            return Path.Combine(Path.GetTempPath(), $"pdfoxide-builder-{Guid.NewGuid():N}.{extension}");
        }
    }
}
