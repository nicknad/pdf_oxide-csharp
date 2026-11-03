using System;
using System.IO;
using PdfOxide;
using Xunit;

namespace PdfOxideTests
{
    /// <summary>
    /// Tests for editing existing PDFs with <see cref="PdfEditor"/>.
    /// </summary>
    public class PageEditingTests
    {
        private static readonly string[] PageTexts = ["page-one", "page-two", "page-three"];

        [Fact]
        public void DeletesPages()
        {
            string sourcePath = CreateThreePagePdf();
            string outputPath = TemporaryPath();

            try
            {
                using (PdfEditor editor = PdfEditor.Open(sourcePath))
                {
                    Assert.Equal(3, editor.GetPageCount());
                    editor.DeletePage(1);
                    Assert.Equal(2, editor.GetPageCount());
                    editor.Save(outputPath);
                }

                var document = new PdfDocument(outputPath);
                Assert.Equal(2, document.GetPageCount());
                Assert.Contains("page-one", document.ExtractText(0), StringComparison.Ordinal);
                Assert.Contains("page-three", document.ExtractText(1), StringComparison.Ordinal);
            }
            finally
            {
                File.Delete(sourcePath);
                File.Delete(outputPath);
            }
        }

        [Fact]
        public void MovesPages()
        {
            string sourcePath = CreateThreePagePdf();
            string outputPath = TemporaryPath();

            try
            {
                using (PdfEditor editor = PdfEditor.Open(sourcePath))
                {
                    editor.MovePage(2, 0);
                    editor.Save(outputPath);
                }

                var document = new PdfDocument(outputPath);
                Assert.Equal(3, document.GetPageCount());
                Assert.Contains("page-three", document.ExtractText(0), StringComparison.Ordinal);
                Assert.Contains("page-one", document.ExtractText(1), StringComparison.Ordinal);
                Assert.Contains("page-two", document.ExtractText(2), StringComparison.Ordinal);
            }
            finally
            {
                File.Delete(sourcePath);
                File.Delete(outputPath);
            }
        }

        [Fact]
        public void RotatesPages()
        {
            string sourcePath = CreateThreePagePdf();
            string firstOutputPath = TemporaryPath();
            string secondOutputPath = TemporaryPath();

            try
            {
                using (PdfEditor editor = PdfEditor.Open(sourcePath))
                {
                    editor.RotatePage(0, 90);
                    editor.Save(firstOutputPath);
                }

                using (PdfEditor editor = PdfEditor.Open(firstOutputPath))
                {
                    Assert.Equal(90, editor.GetPageRotation(0));
                    Assert.Equal(0, editor.GetPageRotation(1));

                    editor.RotateAllPages(180);
                    editor.Save(secondOutputPath);
                }

                using (PdfEditor editor = PdfEditor.Open(secondOutputPath))
                {
                    Assert.Equal(270, editor.GetPageRotation(0));
                    Assert.Equal(180, editor.GetPageRotation(1));
                    Assert.Equal(180, editor.GetPageRotation(2));
                }
            }
            finally
            {
                File.Delete(sourcePath);
                File.Delete(firstOutputPath);
                File.Delete(secondOutputPath);
            }
        }

        [Fact]
        public void SetsCropAndMediaBoxes()
        {
            string sourcePath = CreateThreePagePdf();
            string outputPath = TemporaryPath();

            try
            {
                using (PdfEditor editor = PdfEditor.Open(sourcePath))
                {
                    PdfRect media = editor.GetMediaBox(0);
                    Assert.InRange(media.Width, 590f, 600f);
                    Assert.InRange(media.Height, 835f, 845f);
                    Assert.Null(editor.GetCropBox(0));

                    editor.SetCropBox(0, new PdfRect(10f, 20f, 100f, 200f));
                    editor.SetMediaBox(0, new PdfRect(5f, 6f, 300f, 400f));
                    editor.Save(outputPath);
                }

                using (PdfEditor editor = PdfEditor.Open(outputPath))
                {
                    PdfRect? crop = editor.GetCropBox(0);
                    Assert.True(crop.HasValue);
                    AssertRect(new PdfRect(10f, 20f, 100f, 200f), crop.GetValueOrDefault());

                    AssertRect(new PdfRect(5f, 6f, 300f, 400f), editor.GetMediaBox(0));
                }
            }
            finally
            {
                File.Delete(sourcePath);
                File.Delete(outputPath);
            }
        }

        [Fact]
        public void CropsMargins()
        {
            string sourcePath = CreateThreePagePdf();

            try
            {
                using PdfEditor editor = PdfEditor.Open(sourcePath);
                PdfRect media = editor.GetMediaBox(0);
                editor.CropMargins(10f, 20f, 30f, 40f);

                PdfRect? crop = editor.GetCropBox(0);
                Assert.True(crop.HasValue);
                AssertRect(
                    new PdfRect(
                        media.X + 10f,
                        media.Y + 40f,
                        media.Width - 30f,
                        media.Height - 70f),
                    crop.GetValueOrDefault());
            }
            finally
            {
                File.Delete(sourcePath);
            }
        }

