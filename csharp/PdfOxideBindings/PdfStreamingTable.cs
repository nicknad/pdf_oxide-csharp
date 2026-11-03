using System;
using System.Collections.Generic;

namespace PdfOxide
{
    /// <summary>
    /// Appends rows to a streaming table opened from a
    /// <see cref="PdfPageBuilder"/>. Rows are committed to the document when
    /// <see cref="Finish"/> is called.
    /// </summary>
    /// <remarks>
    /// A streaming table paginates automatically and can repeat its header on
    /// every page it spans. Add rows with <see cref="AddRow(string[])"/> or
    /// <see cref="AddRow(IEnumerable{PdfStreamingCell})"/>, then call
    /// <see cref="Finish"/> exactly once.
    /// </remarks>
    public sealed class PdfStreamingTable
    {
        private readonly DocumentBuilder _owner;
        private readonly int _pageIndex;
        private readonly PdfPageBuilder _page;
        private readonly PdfStreamingTableOptions _options;
        private bool _finished;

        internal PdfStreamingTable(
            DocumentBuilder owner,
            int pageIndex,
            PdfPageBuilder page,
            PdfStreamingTableOptions options)
        {
            _owner = owner;
            _pageIndex = pageIndex;
            _page = page;
            _options = options;
            _owner.BeginStreamingTable(pageIndex, options);
        }

        /// <summary>
        /// Appends a row with plain-text cells.
        /// </summary>
        /// <param name="cells">The cell text, in column order.</param>
        /// <returns>The current table for method chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="cells"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the number of cells does not match the columns or a cell is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the table has already been finished.</exception>
        public PdfStreamingTable AddRow(params string[] cells)
        {
            ThrowIfFinished();
            ArgumentNullException.ThrowIfNull(cells);
            if (cells.Length == 0)
            {
                throw new ArgumentException("A row must contain at least one cell.", nameof(cells));
            }

            ValidateColumnCount(cells.Length);
            _owner.AddStreamingTableRow(_pageIndex, cells, null);
            return this;
        }

        /// <summary>
        /// Appends a row of cells that may span multiple rows.
        /// </summary>
        /// <param name="cells">The cells, in column order.</param>
        /// <returns>The current table for method chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="cells"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the number of cells does not match the columns or a cell is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the table has already been finished.</exception>
        public PdfStreamingTable AddRow(IEnumerable<PdfStreamingCell> cells)
        {
            ThrowIfFinished();
            ArgumentNullException.ThrowIfNull(cells);

            var texts = new List<string>();
            var rowspans = new List<int>();
            foreach (PdfStreamingCell? cell in cells)
            {
                if (cell is null)
                {
                    throw new ArgumentException("Table cells must not be null.", nameof(cells));
                }
                ArgumentNullException.ThrowIfNull(cell.Text);
                if (cell.RowSpan < 1)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(cells), cell.RowSpan, "Cell rowspan must be at least 1.");
                }

                texts.Add(cell.Text);
                rowspans.Add(cell.RowSpan);
            }

            if (texts.Count == 0)
            {
                throw new ArgumentException("A row must contain at least one cell.", nameof(cells));
            }

            ValidateColumnCount(texts.Count);
            _owner.AddStreamingTableRow(_pageIndex, texts.ToArray(), rowspans.ToArray());
            return this;
        }

        /// <summary>
        /// Finishes the table and returns the page builder that owns it.
        /// </summary>
        /// <returns>The page builder for further content.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the table has already been finished.</exception>
        public PdfPageBuilder Finish()
        {
            if (_finished)
            {
                throw new InvalidOperationException("The streaming table has already been finished.");
            }

            _finished = true;
            _owner.EndStreamingTable(_pageIndex);
            return _page;
        }

        private void ValidateColumnCount(int count)
        {
            int expected = _options.Columns.Length;
            if (count != expected)
            {
                throw new ArgumentException(
                    $"A row must contain exactly {expected} cells.", nameof(count));
            }
        }

        private void ThrowIfFinished()
        {
            if (_finished)
            {
                throw new InvalidOperationException("The streaming table has already been finished.");
            }
        }
    }
}
