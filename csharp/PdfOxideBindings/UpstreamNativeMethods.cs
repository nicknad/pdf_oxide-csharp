using System;
using System.Runtime.InteropServices;

namespace PdfOxide
{
    /// <summary>
    /// P/Invoke declarations for the upstream pdf_oxide C ABI that is exported
    /// by the native <c>pdfoxide_bindings</c> library.
    /// </summary>
    /// <remarks>
    /// These declarations are grouped by feature area and contain no managed
    /// conversion logic. Higher-level helpers live in <see cref="PdfConverter"/>.
    /// Opaque handles are represented as <see cref="IntPtr"/> and byte buffers
    /// are represented as <see cref="byte"/> pointers.
    /// </remarks>
    internal static unsafe partial class UpstreamNativeMethods
    {
        private const string DllName = "pdfoxide_bindings";

        #region HTML conversion

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern IntPtr pdf_from_html(byte* html, int* errorCode);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern IntPtr pdf_from_html_css(
            byte* html,
            byte* css,
            byte* fontBytes,
            nuint fontLen,
            int* errorCode);

        #endregion

        #region Image conversion

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern IntPtr pdf_from_image(byte* path, int* errorCode);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern IntPtr pdf_from_image_bytes(byte* data, int dataLen, int* errorCode);

        #endregion

        #region Saving and lifetime

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern int pdf_save(IntPtr handle, byte* path, int* errorCode);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern byte* pdf_save_to_bytes(IntPtr handle, int* dataLen, int* errorCode);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern void pdf_free(IntPtr handle);

        #endregion

        #region Merging

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern byte* pdf_merge(
            byte** paths,
            int pathCount,
            int* dataLen,
            int* errorCode);

        #endregion

        #region Byte buffer lifetime

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern void free_bytes(byte* ptr);

        #endregion

        #region Document editor

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern IntPtr document_editor_open(byte* path, int* errorCode);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern IntPtr document_editor_open_from_bytes(byte* data, nuint len, int* errorCode);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern void document_editor_free(IntPtr handle);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern byte document_editor_is_modified(IntPtr handle);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern int document_editor_get_page_count(IntPtr handle, int* errorCode);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern int document_editor_save(IntPtr handle, byte* path, int* errorCode);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern byte* document_editor_save_to_bytes(
            IntPtr handle,
            nuint* outLen,
            int* errorCode);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern byte* document_editor_extract_pages_to_bytes(
            IntPtr handle,
            int* pages,
            nuint count,
            nuint* outLen,
            int* errorCode);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern int document_editor_delete_page(IntPtr handle, int pageIndex, int* errorCode);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern int document_editor_move_page(IntPtr handle, int from, int to, int* errorCode);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern int document_editor_get_page_rotation(IntPtr handle, int page, int* errorCode);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern int document_editor_set_page_rotation(
            IntPtr handle,
            int page,
            int degrees,
            int* errorCode);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern int document_editor_rotate_page_by(
            IntPtr handle,
            nuint page,
            int degrees,
            int* errorCode);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern int document_editor_rotate_all_pages(IntPtr handle, int degrees, int* errorCode);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern int document_editor_get_page_crop_box(
            IntPtr handle,
            nuint page,
            double* x,
            double* y,
            double* w,
            double* h,
            int* errorCode);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern int document_editor_set_page_crop_box(
            IntPtr handle,
            nuint page,
            double x,
            double y,
            double w,
            double h,
            int* errorCode);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern int document_editor_get_page_media_box(
            IntPtr handle,
            nuint page,
            double* x,
            double* y,
            double* w,
            double* h,
            int* errorCode);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern int document_editor_set_page_media_box(
            IntPtr handle,
            nuint page,
            double x,
            double y,
            double w,
            double h,
            int* errorCode);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern int document_editor_crop_margins(
            IntPtr handle,
            float left,
            float right,
            float top,
            float bottom,
            int* errorCode);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern int document_editor_merge_from(IntPtr handle, byte* sourcePath, int* errorCode);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern int document_editor_merge_from_bytes(
            IntPtr handle,
            byte* data,
            nuint len,
            int* errorCode);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern IntPtr pdfoxide_editor_open_with_password(
            byte* path,
            byte* password,
            int* errorCode);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern IntPtr pdfoxide_editor_open_from_bytes_with_password(
            byte* data,
            nuint len,
            byte* password,
            int* errorCode);

