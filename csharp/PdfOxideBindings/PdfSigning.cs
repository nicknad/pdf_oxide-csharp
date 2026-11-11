using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Formats.Asn1;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace PdfOxide
{
    /// <summary>
    /// Holds signing credentials (a certificate and its private key) for use with
    /// <see cref="PdfSigning"/>.
    /// </summary>
    public sealed unsafe class PdfSigningCertificate : IDisposable
    {
        private IntPtr _handle;

        private PdfSigningCertificate(IntPtr handle)
        {
            _handle = handle;
        }

        /// <summary>
        /// Loads signing credentials from a PKCS#12 (.p12/.pfx) container.
        /// </summary>
        /// <param name="pkcs12Data">The PKCS#12 bytes.</param>
        /// <param name="password">The container password, if any.</param>
        /// <returns>The loaded signing credentials.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="pkcs12Data"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="pkcs12Data"/> is empty.</exception>
        /// <exception cref="PdfOxideException">Thrown when the credentials cannot be loaded.</exception>
        public static PdfSigningCertificate FromPkcs12(byte[] pkcs12Data, string? password = null)
        {
            ArgumentNullException.ThrowIfNull(pkcs12Data);
            if (pkcs12Data.Length == 0)
            {
                throw new ArgumentException("PKCS#12 data must not be empty.", nameof(pkcs12Data));
            }

            int errorCode = 0;
            IntPtr handle;
            fixed (byte* pData = pkcs12Data)
            fixed (byte* pPassword = Utf8(password ?? string.Empty))
            {
                handle = UpstreamNativeMethods.pdf_certificate_load_from_bytes(
                    pData,
                    pkcs12Data.Length,
                    pPassword,
                    &errorCode);
            }

            if (handle == IntPtr.Zero)
            {
                throw new PdfOxideException(
                    $"Native pdf_certificate_load_from_bytes failed (error code {errorCode}).");
            }

            return new PdfSigningCertificate(handle);
        }

        /// <summary>
        /// Loads signing credentials from PEM-encoded certificate and private key strings.
        /// </summary>
        /// <param name="certificatePem">The PEM-encoded X.509 certificate.</param>
        /// <param name="privateKeyPem">The PEM-encoded private key.</param>
        /// <returns>The loaded signing credentials.</returns>
        /// <exception cref="ArgumentException">Thrown when either PEM value is null or empty.</exception>
        /// <exception cref="PdfOxideException">Thrown when the credentials cannot be loaded.</exception>
        public static PdfSigningCertificate FromPem(string certificatePem, string privateKeyPem)
        {
            ArgumentException.ThrowIfNullOrEmpty(certificatePem);
            ArgumentException.ThrowIfNullOrEmpty(privateKeyPem);

            int errorCode = 0;
            IntPtr handle;
            fixed (byte* pCert = Utf8(certificatePem))
            fixed (byte* pKey = Utf8(privateKeyPem))
            {
                handle = UpstreamNativeMethods.pdf_certificate_load_from_pem(
                    pCert,
                    pKey,
                    &errorCode);
            }

            if (handle == IntPtr.Zero)
            {
                throw new PdfOxideException(
                    $"Native pdf_certificate_load_from_pem failed (error code {errorCode}).");
            }

            return new PdfSigningCertificate(handle);
        }

        internal IntPtr Handle
        {
            get
            {
                ObjectDisposedException.ThrowIf(_handle == IntPtr.Zero, this);
                return _handle;
            }
        }

        /// <summary>
        /// Releases the native signing credentials.
        /// </summary>
        public void Dispose()
        {
            if (_handle != IntPtr.Zero)
            {
                UpstreamNativeMethods.pdf_certificate_free(_handle);
                _handle = IntPtr.Zero;
            }

            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Finalizer that releases the native signing credentials.
        /// </summary>
        ~PdfSigningCertificate()
        {
            Dispose();
        }

        private static byte[] Utf8(string value)
        {
            return Encoding.UTF8.GetBytes(value + "\0");
        }
    }

    /// <summary>
    /// Optional metadata embedded in a PDF signature.
    /// </summary>
    public sealed record PdfSigningOptions
    {
        /// <summary>
        /// Gets the reason for signing.
        /// </summary>
        public string? Reason { get; init; }

        /// <summary>
        /// Gets the signing location.
        /// </summary>
        public string? Location { get; init; }
    }

    /// <summary>
    /// Describes a signature embedded in a PDF.
    /// </summary>
    /// <param name="SignerName">The signer name, if present.</param>
    /// <param name="SigningTime">The signing time, if present.</param>
    /// <param name="Reason">The signing reason, if present.</param>
    /// <param name="Location">The signing location, if present.</param>
    /// <param name="HasTimestamp">Whether the signature carries a timestamp token.</param>
    public sealed record PdfSignatureInfo(
        string? SignerName,
        DateTimeOffset? SigningTime,
        string? Reason,
        string? Location,
        bool HasTimestamp);

    /// <summary>
    /// Signs and verifies PDF documents, and reads their embedded signatures.
    /// </summary>
    public static unsafe class PdfSigning
    {
        /// <summary>
        /// Signs a PDF using the supplied credentials and returns the signed PDF.
        /// </summary>
        /// <param name="pdf">The PDF bytes to sign.</param>
        /// <param name="certificate">The signing credentials.</param>
        /// <param name="options">Optional signing metadata.</param>
        /// <returns>The signed PDF bytes.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="pdf"/> or <paramref name="certificate"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="pdf"/> is empty.</exception>
        /// <exception cref="PdfOxideException">Thrown when signing fails.</exception>
        public static byte[] Sign(
            byte[] pdf,
            PdfSigningCertificate certificate,
            PdfSigningOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(pdf);
            if (pdf.Length == 0)
            {
                throw new ArgumentException("PDF data must not be empty.", nameof(pdf));
            }

            ArgumentNullException.ThrowIfNull(certificate);
            IntPtr certHandle = certificate.Handle;

            byte[]? reasonBytes = options?.Reason is null ? null : Utf8(options.Reason);
            byte[]? locationBytes = options?.Location is null ? null : Utf8(options.Location);

            int errorCode = 0;
            nuint outLength = 0;
            byte* data;
            fixed (byte* pPdf = pdf)
            fixed (byte* pReason = reasonBytes)
            fixed (byte* pLocation = locationBytes)
            {
                data = UpstreamNativeMethods.pdf_sign_bytes(
                    pPdf,
                    (nuint)pdf.Length,
                    certHandle,
                    pReason,
                    pLocation,
                    &outLength,
                    &errorCode);
            }

            if (data == null)
            {
                throw new PdfOxideException($"Native pdf_sign_bytes failed (error code {errorCode}).");
            }

            try
            {
                return CopyBytes(data, outLength);
            }
            finally
            {
                UpstreamNativeMethods.free_bytes(data);
            }
        }

        /// <summary>
        /// Signs a PDF at a PAdES baseline level and returns the signed PDF.
        /// </summary>
        /// <param name="pdf">The PDF bytes to sign.</param>
        /// <param name="certificate">The signing credentials.</param>
        /// <param name="level">The PAdES level: 0 = B-B, 1 = B-T, 2 = B-LT.</param>
        /// <param name="tsaUrl">The RFC 3161 timestamp authority URL, required for levels above B-B.</param>
        /// <param name="options">Optional signing metadata.</param>
        /// <returns>The signed PDF bytes.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="pdf"/> or <paramref name="certificate"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="pdf"/> is empty.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="level"/> is outside the supported range.</exception>
        /// <exception cref="PdfOxideException">Thrown when signing fails.</exception>
        [SuppressMessage(
            "Design",
            "CA1054:URI-like parameters should not be strings",
            Justification = "The TSA endpoint is passed directly to the native UTF-8 API.")]
        public static byte[] SignPades(
            byte[] pdf,
            PdfSigningCertificate certificate,
            int level = 0,
            string? tsaUrl = null,
            PdfSigningOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(pdf);
            if (pdf.Length == 0)
            {
                throw new ArgumentException("PDF data must not be empty.", nameof(pdf));
            }

            ArgumentNullException.ThrowIfNull(certificate);
            ArgumentOutOfRangeException.ThrowIfNegative(level);
            if (level > 2)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(level),
                    level,
                    "PAdES levels above B-LT are not supported.");
            }

            IntPtr certHandle = certificate.Handle;

            byte[]? tsaBytes = tsaUrl is null ? null : Utf8(tsaUrl);
            byte[]? reasonBytes = options?.Reason is null ? null : Utf8(options.Reason);
            byte[]? locationBytes = options?.Location is null ? null : Utf8(options.Location);

            int errorCode = 0;
            nuint outLength = 0;
            byte* data;
            fixed (byte* pPdf = pdf)
            fixed (byte* pTsa = tsaBytes)
            fixed (byte* pReason = reasonBytes)
            fixed (byte* pLocation = locationBytes)
            {
                data = UpstreamNativeMethods.pdf_sign_bytes_pades(
                    pPdf,
                    (nuint)pdf.Length,
                    certHandle,
                    level,
                    pTsa,
                    pReason,
                    pLocation,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    0,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    0,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    0,
                    &outLength,
                    &errorCode);
            }

            if (data == null)
            {
                throw new PdfOxideException(
                    $"Native pdf_sign_bytes_pades failed (error code {errorCode}).");
            }

            try
            {
                return CopyBytes(data, outLength);
            }
            finally
            {
                UpstreamNativeMethods.free_bytes(data);
            }
        }

        /// <summary>
        /// Gets the number of signatures embedded in a PDF.
        /// </summary>
        /// <param name="pdf">The PDF bytes.</param>
        /// <returns>The signature count.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="pdf"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="pdf"/> is empty.</exception>
        /// <exception cref="PdfOxideException">Thrown when the document cannot be opened.</exception>
        public static int GetSignatureCount(byte[] pdf)
        {
            IntPtr document = OpenDocument(pdf);
            try
            {
                int errorCode = 0;
                int count = UpstreamNativeMethods.pdf_document_get_signature_count(
                    document,
                    &errorCode);
                ThrowIfFailed(count, errorCode, "pdf_document_get_signature_count");
                return count == 0 ? ParseBareSignatures(pdf).Count : count;
            }
            finally
            {
                UpstreamNativeMethods.pdf_document_free(document);
            }
        }

        /// <summary>
        /// Reads metadata for every signature embedded in a PDF.
        /// </summary>
        /// <param name="pdf">The PDF bytes.</param>
        /// <returns>The signature metadata, in document order.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="pdf"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="pdf"/> is empty.</exception>
        /// <exception cref="PdfOxideException">Thrown when the document or a signature cannot be read.</exception>
        public static IReadOnlyList<PdfSignatureInfo> GetSignatures(byte[] pdf)
        {
            IntPtr document = OpenDocument(pdf);
            try
            {
                int errorCode = 0;
                int count = UpstreamNativeMethods.pdf_document_get_signature_count(
                    document,
                    &errorCode);
                ThrowIfFailed(count, errorCode, "pdf_document_get_signature_count");

                if (count == 0)
                {
                    List<BareSignature> bare = ParseBareSignatures(pdf);
                    List<PdfSignatureInfo> bareInfo =
                    [
                        with(capacity: bare.Count),
                        .. bare.Select(signature => new PdfSignatureInfo(
                            signature.SignerName,
                            signature.SigningTime,
                            signature.Reason,
                            signature.Location,
                            HasTimestamp: false)),
                    ];

                    return bareInfo;
                }

                var signatures = new List<PdfSignatureInfo>(count);
                for (int index = 0; index < count; index++)
                {
                    errorCode = 0;
                    IntPtr signature = UpstreamNativeMethods.pdf_document_get_signature(
                        document,
                        index,
                        &errorCode);
                    if (signature == IntPtr.Zero)
                    {
                        throw new PdfOxideException(
                            $"Native pdf_document_get_signature failed at index {index} (error code {errorCode}).");
                    }

                    try
                    {
                        errorCode = 0;
                        string? signerName = ReadString(
                            UpstreamNativeMethods.pdf_signature_get_signer_name(signature, &errorCode));

                        errorCode = 0;
                        long signingSeconds = UpstreamNativeMethods.pdf_signature_get_signing_time(
                            signature,
                            &errorCode);
                        DateTimeOffset? signingTime = signingSeconds > 0
                            ? DateTimeOffset.FromUnixTimeSeconds(signingSeconds)
                            : null;

                        errorCode = 0;
                        string? reason = ReadString(
                            UpstreamNativeMethods.pdf_signature_get_signing_reason(signature, &errorCode));

                        errorCode = 0;
                        string? location = ReadString(
                            UpstreamNativeMethods.pdf_signature_get_signing_location(signature, &errorCode));

                        // The upstream implementation currently reports unsupported for this
                        // accessor; treat a non-zero value as "no timestamp" rather than failing.
                        errorCode = 0;
                        bool hasTimestamp = UpstreamNativeMethods.pdf_signature_has_timestamp(
                            signature,
                            &errorCode) != 0;

                        signatures.Add(new PdfSignatureInfo(
                            signerName,
                            signingTime,
                            reason,
                            location,
                            hasTimestamp));
                    }
                    finally
                    {
                        UpstreamNativeMethods.pdf_signature_free(signature);
                    }
                }

                return signatures;
            }
            finally
            {
                UpstreamNativeMethods.pdf_document_free(document);
            }
        }

        /// <summary>
        /// Verifies every signature embedded in a PDF.
        /// </summary>
        /// <param name="pdf">The PDF bytes.</param>
        /// <returns><see langword="true"/> when the document has at least one signature and all verify; otherwise <see langword="false"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="pdf"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="pdf"/> is empty.</exception>
        /// <exception cref="PdfOxideException">Thrown when the document cannot be opened.</exception>
        public static bool Verify(byte[] pdf)
        {
            IntPtr document = OpenDocument(pdf);
            try
            {
                int errorCode = 0;
                int count = UpstreamNativeMethods.pdf_document_get_signature_count(
                    document,
                    &errorCode);
                ThrowIfFailed(count, errorCode, "pdf_document_get_signature_count");
                if (count == 0)
                {
                    List<BareSignature> bare = ParseBareSignatures(pdf);
                    if (bare.Count == 0)
                    {
                        return false;
                    }

                    foreach (BareSignature signature in bare)
                    {
                        if (!VerifyBareSignature(signature, pdf))
                        {
                            return false;
                        }
                    }

                    return true;
                }

                for (int index = 0; index < count; index++)
                {
                    errorCode = 0;
                    IntPtr signature = UpstreamNativeMethods.pdf_document_get_signature(
                        document,
                        index,
                        &errorCode);
                    if (signature == IntPtr.Zero)
                    {
                        throw new PdfOxideException(
                            $"Native pdf_document_get_signature failed at index {index} (error code {errorCode}).");
                    }

                    try
                    {
                        fixed (byte* pPdf = pdf)
                        {
                            errorCode = 0;
                            int valid = UpstreamNativeMethods.pdf_signature_verify_detached(
                                signature,
                                pPdf,
                                (nuint)pdf.Length,
                                &errorCode);
                            if (valid != 1)
                            {
                                return false;
                            }
                        }
                    }
                    finally
                    {
                        UpstreamNativeMethods.pdf_signature_free(signature);
                    }
                }

                return true;
            }
            finally
            {
                UpstreamNativeMethods.pdf_document_free(document);
            }
        }

        private sealed class BareSignature
        {
            public long[] ByteRange { get; init; } = [];

            public byte[] Cms { get; init; } = [];

            public string? SignerName { get; init; }

            public DateTimeOffset? SigningTime { get; init; }

            public string? Reason { get; init; }

            public string? Location { get; init; }
        }

        private static List<BareSignature> ParseBareSignatures(byte[] pdf)
        {
            var signatures = new List<BareSignature>();
            int search = 0;

            while (true)
            {
                int byteRangePos = IndexOf(pdf, "/ByteRange", search);
                if (byteRangePos < 0)
                {
                    break;
                }

                int typePos = FindSignatureTypeBefore(pdf, byteRangePos);
                int dictEnd = IndexOf(pdf, ">>", byteRangePos);
                if (typePos < 0 || dictEnd < 0)
                {
                    search = byteRangePos + 10;
                    continue;
                }

                int open = IndexOf(pdf, "[", byteRangePos);
                int close = IndexOf(pdf, "]", byteRangePos);
                if (open < 0 || close < 0 || open > close || close > dictEnd)
                {
                    search = dictEnd + 2;
                    continue;
                }

                string numberText = Encoding.ASCII.GetString(pdf, open + 1, close - open - 1);
                string[] parts = numberText.Split(
                    [' ', '\t', '\r', '\n'],
                    StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 4)
                {
                    search = dictEnd + 2;
                    continue;
                }

                var byteRange = new long[4];
                bool parsed = true;
                for (int i = 0; i < 4; i++)
                {
                    if (!long.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out byteRange[i]))
                    {
                        parsed = false;
                        break;
                    }
                }

                if (!parsed)
                {
                    search = dictEnd + 2;
                    continue;
                }

                int contentsPos = IndexOf(pdf, "/Contents", byteRangePos);
                if (contentsPos < 0 || contentsPos > dictEnd)
                {
                    search = dictEnd + 2;
                    continue;
                }

                int contentsOpen = IndexOf(pdf, "<", contentsPos);
                int contentsClose = contentsOpen < 0 ? -1 : IndexOf(pdf, ">", contentsOpen + 1);
                if (contentsOpen < 0 || contentsClose < 0 || contentsClose > dictEnd)
                {
                    search = dictEnd + 2;
                    continue;
                }

                string hex = Encoding.ASCII.GetString(
                    pdf,
                    contentsOpen + 1,
                    contentsClose - contentsOpen - 1);
                byte[] cms = TrimDerSequence(HexDecode(hex));
                if (cms.Length == 0)
                {
                    search = dictEnd + 2;
                    continue;
                }

                signatures.Add(new BareSignature
                {
                    ByteRange = byteRange,
                    Cms = cms,
                    SignerName = ReadPdfTextValue(pdf, "/Name", typePos, dictEnd),
                    SigningTime = ReadSigningTime(pdf, typePos, dictEnd),
                    Reason = ReadPdfTextValue(pdf, "/Reason", typePos, dictEnd),
                    Location = ReadPdfTextValue(pdf, "/Location", typePos, dictEnd),
                });

                search = dictEnd + 2;
            }

            return signatures;
        }

        private static int FindSignatureTypeBefore(byte[] pdf, int position)
        {
            int start = Math.Max(0, position - 4096);
            ReadOnlySpan<byte> window = pdf.AsSpan(start, position - start);
            int relative = window.LastIndexOf("/Type"u8);
            if (relative < 0)
            {
                return -1;
            }

            int typePos = start + relative;
            int cursor = typePos + 5;
            while (cursor < pdf.Length && IsWhitespace(pdf[cursor]))
            {
                cursor++;
            }

            return MatchesAt(pdf, cursor, "/Sig") ? typePos : -1;
        }

        private static bool VerifyBareSignature(BareSignature signature, byte[] pdf)
        {
            long[] byteRange = signature.ByteRange;
            long totalLength = pdf.LongLength;
            if (byteRange[0] != 0
                || byteRange[1] < 0
                || byteRange[3] < 0
                || byteRange[2] < byteRange[1]
                || byteRange[2] + byteRange[3] != totalLength
                || byteRange[1] > int.MaxValue
                || byteRange[3] > int.MaxValue)
            {
                return false;
            }

            var signedContent = new byte[(int)byteRange[1] + (int)byteRange[3]];
            Array.Copy(pdf, 0, signedContent, 0, (int)byteRange[1]);
            Array.Copy(pdf, (int)byteRange[2], signedContent, (int)byteRange[1], (int)byteRange[3]);

            try
            {
                return VerifyCmsSignature(signature.Cms, signedContent);
            }
            catch (AsnContentException)
            {
                return false;
            }
            catch (CryptographicException)
            {
                return false;
            }
        }

        private static bool VerifyCmsSignature(byte[] cms, byte[] signedContent)
        {
            var reader = new AsnReader(cms, AsnEncodingRules.DER);
            AsnReader contentInfo = reader.ReadSequence();
            if (contentInfo.ReadObjectIdentifier() != "1.2.840.113549.1.7.2")
            {
                return false;
            }

            AsnReader explicitContent = contentInfo.ReadSequence(
                new Asn1Tag(TagClass.ContextSpecific, 0, isConstructed: true));
            AsnReader signedData = explicitContent.ReadSequence();
            signedData.ReadInteger();
            signedData.ReadSetOf(skipSortOrderValidation: true);
            signedData.ReadSequence();

            var certTag = new Asn1Tag(TagClass.ContextSpecific, 0, isConstructed: true);
            var crlTag = new Asn1Tag(TagClass.ContextSpecific, 1, isConstructed: true);
            var certificates = new List<X509Certificate2>();

            if (signedData.HasData && signedData.PeekTag().HasSameClassAndValue(certTag))
            {
                AsnReader certificateSet = signedData.ReadSetOf(
                    skipSortOrderValidation: true,
                    expectedTag: certTag);
                while (certificateSet.HasData)
                {
                    byte[] certificateDer = certificateSet.ReadEncodedValue().ToArray();
                    try
                    {
                        certificates.Add(X509CertificateLoader.LoadCertificate(certificateDer));
                    }
                    catch (CryptographicException)
                    {
                    }
                }
            }

            if (signedData.HasData && signedData.PeekTag().HasSameClassAndValue(crlTag))
            {
                signedData.ReadEncodedValue();
            }

            AsnReader signerInfos = signedData.ReadSetOf(skipSortOrderValidation: true);
            while (signerInfos.HasData)
            {
                AsnReader signerInfo = signerInfos.ReadSequence();
                signerInfo.ReadInteger();

                Asn1Tag sidTag = signerInfo.PeekTag();
                if (sidTag.TagClass == TagClass.ContextSpecific && sidTag.TagValue == 0)
                {
                    signerInfo.ReadEncodedValue();
                }
                else
                {
                    signerInfo.ReadSequence();
                }

                AsnReader digestAlgorithm = signerInfo.ReadSequence();
                string digestOid = digestAlgorithm.ReadObjectIdentifier();
                string? hashAlgorithm = HashAlgorithmNameFromOid(digestOid);
                if (hashAlgorithm is null)
                {
                    continue;
                }

                Asn1Tag signedAttributesTag = signerInfo.PeekTag();
                if (signedAttributesTag.TagClass != TagClass.ContextSpecific
                    || signedAttributesTag.TagValue != 0)
                {
                    continue;
                }

                byte[] signedAttributes = signerInfo.ReadEncodedValue().ToArray();
                AsnReader signatureAlgorithm = signerInfo.ReadSequence();
                string signatureOid = signatureAlgorithm.ReadObjectIdentifier();
                byte[] signature = signerInfo.ReadOctetString();

                signedAttributes[0] = 0x31;
                byte[] contentHash = ComputeHash(hashAlgorithm, signedContent);
                byte[]? messageDigest = ReadMessageDigest(signedAttributes);
                if (messageDigest is null
                    || !CryptographicOperations.FixedTimeEquals(messageDigest, contentHash))
                {
                    continue;
                }

                RSASignaturePadding padding = signatureOid == "1.2.840.113549.1.1.10"
                    ? RSASignaturePadding.Pss
                    : RSASignaturePadding.Pkcs1;

                foreach (X509Certificate2 certificate in certificates)
                {
                    using RSA? rsa = certificate.GetRSAPublicKey();
                    if (rsa is null)
                    {
                        continue;
                    }

                    try
                    {
                        if (rsa.VerifyData(
                            signedAttributes,
                            signature,
                            new HashAlgorithmName(hashAlgorithm),
                            padding))
                        {
                            return true;
                        }
                    }
                    catch (CryptographicException)
                    {
                    }
                }
            }

            return false;
        }

        private static byte[]? ReadMessageDigest(byte[] signedAttributes)
        {
            var reader = new AsnReader(signedAttributes, AsnEncodingRules.DER);
            AsnReader attributes = reader.ReadSetOf(skipSortOrderValidation: true);
            while (attributes.HasData)
            {
                AsnReader attribute = attributes.ReadSequence();
                if (attribute.ReadObjectIdentifier() == "1.2.840.113549.1.9.4")
                {
                    AsnReader values = attribute.ReadSetOf(skipSortOrderValidation: true);
                    return values.ReadOctetString();
                }
            }

            return null;
        }

        private static byte[] ComputeHash(string algorithmName, byte[] data)
        {
            using IncrementalHash hash = IncrementalHash.CreateHash(new HashAlgorithmName(algorithmName));
            hash.AppendData(data);
            return hash.GetHashAndReset();
        }

        private static string? HashAlgorithmNameFromOid(string oid)
        {
            return oid switch
            {
                "1.3.14.3.2.26" => "SHA1",
                "2.16.840.1.101.3.4.2.1" => "SHA256",
                "2.16.840.1.101.3.4.2.2" => "SHA384",
                "2.16.840.1.101.3.4.2.3" => "SHA512",
                _ => null,
            };
        }

        private static string? ReadPdfTextValue(byte[] pdf, string key, int start, int end)
        {
            int keyPos = IndexOf(pdf, key, start);
            if (keyPos < 0 || keyPos >= end)
            {
                return null;
            }

            int cursor = keyPos + key.Length;
            while (cursor < end && IsWhitespace(pdf[cursor]))
            {
                cursor++;
            }

            if (cursor >= end)
            {
                return null;
            }

            if (pdf[cursor] == '<')
            {
                int close = IndexOf(pdf, ">", cursor + 1);
                if (close < 0 || close > end)
                {
                    return null;
                }

                string hex = Encoding.ASCII.GetString(pdf, cursor + 1, close - cursor - 1);
                return DecodePdfText(HexDecode(hex));
            }

            if (pdf[cursor] == '(')
            {
                int close = IndexOf(pdf, ")", cursor + 1);
                if (close < 0 || close > end)
                {
                    return null;
                }

                return Encoding.UTF8.GetString(pdf, cursor + 1, close - cursor - 1);
            }

            return null;
        }

        private static DateTimeOffset? ReadSigningTime(byte[] pdf, int start, int end)
        {
            int keyPos = IndexOf(pdf, "/M", start);
            if (keyPos < 0 || keyPos >= end)
            {
                return null;
            }

            int cursor = keyPos + 2;
            if (cursor >= end || (!IsWhitespace(pdf[cursor]) && pdf[cursor] != '('))
            {
                return null;
            }

            while (cursor < end && IsWhitespace(pdf[cursor]))
            {
                cursor++;
            }

            if (cursor >= end || pdf[cursor] != '(')
            {
                return null;
            }

            int close = IndexOf(pdf, ")", cursor + 1);
            if (close < 0 || close > end)
            {
                return null;
            }

            string value = Encoding.ASCII.GetString(pdf, cursor + 1, close - cursor - 1);
            if (!value.StartsWith("D:", StringComparison.Ordinal) || value.Length < 16)
            {
                return null;
            }

            string timestamp = value.Substring(2, 14);
            if (DateTime.TryParseExact(
                timestamp,
                "yyyyMMddHHmmss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out DateTime parsed))
            {
                return new DateTimeOffset(parsed, TimeSpan.Zero);
            }

            return null;
        }

        private static string DecodePdfText(byte[] bytes)
        {
            if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            {
                return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
            }

            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            {
                return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
            }

            return Encoding.UTF8.GetString(bytes);
        }

        private static byte[] TrimDerSequence(byte[] data)
        {
            if (data.Length < 2 || data[0] != 0x30)
            {
                return data;
            }

            int lengthByte = data[1];
            int headerLength = 2;
            int contentLength;
            if (lengthByte < 0x80)
            {
                contentLength = lengthByte;
            }
            else if (lengthByte == 0x80)
            {
                return data;
            }
            else
            {
                int lengthBytes = lengthByte & 0x7F;
                if (lengthBytes > 4 || 2 + lengthBytes > data.Length)
                {
                    return data;
                }

                contentLength = 0;
                for (int i = 0; i < lengthBytes; i++)
                {
                    contentLength = (contentLength << 8) | data[2 + i];
                }

                headerLength += lengthBytes;
            }

            int totalLength = headerLength + contentLength;
            if (totalLength <= 0 || totalLength > data.Length)
            {
                return data;
            }

            return data[..totalLength];
        }

        private static byte[] HexDecode(string hex)
        {
            var bytes = new List<byte>(hex.Length / 2);
            int high = -1;
            foreach (char character in hex)
            {
                int value = HexValue(character);
                if (value < 0)
                {
                    continue;
                }

                if (high < 0)
                {
                    high = value;
                }
                else
                {
                    bytes.Add((byte)((high << 4) | value));
                    high = -1;
                }
            }

            return bytes.ToArray();
        }

        private static int HexValue(char character)
        {
            if (character >= '0' && character <= '9')
            {
                return character - '0';
            }

            if (character >= 'a' && character <= 'f')
            {
                return character - 'a' + 10;
            }

            if (character >= 'A' && character <= 'F')
            {
                return character - 'A' + 10;
            }

            return -1;
        }

        private static int IndexOf(byte[] data, string value, int start)
        {
            if (start < 0 || start >= data.Length)
            {
                return -1;
            }

            int relative = data.AsSpan(start).IndexOf(Encoding.ASCII.GetBytes(value));
            return relative < 0 ? -1 : start + relative;
        }

        private static bool MatchesAt(byte[] data, int position, string value)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(value);
            if (position < 0 || position + bytes.Length > data.Length)
            {
                return false;
            }

            return data.AsSpan(position, bytes.Length).SequenceEqual(bytes);
        }

        private static bool IsWhitespace(byte value)
        {
            return value is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n' or 0 or 12;
        }

        private static IntPtr OpenDocument(byte[] pdf)
        {
            ArgumentNullException.ThrowIfNull(pdf);
            if (pdf.Length == 0)
            {
                throw new ArgumentException("PDF data must not be empty.", nameof(pdf));
            }

            int errorCode = 0;
            IntPtr document;
            fixed (byte* pPdf = pdf)
            {
                document = UpstreamNativeMethods.pdf_document_open_from_bytes(
                    pPdf,
                    (nuint)pdf.Length,
                    &errorCode);
            }

            if (document == IntPtr.Zero)
            {
                throw new PdfOxideException(
                    $"Native pdf_document_open_from_bytes failed (error code {errorCode}).");
            }

            return document;
        }

        private static string? ReadString(byte* value)
        {
            if (value == null)
            {
                return null;
            }

            try
            {
                return Marshal.PtrToStringUTF8((IntPtr)value);
            }
            finally
            {
                UpstreamNativeMethods.free_string(value);
            }
        }

        private static byte[] CopyBytes(byte* data, nuint length)
        {
            int intLength = checked((int)length);
            if (intLength == 0)
            {
                return [];
            }

            var result = new byte[intLength];
            Marshal.Copy((IntPtr)data, result, 0, intLength);
            return result;
        }

        private static void ThrowIfFailed(int result, int errorCode, string operation)
        {
            if (result < 0 || errorCode != 0)
            {
                throw new PdfOxideException($"Native {operation} failed (error code {errorCode}).");
            }
        }

        private static byte[] Utf8(string value)
        {
            return Encoding.UTF8.GetBytes(value + "\0");
        }
    }

    /// <summary>
    /// Requests RFC 3161 timestamp tokens from a Time Stamp Authority.
    /// </summary>
    public sealed unsafe class PdfTimestampClient : IDisposable
    {
        private IntPtr _handle;

        /// <summary>
        /// Creates a timestamp client.
        /// </summary>
        /// <param name="url">The TSA endpoint URL.</param>
        /// <param name="username">Optional HTTP basic-auth user name.</param>
        /// <param name="password">Optional HTTP basic-auth password.</param>
        /// <param name="timeoutSeconds">The request timeout in seconds.</param>
        /// <param name="useNonce">Whether to request a nonce.</param>
        /// <param name="certReq">Whether to require the TSA certificate in the response.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="url"/> is null or empty.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="timeoutSeconds"/> is not positive.</exception>
        /// <exception cref="PdfOxideException">Thrown when the native client cannot be created.</exception>
        [SuppressMessage(
            "Design",
            "CA1054:URI-like parameters should not be strings",
            Justification = "The TSA endpoint is passed directly to the native UTF-8 API.")]
        public PdfTimestampClient(
            string url,
            string? username = null,
            string? password = null,
            int timeoutSeconds = 30,
            bool useNonce = true,
            bool certReq = false)
        {
            ArgumentException.ThrowIfNullOrEmpty(url);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeoutSeconds, nameof(timeoutSeconds));

            byte[]? usernameBytes = username is null ? null : Utf8(username);
            byte[]? passwordBytes = password is null ? null : Utf8(password);

            int errorCode = 0;
            fixed (byte* pUrl = Utf8(url))
            fixed (byte* pUsername = usernameBytes)
            fixed (byte* pPassword = passwordBytes)
            {
                _handle = UpstreamNativeMethods.pdf_tsa_client_create(
                    pUrl,
                    pUsername,
                    pPassword,
                    timeoutSeconds,
                    0,
                    useNonce ? (byte)1 : (byte)0,
                    certReq ? (byte)1 : (byte)0,
                    &errorCode);
            }

            if (_handle == IntPtr.Zero)
            {
                throw new PdfOxideException(
                    $"Native pdf_tsa_client_create failed (error code {errorCode}).");
            }
        }

        /// <summary>
        /// Requests a timestamp token for the supplied data.
        /// </summary>
        /// <param name="data">The data to timestamp.</param>
        /// <returns>The DER-encoded timestamp token.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="data"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="data"/> is empty.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the client has been disposed.</exception>
        /// <exception cref="PdfOxideException">Thrown when the timestamp request fails.</exception>
        public byte[] RequestTimestamp(byte[] data)
        {
            ArgumentNullException.ThrowIfNull(data);
            if (data.Length == 0)
            {
                throw new ArgumentException("Timestamp data must not be empty.", nameof(data));
            }

            ObjectDisposedException.ThrowIf(_handle == IntPtr.Zero, this);

            int errorCode = 0;
            IntPtr timestamp;
            fixed (byte* pData = data)
            {
                timestamp = UpstreamNativeMethods.pdf_tsa_request_timestamp(
                    _handle,
                    pData,
                    (nuint)data.Length,
                    &errorCode);
            }

            if (timestamp == IntPtr.Zero)
            {
                throw new PdfOxideException(
                    $"Native pdf_tsa_request_timestamp failed (error code {errorCode}).");
            }

            try
            {
                errorCode = 0;
                nuint outLength = 0;
                byte* token = UpstreamNativeMethods.pdf_timestamp_get_token(
                    timestamp,
                    &outLength,
                    &errorCode);
                if (token == null)
                {
                    throw new PdfOxideException(
                        $"Native pdf_timestamp_get_token failed (error code {errorCode}).");
                }

                return CopyBytes(token, outLength);
            }
            finally
            {
                UpstreamNativeMethods.pdf_timestamp_free(timestamp);
            }
        }

        /// <summary>
        /// Releases the native timestamp client.
        /// </summary>
        public void Dispose()
        {
            if (_handle != IntPtr.Zero)
            {
                UpstreamNativeMethods.pdf_tsa_client_free(_handle);
                _handle = IntPtr.Zero;
            }

            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Finalizer that releases the native timestamp client.
        /// </summary>
        ~PdfTimestampClient()
        {
            Dispose();
        }

        private static byte[] CopyBytes(byte* data, nuint length)
        {
            int intLength = checked((int)length);
            if (intLength == 0)
            {
                return [];
            }

            var result = new byte[intLength];
            Marshal.Copy((IntPtr)data, result, 0, intLength);
            return result;
        }

        private static byte[] Utf8(string value)
        {
            return Encoding.UTF8.GetBytes(value + "\0");
        }
    }
}
