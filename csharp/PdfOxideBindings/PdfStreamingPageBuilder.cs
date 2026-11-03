using System;

namespace PdfOxide
{
    /// <summary>
    /// Writes flowing content across same-size pages, inserting page breaks as
    /// needed.
    /// </summary>
    /// <remarks>
    /// The first page is added when the builder is created. Use
    /// <see cref="NewPage"/> to move to a new page with the same size, then
    /// continue adding content.
    /// </remarks>
    public sealed class PdfStreamingPageBuilder
    {
        internal PdfStreamingPageBuilder(DocumentBuilder owner, PdfPageSize size)
        {
            CurrentPage = owner.AddPage(size);
        }

        /// <summary>
        /// Gets the page builder that receives the next content.
        /// </summary>
        public PdfPageBuilder CurrentPage { get; private set; }

        /// <summary>
        /// Adds text at the current cursor position.
        /// </summary>
        /// <param name="text">The text to add.</param>
        /// <param name="style">The font style, or <see langword="null"/> for the default.</param>
        /// <returns>The current streaming builder for method chaining.</returns>
        public PdfStreamingPageBuilder Text(string text, PdfTextStyle? style = null)
        {
            CurrentPage.Text(text, style);
            return this;
        }

        /// <summary>
        /// Adds a word-wrapped paragraph at the current cursor position.
        /// </summary>
        /// <param name="text">The paragraph text.</param>
        /// <returns>The current streaming builder for method chaining.</returns>
        public PdfStreamingPageBuilder Paragraph(string text)
        {
            CurrentPage.Paragraph(text);
            return this;
        }

        /// <summary>
        /// Adds a heading at the current cursor position.
        /// </summary>
        /// <param name="level">Heading level from 1 to 6.</param>
        /// <param name="text">The heading text.</param>
        /// <returns>The current streaming builder for method chaining.</returns>
        public PdfStreamingPageBuilder Heading(int level, string text)
        {
            CurrentPage.Heading(level, text);
            return this;
        }

        /// <summary>
        /// Adds vertical space at the current cursor position.
        /// </summary>
        /// <param name="points">The amount of space in points.</param>
        /// <returns>The current streaming builder for method chaining.</returns>
        public PdfStreamingPageBuilder Space(float points)
        {
            CurrentPage.Space(points);
            return this;
        }

        /// <summary>
        /// Adds a horizontal rule at the current cursor position.
        /// </summary>
        /// <returns>The current streaming builder for method chaining.</returns>
        public PdfStreamingPageBuilder HorizontalRule()
        {
            CurrentPage.HorizontalRule();
            return this;
        }

        /// <summary>
        /// Starts a new page with the same size as the current page and makes
        /// it the target of subsequent content.
        /// </summary>
        /// <returns>The current streaming builder for method chaining.</returns>
        public PdfStreamingPageBuilder NewPage()
        {
            CurrentPage = CurrentPage.NewPageSameSize();
            return this;
        }
    }
}
