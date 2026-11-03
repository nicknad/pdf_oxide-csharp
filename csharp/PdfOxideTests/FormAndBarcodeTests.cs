using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PdfOxide;
using Xunit;

namespace PdfOxideTests
{
    /// <summary>
    /// Tests for form-field creation and editing plus QR and 1-D barcodes.
    /// </summary>
    public class FormAndBarcodeTests
    {
        [Fact]
        public void BuildsFormFieldsAndBarcodes()
        {
            string path = TemporaryPath();

            try
            {
                using (var builder = new DocumentBuilder())
                {
                    builder.AddPage()
                        .TextField("name", new PdfRect(72f, 700f, 220f, 24f), "default")
                        .Checkbox("agree", new PdfRect(72f, 660f, 16f, 16f), true)
                        .ComboBox("country", new PdfRect(72f, 620f, 160f, 24f), ["US", "CA"], "US")
                        .ListBox("tags", new PdfRect(72f, 560f, 160f, 50f), ["a", "b", "c"], "a")
                        .RadioGroup(
                            "choice",
                            [
                                new PdfRadioOption("one", new PdfRect(72f, 520f, 16f, 16f)),
                                new PdfRadioOption("two", new PdfRect(100f, 520f, 16f, 16f)),
                            ],
                            "one")
                        .PushButton("submit", new PdfRect(72f, 480f, 100f, 28f), "Submit")
                        .SignatureField("sig", new PdfRect(72f, 430f, 180f, 40f))
                        .QrCode("https://example.com", new PdfRect(320f, 700f, 100f, 100f))
                        .Barcode(PdfBarcodeType.Code128, "ABC123", new PdfRect(320f, 620f, 200f, 60f));

                    builder.Save(path);
                }

                var document = new PdfDocument(path);
                Assert.True(
                    document.ExtractImages(0).Count >= 2,
                    "Expected the QR code and barcode to be embedded as images.");

                using var editor = PdfEditor.Open(path);
                IReadOnlyList<PdfFormFieldInfo> fields = editor.GetFormFields();
                Assert.True(fields.Count >= 7, $"Expected at least 7 form fields, found {fields.Count}.");

                var names = fields.Select(field => field.Name).ToHashSet(StringComparer.Ordinal);
                foreach (string expected in new[]
                         {
                             "name", "agree", "country", "tags", "choice", "submit", "sig",
                         })
                {
                    Assert.Contains(expected, names);
                }

                Assert.All(fields, field =>
                {
                    Assert.False(string.IsNullOrEmpty(field.Name));
                    Assert.False(string.IsNullOrEmpty(field.Type));
                    Assert.False(string.IsNullOrEmpty(field.Value));
                });
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void SetsAndFlattensFormFields()
        {
            string originalPath = TemporaryPath();
            string filledPath = TemporaryPath();
            string flattenedPath = TemporaryPath();

            try
            {
                using (var builder = new DocumentBuilder())
                {
                    builder.AddPage()
                        .TextField("name", new PdfRect(72f, 700f, 220f, 24f), "default")
                        .Checkbox("agree", new PdfRect(72f, 660f, 16f, 16f), true);

                    builder.Save(originalPath);
                }

                using (var editor = PdfEditor.Open(originalPath))
                {
                    IReadOnlyList<PdfFormFieldInfo> fields = editor.GetFormFields();
                    Assert.Contains(fields, field => field.Name == "name");

                    editor.SetFormFieldValue("name", "filled");
                    editor.Save(filledPath);
                }

                using (var editor = PdfEditor.Open(filledPath))
                {
                    PdfFormFieldInfo nameField = Assert.Single(
                        editor.GetFormFields(), field => field.Name == "name");
                    Assert.Contains("filled", nameField.Value, StringComparison.Ordinal);

                    editor.FlattenForms();
                    editor.Save(flattenedPath);
                }

                using (var editor = PdfEditor.Open(flattenedPath))
                {
                    Assert.Empty(editor.GetFormFields());
                }
            }
            finally
            {
                File.Delete(originalPath);
                File.Delete(filledPath);
                File.Delete(flattenedPath);
            }
        }

        [Fact]
        public void FlattensAnnotations()
        {
            string originalPath = TemporaryPath();
            string flattenedPath = TemporaryPath();

            try
            {
                using (var builder = new DocumentBuilder())
                {
                    builder.AddPage()
                        .Text("Highlight this text.")
                        .Highlight(new PdfColor(1f, 1f, 0f));

                    builder.Save(originalPath);
                }

                var document = new PdfDocument(originalPath);
                Assert.NotEmpty(document.GetAnnotations(0));

                using (var editor = PdfEditor.Open(originalPath))
                {
                    editor.FlattenAnnotations(0);
                    editor.Save(flattenedPath);
                }

                var flattened = new PdfDocument(flattenedPath);
                Assert.Empty(flattened.GetAnnotations(0));
                Assert.Contains("Highlight this text.", flattened.ExtractText(), StringComparison.Ordinal);
            }
            finally
            {
                File.Delete(originalPath);
                File.Delete(flattenedPath);
            }
        }

        private static string TemporaryPath()
        {
            return Path.Combine(
                Path.GetTempPath(),
                $"pdfoxide-forms-{Guid.NewGuid():N}.pdf");
        }
    }
}
