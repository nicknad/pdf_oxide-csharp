using System;
using System.IO;
using Xunit;
using PdfOxide;

namespace PdfOxideTests
{
    /// <summary>
    /// Tests for PDF creation and conversion functionality.
    /// </summary>
    /// <remarks>
    /// These tests verify that the various PDF creation methods work correctly
    /// by creating new PDFs and then opening them to verify their contents.
    /// </remarks>
    public class ConversionTests
    {
        /// <summary>
        /// Tests creating a PDF from Markdown format.
        /// </summary>
        [Fact]
        public void TestMarkdownToPdf()
        {
            string md = "# Heading\n\n* Item 1\n* Item 2";
            string path = "test_md.pdf";
            
            // Create PDF from markdown
            PdfConverter.ConvertMarkdown(md, path);
            
            // Verify it was created and is valid
            Assert.True(File.Exists(path));
            var doc = new PdfDocument(path);
            Assert.True(doc.GetPageCount() > 0);
            
            // Clean up
            File.Delete(path);
        }

        /// <summary>
        /// Tests creating a PDF from plain text.
        /// </summary>
        [Fact]
        public void TestTextToPdf()
        {
            string text = "Simple text content";
            string path = "test_text.pdf";
            
            // Create PDF from plain text
            PdfConverter.ConvertText(text, path);
            
            // Verify it was created and is valid
            Assert.True(File.Exists(path));
            var doc = new PdfDocument(path);
            Assert.True(doc.GetPageCount() > 0);
            
            // Clean up
            File.Delete(path);
        }

        /// <summary>
        /// Tests the DocumentBuilder API for programmatic PDF creation.
        /// </summary>
        [Fact]
        public void TestDocumentBuilder()
        {
            string path = "test_builder.pdf";
            
            // Use fluent API to build PDF with multiple pages
            using (var builder = new DocumentBuilder())
            {
                builder.AddPage("Page 1 content");
                builder.AddPage("Page 2 content");
                builder.Save(path);
            }

            // Verify the PDF has the expected page count
            Assert.True(File.Exists(path));
            var doc = new PdfDocument(path);
            Assert.Equal(2, doc.GetPageCount());
            
            // Clean up
            File.Delete(path);
        }
    }
}