        #endregion

        #region Encryption

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern int document_editor_save_encrypted(
            IntPtr handle,
            byte* path,
            byte* userPassword,
            byte* ownerPassword,
            int* errorCode);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern byte* document_editor_save_encrypted_to_bytes(
            IntPtr handle,
            byte* userPassword,
            byte* ownerPassword,
            nuint* outLen,
            int* errorCode);

        #endregion

        #region Redaction

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern int pdf_redaction_add(
            IntPtr handle,
            nuint page,
            double x1,
            double y1,
            double x2,
            double y2,
            double r,
            double g,
            double b,
            int* errorCode);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern int pdf_redaction_count(
            IntPtr handle,
            nuint page,
            int* errorCode);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern int document_editor_apply_page_redactions(
            IntPtr handle,
            nuint page,
            int* errorCode);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern int document_editor_apply_all_redactions(
            IntPtr handle,
            int* errorCode);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern int pdf_redaction_apply(
            IntPtr handle,
            byte scrubMetadata,
            double r,
            double g,
            double b,
            int* errorCode);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern int pdf_redaction_scrub_metadata(
            IntPtr handle,
            int* errorCode);

        #endregion

        #region Signing

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern IntPtr pdf_certificate_load_from_bytes(
            byte* data,
            int len,
            byte* password,
            int* errorCode);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern IntPtr pdf_certificate_load_from_pem(
            byte* certPem,
            byte* keyPem,
            int* errorCode);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern void pdf_certificate_free(IntPtr handle);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern byte* pdf_sign_bytes(
            byte* pdf,
            nuint pdfLen,
            IntPtr certificate,
            byte* reason,
            byte* location,
            nuint* outLen,
            int* errorCode);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern byte* pdf_sign_bytes_pades(
            byte* pdf,
            nuint pdfLen,
            IntPtr certificate,
            int level,
            byte* tsaUrl,
            byte* reason,
            byte* location,
            IntPtr certs,
            IntPtr certLens,
            nuint nCerts,
            IntPtr crls,
            IntPtr crlLens,
            nuint nCrls,
            IntPtr ocsps,
            IntPtr ocspLens,
            nuint nOcsps,
            nuint* outLen,
            int* errorCode);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern IntPtr pdf_document_open_from_bytes(
            byte* data,
            nuint len,
            int* errorCode);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern void pdf_document_free(IntPtr handle);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern int pdf_document_get_signature_count(
            IntPtr document,
            int* errorCode);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern IntPtr pdf_document_get_signature(
            IntPtr document,
            int index,
            int* errorCode);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern int pdf_signature_verify_detached(
            IntPtr signature,
            byte* pdf,
            nuint len,
            int* errorCode);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern byte* pdf_signature_get_signer_name(
            IntPtr signature,
            int* errorCode);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern long pdf_signature_get_signing_time(
            IntPtr signature,
            int* errorCode);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern byte* pdf_signature_get_signing_reason(
            IntPtr signature,
            int* errorCode);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern byte* pdf_signature_get_signing_location(
            IntPtr signature,
            int* errorCode);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern byte pdf_signature_has_timestamp(
            IntPtr signature,
            int* errorCode);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern void pdf_signature_free(IntPtr handle);

        #endregion

        #region TSA and timestamps

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern IntPtr pdf_tsa_client_create(
            byte* url,
            byte* username,
            byte* password,
            int timeout,
            int hashAlgo,
            byte useNonce,
            byte certReq,
            int* errorCode);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern void pdf_tsa_client_free(IntPtr client);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern IntPtr pdf_tsa_request_timestamp(
            IntPtr client,
            byte* data,
            nuint len,
            int* errorCode);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern void pdf_timestamp_free(IntPtr timestamp);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern IntPtr pdf_timestamp_parse(
            byte* bytes,
            nuint len,
            int* errorCode);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern byte* pdf_timestamp_get_token(
            IntPtr timestamp,
            nuint* outLen,
            int* errorCode);

        #endregion

        #region String lifetime

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern void free_string(byte* ptr);

        #endregion
    }
}
