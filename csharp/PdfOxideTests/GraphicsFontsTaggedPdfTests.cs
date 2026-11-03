using System;
using System.IO;
using System.Text;
using PdfOxide;
using Xunit;

namespace PdfOxideTests
{
    /// <summary>
    /// Tests for vector graphics, embedded fonts, and tagged PDF/UA support.
    /// </summary>
    public class GraphicsFontsTaggedPdfTests
    {
        [Fact]
        public void BuildsPagesWithVectorShapes()
        {
            string path = TemporaryPath();

            try
            {
                using (var builder = new DocumentBuilder())
                {
                    PdfPageBuilder page = builder.AddPage(PdfPageSize.A4);

                    page.Rectangle(new PdfRect(50f, 700f, 100f, 40f));
                    page.FilledRectangle(new PdfRect(170f, 700f, 100f, 40f), new PdfColor(0.2f, 0.4f, 0.8f));
                    page.Line(50f, 680f, 270f, 680f);
                    page.StrokeRectangle(
                        new PdfRect(50f, 610f, 100f, 40f),
                        new PdfLineStyle
                        {
                            Width = 2f,
                            Color = new PdfColor(0.8f, 0.1f, 0.1f),
                            DashPattern = [4f, 2f],
                        });
                    page.StrokeLine(
                        170f,
                        610f,
                        270f,
                        650f,
                        new PdfLineStyle
                        {
                            Width = 1.5f,
                            Color = new PdfColor(0.1f, 0.6f, 0.2f),
                            DashPattern = [3f, 3f],
                        });
                    page.Circle(
                        100f,
                        540f,
                        35f,
                        new PdfLineStyle { Width = 1f, Color = new PdfColor(0f, 0f, 1f) },
                        new PdfColor(0.9f, 0.9f, 0.2f));
                    page.Ellipse(
                        220f,
                        540f,
                        50f,
                        25f,
                        new PdfLineStyle(),
                        new PdfColor(0.2f, 0.8f, 0.8f));
                    page.Polygon(
                        [
                            new PdfPoint(50f, 450f),
                            new PdfPoint(130f, 490f),
                            new PdfPoint(90f, 420f),
                        ],
                        new PdfLineStyle(),
                        new PdfColor(0.5f, 0.2f, 0.7f));
                    page.Arc(220f, 450f, 40f, 0f, 180f, new PdfLineStyle());
                    page.BezierCurve(
                        50f,
                        370f,
                        110f,
                        430f,
                        190f,
                        310f,
                        250f,
                        370f,
                        new PdfLineStyle(),
                        new PdfColor(0.3f, 0.3f, 0.3f));

                    builder.Save(path);
                }

                var document = new PdfDocument(path);
                Assert.True(document.ExtractPaths(0).Count >= 8);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void BuildsPageWithGenericPath()
        {
            string path = TemporaryPath();

            try
            {
                using (var builder = new DocumentBuilder())
                {
                    var pathShape = new PdfGraphicsPath()
                        .MoveTo(80f, 600f)
                        .LineTo(180f, 600f)
                        .CurveTo(220f, 650f, 260f, 550f, 300f, 600f)
                        .Close();

                    builder.AddPage()
                        .Path(pathShape, new PdfPathStyle
                        {
                            StrokeColor = new PdfColor(0f, 0f, 0f),
                            StrokeWidth = 2f,
                            FillColor = new PdfColor(0.7f, 0.8f, 0.9f),
                        });

                    builder.Save(path);
                }

                var document = new PdfDocument(path);
                Assert.True(document.ExtractPaths(0).Count >= 1);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void RegistersBundledAndEmbeddedFonts()
        {
            string path = TemporaryPath();
            string fontPath = TemporaryPath("ttf");

            try
            {
                byte[] fontData;
                using (Stream? stream = typeof(DocumentBuilder).Assembly
                    .GetManifestResourceStream("PdfOxide.Fonts.DejaVuSans.ttf"))
                {
                    Assert.NotNull(stream);
                    using var memory = new MemoryStream();
                    stream!.CopyTo(memory);
                    fontData = memory.ToArray();
                }

                File.WriteAllBytes(fontPath, fontData);

                using (var builder = new DocumentBuilder())
                {
                    builder.RegisterBundledFonts();
                    builder.RegisterFont("MyFont", fontData);
                    builder.RegisterFont("MyFontFile", fontPath);

                    builder.AddPage()
                        .Text("héllo Ω", new PdfTextStyle { Font = "DejaVuSans", Size = 14f });
                    builder.AddPage()
                        .Text("Ω from bytes", new PdfTextStyle { Font = "MyFont", Size = 14f });
                    builder.AddPage()
                        .Text("Ω from file", new PdfTextStyle { Font = "MyFontFile", Size = 14f });

                    builder.Save(path);
                }

                var document = new PdfDocument(path);
                string text = document.ExtractText();
                Assert.Contains("héllo", text, StringComparison.Ordinal);
                Assert.Contains("Ω", text, StringComparison.Ordinal);
                Assert.Contains("Ω from bytes", text, StringComparison.Ordinal);
                Assert.Contains("Ω from file", text, StringComparison.Ordinal);
            }
            finally
            {
                File.Delete(path);
                File.Delete(fontPath);
            }
        }

        [Fact]
        public void WritesTaggedPdfUaStructure()
        {
            string path = TemporaryPath();

            try
            {
                using (var builder = new DocumentBuilder())
                {
                    builder.SetTaggedPdfUa1()
                        .SetLanguage("en-US")
                        .AddRoleMap("MyHeading", "H1");

                    builder.AddPage().Paragraph("tagged page content");
                    builder.Save(path);
                }

                string raw = Encoding.Latin1.GetString(File.ReadAllBytes(path));
                Assert.Contains("/StructTreeRoot", raw, StringComparison.Ordinal);
                Assert.Contains("/MarkInfo", raw, StringComparison.Ordinal);
                Assert.Contains("/Lang", raw, StringComparison.Ordinal);
                Assert.Contains("en-US", raw, StringComparison.Ordinal);
                Assert.Contains("/RoleMap", raw, StringComparison.Ordinal);
                Assert.Contains("MyHeading", raw, StringComparison.Ordinal);

                var document = new PdfDocument(path);
                Assert.Contains("tagged page content", document.ExtractText(), StringComparison.Ordinal);
            }
            finally
            {
                File.Delete(path);
            }
        }

        private static string TemporaryPath(string extension = "pdf")
        {
            return Path.Combine(Path.GetTempPath(), $"pdfoxide-graphics-{Guid.NewGuid():N}.{extension}");
        }
    }
}
