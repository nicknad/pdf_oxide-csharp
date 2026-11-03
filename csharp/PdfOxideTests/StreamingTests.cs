using System;
using System.IO;
using PdfOxide;
using Xunit;

namespace PdfOxideTests
{
    /// <summary>
    /// Tests for streaming tables, same-size page breaks, and the streaming
    /// page builder.
    /// </summary>
    public class StreamingTests
    {
        [Fact]
        public void StreamingTableSpansPagesAndRepeatsHeader()
        {
            string path = TemporaryPath();

            try
            {
                using (var builder = new DocumentBuilder())
                {
                    var options = new PdfStreamingTableOptions
                    {
                        Columns =
                        [
                            new PdfStreamingColumn { Header = "HDR-ID", Width = 50f },
                            new PdfStreamingColumn { Header = "HDR-NAME", Width = 180f },
                            new PdfStreamingColumn { Header = "HDR-VALUE", Width = 100f },
                        ],
                        RepeatHeader = true,
                    };

                    PdfPageBuilder page = builder.AddPage(PdfPageSize.A4);
                    PdfStreamingTable table = page.StreamingTable(options);
                    for (int index = 0; index < 60; index++)
                    {
                        table.AddRow(
                            $"row-{index:D3}",
                            $"name-{index}",
                            $"value-{index}");
                    }
                    table.Finish();

                    builder.Save(path);
                }

                var document = new PdfDocument(path);
                Assert.True(document.GetPageCount() > 1);
                string text = document.ExtractText();
                Assert.Contains("row-000", text, StringComparison.Ordinal);
                Assert.Contains("row-059", text, StringComparison.Ordinal);

                int headerCount = text.Split(["HDR-ID"], StringSplitOptions.None).Length - 1;
                Assert.True(headerCount >= 2);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void StreamingTableSupportsRowspan()
        {
            string path = TemporaryPath();

            try
            {
                using (var builder = new DocumentBuilder())
                {
                    var options = new PdfStreamingTableOptions
                    {
                        Columns =
                        [
                            new PdfStreamingColumn { Header = "A", Width = 100f },
                            new PdfStreamingColumn { Header = "B", Width = 100f },
                        ],
                        MaxRowspan = 2,
                    };

                    PdfStreamingTable table = builder.AddPage(PdfPageSize.A4).StreamingTable(options);
                    table.AddRow(new PdfStreamingCell[]
                    {
                        new("span", 2),
                        new("adjacent", 1),
                    });
                    table.AddRow(new PdfStreamingCell[]
                    {
                        new("next", 1),
                        new("next-two", 1),
                    });
                    table.Finish();

                    builder.Save(path);
                }

                var document = new PdfDocument(path);
                string text = document.ExtractText();
                Assert.Contains("span", text, StringComparison.Ordinal);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void NewPageSameSizeAddsMatchingPage()
        {
            string path = TemporaryPath();

            try
            {
                using (var builder = new DocumentBuilder())
                {
                    builder.AddPage()
                        .Paragraph("page-one")
                        .NewPageSameSize()
                        .Paragraph("page-two");

                    builder.Save(path);
                }

                var document = new PdfDocument(path);
                Assert.Equal(2, document.GetPageCount());
                Assert.Contains("page-one", document.ExtractText(0), StringComparison.Ordinal);
                Assert.Contains("page-two", document.ExtractText(1), StringComparison.Ordinal);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void StreamingPageBuilderInsertsPageBreaks()
        {
            string path = TemporaryPath();

            try
            {
                using (var builder = new DocumentBuilder())
                {
                    builder.BeginStreamingPage(PdfPageSize.A4)
                        .Paragraph("stream-one")
                        .NewPage()
                        .Paragraph("stream-two");

                    builder.Save(path);
                }

                var document = new PdfDocument(path);
                Assert.Equal(2, document.GetPageCount());
                Assert.Contains("stream-one", document.ExtractText(0), StringComparison.Ordinal);
                Assert.Contains("stream-two", document.ExtractText(1), StringComparison.Ordinal);
            }
            finally
            {
                File.Delete(path);
            }
        }

        private static string TemporaryPath()
        {
            return Path.Combine(Path.GetTempPath(), $"pdfoxide-streaming-{Guid.NewGuid():N}.pdf");
        }
    }
}
