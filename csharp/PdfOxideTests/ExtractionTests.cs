using PdfOxide;

namespace PdfOxideTests
{
    /// <summary>
    /// Tests PDF text extraction and Markdown conversion using generated fixtures.
    /// </summary>
    public class ExtractionTests
    {
        [Fact]
        public void GeneratedPdfSupportsTextAndMarkdownExtraction()
        {
            const string content = "Self-contained extraction fixture";
            string path = CreateTemporaryPdf(content);

            try
            {
                var document = new PdfDocument(path);

                Assert.Equal(1, document.GetPageCount());
                Assert.Contains(content, document.ExtractText(), StringComparison.Ordinal);
                Assert.False(string.IsNullOrWhiteSpace(document.ToMarkdown()));
                Assert.False(string.IsNullOrWhiteSpace(document.ToMarkdown(0)));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void ExtractsTextFromSeveralGeneratedPdfs()
        {
            string[] fixtures =
            [
                "First deterministic fixture",
                "Second deterministic fixture",
                "Third deterministic fixture"
            ];

            foreach (string content in fixtures)
            {
                string path = CreateTemporaryPdf(content);
                try
                {
                    var document = new PdfDocument(path);
                    Assert.Equal(1, document.GetPageCount());
                    Assert.Contains(content, document.ExtractText(), StringComparison.Ordinal);
                }
                finally
                {
                    File.Delete(path);
                }
            }
        }

        private static string CreateTemporaryPdf(string content)
        {
            string path = Path.Combine(Path.GetTempPath(), $"pdfoxide-{Guid.NewGuid():N}.pdf");
            PdfConverter.ConvertText(content, path);
            return path;
        }
    }
}
