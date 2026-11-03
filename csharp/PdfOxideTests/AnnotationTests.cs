using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PdfOxide;
using Xunit;

namespace PdfOxideTests
{
    /// <summary>
    /// Tests for annotation creation and reading.
    /// </summary>
    public class AnnotationTests
    {
        [Fact]
        public void BuildsAndReadsAnnotations()
        {
            string path = TemporaryPath();

            try
            {
                using (var builder = new DocumentBuilder())
                {
                    builder.AddPage()
                        .Text("Annotation test paragraph with some text.")
                        .LinkUrl("https://example.com")
                        .Highlight(new PdfColor(1f, 1f, 0f))
                        .Underline(new PdfColor(0f, 0f, 1f))
                        .StickyNote("review this")
                        .FreeText(new PdfRect(72f, 500f, 220f, 60f), "free text")
                        .Watermark("DRAFT")
                        .Stamp(PdfStampType.Draft)
                        .Footnote("1", "Footnote body text");

                    builder.Save(path);
                }

                var document = new PdfDocument(path);
                string text = document.ExtractText();
                Assert.Contains("Annotation test paragraph", text, StringComparison.Ordinal);
                Assert.Contains("Footnote body text", text, StringComparison.Ordinal);

                IReadOnlyList<PdfAnnotationInfo> annotations = document.GetAnnotations(0);
                Assert.NotEmpty(annotations);
                Assert.Contains(annotations, annotation =>
                    string.Equals(annotation.Subtype, "Link", StringComparison.OrdinalIgnoreCase)
                    && annotation.Uri is not null
                    && annotation.Uri.Contains("example.com", StringComparison.OrdinalIgnoreCase));
                AssertHasSubtype(annotations, "Highlight");
                AssertHasSubtype(annotations, "Underline");
                AssertHasSubtype(annotations, "Text");
                AssertHasSubtype(annotations, "FreeText");
                AssertHasSubtype(annotations, "Watermark");
                AssertHasSubtype(annotations, "Stamp");
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void BuildsRectBasedAnnotations()
        {
            string path = TemporaryPath();

            try
            {
                using (var builder = new DocumentBuilder())
                {
                    builder.AddPage()
                        .Text("Rect based annotation text.")
                        .LinkRect(new PdfRect(72f, 600f, 120f, 20f), "https://example.org")
                        .HighlightRect(new PdfRect(72f, 580f, 120f, 20f), new PdfColor(1f, 1f, 0f))
                        .UnderlineRect(new PdfRect(72f, 560f, 120f, 20f), new PdfColor(0f, 0f, 1f))
                        .StrikeoutRect(new PdfRect(72f, 540f, 120f, 20f), new PdfColor(1f, 0f, 0f))
                        .SquigglyRect(new PdfRect(72f, 520f, 120f, 20f), new PdfColor(0f, 1f, 0f))
                        .StickyNoteAt(100f, 480f, "positioned note", PdfTextAnnotationIcon.Comment)
                        .FreeText(new PdfRect(72f, 440f, 220f, 60f), "styled free text", "Helvetica", 10f)
                        .WatermarkConfidential()
                        .StampAt(new PdfRect(300f, 200f, 150f, 50f), PdfStampType.Approved);

                    builder.Save(path);
                }

                var document = new PdfDocument(path);
                IReadOnlyList<PdfAnnotationInfo> annotations = document.GetAnnotations(0);
                Assert.NotEmpty(annotations);
                Assert.Contains(annotations, annotation =>
                    string.Equals(annotation.Subtype, "Link", StringComparison.OrdinalIgnoreCase)
                    && annotation.Uri is not null
                    && annotation.Uri.Contains("example.org", StringComparison.OrdinalIgnoreCase));
                AssertHasSubtype(annotations, "Highlight");
                AssertHasSubtype(annotations, "Underline");
                AssertHasSubtype(annotations, "StrikeOut");
                AssertHasSubtype(annotations, "Squiggly");
                AssertHasSubtype(annotations, "Text");
                AssertHasSubtype(annotations, "FreeText");
                AssertHasSubtype(annotations, "Watermark");
                AssertHasSubtype(annotations, "Stamp");
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void AddsAnnotationsToNonLastPage()
        {
            string path = TemporaryPath();

            try
            {
                using (var builder = new DocumentBuilder())
                {
                    PdfPageBuilder firstPage = builder.AddPage().Text("first page text");
                    _ = builder.AddPage().Text("second page text");

                    firstPage
                        .LinkUrl("https://example.com/first")
                        .Highlight(new PdfColor(1f, 1f, 0f));

                    builder.Save(path);
                }

                var document = new PdfDocument(path);
                Assert.Equal(2, document.GetPageCount());
                IReadOnlyList<PdfAnnotationInfo> annotations = document.GetAnnotations(0);
                Assert.Contains(annotations, annotation =>
                    string.Equals(annotation.Subtype, "Link", StringComparison.OrdinalIgnoreCase)
                    && annotation.Uri is not null
                    && annotation.Uri.Contains("example.com", StringComparison.OrdinalIgnoreCase));
                AssertHasSubtype(annotations, "Highlight");
                Assert.Empty(document.GetAnnotations(1));
            }
            finally
            {
                File.Delete(path);
            }
        }

        private static void AssertHasSubtype(IReadOnlyList<PdfAnnotationInfo> annotations, string subtype)
        {
            Assert.Contains(annotations, annotation =>
                string.Equals(annotation.Subtype, subtype, StringComparison.OrdinalIgnoreCase));
        }

        private static string TemporaryPath()
        {
            return Path.Combine(Path.GetTempPath(), $"pdfoxide-annotations-{Guid.NewGuid():N}.pdf");
        }
    }
}