        [Fact]
        public void ExtractsPages()
        {
            string sourcePath = CreateThreePagePdf();
            string extractedPath = TemporaryPath();
            string singlePath = TemporaryPath();

            try
            {
                using (PdfEditor editor = PdfEditor.Open(sourcePath))
                {
                    byte[] bytes = editor.ExtractPages(0, 2);
                    File.WriteAllBytes(extractedPath, bytes);
                    editor.SaveExtracted(singlePath, 1);
                }

                var extracted = new PdfDocument(extractedPath);
                Assert.Equal(2, extracted.GetPageCount());
                Assert.Contains("page-one", extracted.ExtractText(0), StringComparison.Ordinal);
                Assert.Contains("page-three", extracted.ExtractText(1), StringComparison.Ordinal);

                var single = new PdfDocument(singlePath);
                Assert.Equal(1, single.GetPageCount());
                Assert.Contains("page-two", single.ExtractText(0), StringComparison.Ordinal);
            }
            finally
            {
                File.Delete(sourcePath);
                File.Delete(extractedPath);
                File.Delete(singlePath);
            }
        }

        [Fact]
        public void SplitsIntoOneFilePerPage()
        {
            string sourcePath = CreateThreePagePdf();
            string outputDirectory = Path.Combine(
                Path.GetTempPath(),
                $"pdfoxide-split-{Guid.NewGuid():N}");

            try
            {
                using (PdfEditor editor = PdfEditor.Open(sourcePath))
                {
                    editor.Split(outputDirectory, "part");
                }

                for (int pageIndex = 0; pageIndex < PageTexts.Length; pageIndex++)
                {
                    string pagePath = Path.Combine(outputDirectory, $"part-{pageIndex + 1}.pdf");
                    Assert.True(File.Exists(pagePath));

                    var document = new PdfDocument(pagePath);
                    Assert.Equal(1, document.GetPageCount());
                    Assert.Contains(PageTexts[pageIndex], document.ExtractText(), StringComparison.Ordinal);
                }

                Assert.Equal(PageTexts.Length, Directory.GetFiles(outputDirectory, "*.pdf").Length);
            }
            finally
            {
                File.Delete(sourcePath);
                if (Directory.Exists(outputDirectory))
                {
                    Directory.Delete(outputDirectory, recursive: true);
                }
            }
        }

        [Fact]
        public void MergesDocuments()
        {
            string firstPath = CreatePdf("first-doc-page");
            string secondPath = CreatePdf("second-doc-page-a", "second-doc-page-b");
            string pathOutput = TemporaryPath();
            string bytesOutput = TemporaryPath();

            try
            {
                using (PdfEditor editor = PdfEditor.Open(firstPath))
                {
                    editor.MergeFrom(secondPath);
                    Assert.Equal(3, editor.GetPageCount());
                    editor.Save(pathOutput);
                }

                var merged = new PdfDocument(pathOutput);
                Assert.Equal(3, merged.GetPageCount());
                string text = merged.ExtractText();
                Assert.Contains("first-doc-page", text, StringComparison.Ordinal);
                Assert.Contains("second-doc-page-a", text, StringComparison.Ordinal);
                Assert.Contains("second-doc-page-b", text, StringComparison.Ordinal);

                using (PdfEditor editor = PdfEditor.Open(firstPath))
                {
                    editor.MergeFrom(File.ReadAllBytes(secondPath));
                    Assert.Equal(3, editor.GetPageCount());
                    editor.Save(bytesOutput);
                }

                Assert.Equal(3, new PdfDocument(bytesOutput).GetPageCount());
            }
            finally
            {
                File.Delete(firstPath);
                File.Delete(secondPath);
                File.Delete(pathOutput);
                File.Delete(bytesOutput);
            }
        }

        private static string CreateThreePagePdf()
        {
            return CreatePdf(PageTexts);
        }

        private static string CreatePdf(params string[] pageTexts)
        {
            string path = TemporaryPath();
            using var builder = new DocumentBuilder();
            foreach (string pageText in pageTexts)
            {
                builder.AddPage(pageText);
            }

            builder.Save(path);
            return path;
        }

        private static void AssertRect(PdfRect expected, PdfRect actual)
        {
            const float tolerance = 0.01f;
            Assert.InRange(actual.X, expected.X - tolerance, expected.X + tolerance);
            Assert.InRange(actual.Y, expected.Y - tolerance, expected.Y + tolerance);
            Assert.InRange(actual.Width, expected.Width - tolerance, expected.Width + tolerance);
            Assert.InRange(actual.Height, expected.Height - tolerance, expected.Height + tolerance);
        }

        private static string TemporaryPath()
        {
            return Path.Combine(Path.GetTempPath(), $"pdfoxide-editor-{Guid.NewGuid():N}.pdf");
        }
    }
}
