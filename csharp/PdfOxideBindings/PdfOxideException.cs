using System;

namespace PdfOxide
{
    /// <summary>
    /// Represents an error reported by the native PDF Oxide bindings.
    /// </summary>
    public sealed class PdfOxideException : InvalidOperationException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PdfOxideException"/> class.
        /// </summary>
        public PdfOxideException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PdfOxideException"/> class with a message.
        /// </summary>
        /// <param name="message">The error message.</param>
        public PdfOxideException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PdfOxideException"/> class with a message and inner exception.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="innerException">The exception that caused this error.</param>
        public PdfOxideException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
