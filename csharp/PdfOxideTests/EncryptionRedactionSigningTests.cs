using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using PdfOxide;
using Xunit;

namespace PdfOxideTests
{
    /// <summary>
    /// Tests for encryption, redaction, and digital signatures.
    /// </summary>
    public class EncryptionRedactionSigningTests
    {
        [Fact]
        public void EncryptsAndReopensWithPassword()
        {
            string sourcePath = CreatePdf("encrypted secret");
            string encryptedPath = TemporaryPath();
            string decryptedPath = TemporaryPath();

            try
            {
                using (PdfEditor editor = PdfEditor.Open(sourcePath))
                {
                    editor.SaveEncrypted(encryptedPath, "user", "owner");
                }

                Assert.Throws<PdfOxideException>(() =>
                {
                    using PdfEditor wrong = PdfEditor.Open(encryptedPath, "wrong");
                });

                using (PdfEditor editor = PdfEditor.Open(encryptedPath, "user"))
                {
                    editor.Save(decryptedPath);
                }

                var document = new PdfDocument(decryptedPath);
                Assert.Contains("encrypted secret", document.ExtractText(), StringComparison.Ordinal);
            }
            finally
            {
                File.Delete(sourcePath);
                File.Delete(encryptedPath);
                File.Delete(decryptedPath);
            }
        }

        [Fact]
        public void EncryptsToBytesAndReopensWithPassword()
        {
            string sourcePath = CreatePdf("bytes encrypted secret");
            string decryptedPath = TemporaryPath();

            try
            {
                byte[] encrypted;
                using (PdfEditor editor = PdfEditor.Open(sourcePath))
                {
                    encrypted = editor.ToBytesEncrypted("user", "owner");
                }

                Assert.NotEmpty(encrypted);
                Assert.Throws<PdfOxideException>(() =>
                {
                    using PdfEditor wrong = PdfEditor.FromBytes(encrypted, "wrong");
                });

                using (PdfEditor editor = PdfEditor.FromBytes(encrypted, "user"))
                {
                    editor.Save(decryptedPath);
                }

                var document = new PdfDocument(decryptedPath);
                Assert.Contains("bytes encrypted secret", document.ExtractText(), StringComparison.Ordinal);
            }
            finally
            {
                File.Delete(sourcePath);
                File.Delete(decryptedPath);
            }
        }

        [Fact]
        public void RedactsTextFromPage()
        {
            string sourcePath = TemporaryPath();
            string redactedPath = TemporaryPath();

            try
            {
                using (var builder = new DocumentBuilder())
                {
                    builder.AddPage().TextAt(72f, 700f, "SECRET-TO-REMOVE");
                    builder.Save(sourcePath);
                }

                using (PdfEditor editor = PdfEditor.Open(sourcePath))
                {
                    editor.AddRedaction(0, new PdfRect(0f, 0f, 612f, 792f));
                    Assert.True(editor.GetRedactionCount(0) >= 1);
                    editor.ApplyRedactions();
                    editor.Save(redactedPath);
                }

                var document = new PdfDocument(redactedPath);
                Assert.DoesNotContain("SECRET-TO-REMOVE", document.ExtractText(), StringComparison.Ordinal);
            }
            finally
            {
                File.Delete(sourcePath);
                File.Delete(redactedPath);
            }
        }

        [Fact]
        public void SignsAndVerifiesWithPemCredentialsAndPades()
        {
            byte[] pdf = CreatePdfBytes("signed content");

            using RSA rsa = RSA.Create(2048);
            var request = new CertificateRequest(
                "CN=PdfOxide Test Signer",
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
            using X509Certificate2 certificate = request.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddMinutes(-5),
                DateTimeOffset.UtcNow.AddYears(1));

            string certPem = certificate.ExportCertificatePem();
            string keyPem = rsa.ExportPkcs8PrivateKeyPem();

            using PdfSigningCertificate signingCertificate = PdfSigningCertificate.FromPem(certPem, keyPem);

            byte[] signed = PdfSigning.Sign(
                pdf,
                signingCertificate,
                new PdfSigningOptions { Reason = "Test signing", Location = "Test lab" });

            Assert.True(signed.Length > pdf.Length);
            Assert.Equal(1, PdfSigning.GetSignatureCount(signed));
            PdfSignatureInfo info = Assert.Single(PdfSigning.GetSignatures(signed));
            Assert.True(PdfSigning.Verify(signed));
            Assert.Equal("Test signing", info.Reason);
            Assert.Equal("Test lab", info.Location);

            byte[] pades = PdfSigning.SignPades(pdf, signingCertificate, level: 0);
            Assert.True(pades.Length > pdf.Length);
            Assert.Equal(1, PdfSigning.GetSignatureCount(pades));
            Assert.True(PdfSigning.Verify(pades));
        }

        [Fact]
        public void LoadsPkcs12AndSigns()
        {
            const string password = "pfx-password";
            byte[] pdf = CreatePdfBytes("pkcs12 signed content");

            using RSA rsa = RSA.Create(2048);
            var request = new CertificateRequest(
                "CN=PdfOxide PKCS12 Signer",
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
            using X509Certificate2 certificate = request.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddMinutes(-5),
                DateTimeOffset.UtcNow.AddYears(1));

            byte[] pkcs12 = certificate.Export(X509ContentType.Pkcs12, password);

            using PdfSigningCertificate signingCertificate = PdfSigningCertificate.FromPkcs12(pkcs12, password);
            byte[] signed = PdfSigning.Sign(pdf, signingCertificate);

            Assert.Equal(1, PdfSigning.GetSignatureCount(signed));
            Assert.True(PdfSigning.Verify(signed));
        }

        private static string CreatePdf(string text)
        {
            string path = TemporaryPath();
            using var builder = new DocumentBuilder();
            builder.AddPage(text);
            builder.Save(path);
            return path;
        }

        private static byte[] CreatePdfBytes(string text)
        {
            using var builder = new DocumentBuilder();
            builder.AddPage(text);
            return builder.ToBytes();
        }

        private static string TemporaryPath()
        {
            return Path.Combine(Path.GetTempPath(), $"pdfoxide-crypto-{Guid.NewGuid():N}.pdf");
        }
    }
}
