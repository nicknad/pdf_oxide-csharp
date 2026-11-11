using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using PdfOxide;

namespace FfiCrashHost;

/// <summary>
/// The outcome of a crash-host scenario: exactly one of success or failure.
/// </summary>
/// <remarks>
/// The hierarchy is closed so the host can switch over every outcome without a
/// default arm; adding an outcome forces that switch to be updated.
/// </remarks>
internal closed record class ScenarioResult
{
    internal static ScenarioResult Ok(string? detail = null) => new ScenarioSucceeded(detail);

    internal static ScenarioResult Fail(string? error, string? detail = null) => new ScenarioFailed(error, detail);
}

internal sealed record class ScenarioSucceeded(string? Detail) : ScenarioResult;

internal sealed record class ScenarioFailed(string? Error, string? Detail) : ScenarioResult;

internal delegate ScenarioResult ScenarioRunner(IReadOnlyList<string> arguments);

internal static class Scenarios
{
    private static readonly Dictionary<string, ScenarioRunner> Registry = new(StringComparer.Ordinal)
    {
        ["noop"] = _ => ScenarioResult.Ok(),
        ["null-path-extract"] = _ => NullPathExtract(),
        ["null-builder-ops"] = _ => NullBuilderOps(),
        ["null-out-pointers"] = _ => NullOutPointers(),
        ["free-null"] = _ => FreeNull(),
        ["double-free-string"] = _ => DoubleFreeString(),
        ["double-free-builder"] = _ => DoubleFreeBuilder(),
        ["use-after-free-builder"] = _ => UseAfterFreeBuilder(),
        ["free-wrong-length"] = _ => FreeWrongLength(),
        ["image-len-overflow"] = _ => ImageLenOverflow(),
        ["table-short-cells"] = _ => TableShortCells(),
        ["rich-short-colors"] = _ => RichShortColors(),
        ["images-null-element"] = _ => ImagesNullElement(),
        ["table-null-cell"] = _ => TableNullCell(),
        ["page-index-extremes"] = _ => PageIndexExtremes(),
        ["empty-pdf-build"] = _ => EmptyPdfBuild(),
        ["reuse-after-save"] = _ => ReuseAfterSave(),
        ["streaming-state-machine"] = _ => StreamingStateMachine(),
        ["streaming-abandoned"] = _ => StreamingAbandoned(),
        ["streaming-column-validation"] = _ => StreamingColumnValidation(),
        ["add-page-like-extremes"] = _ => AddPageLikeExtremes(),
        ["editor-error-code-contract"] = _ => EditorErrorCodeContract(),
        ["editor-from-bytes-validation"] = _ => EditorFromBytesValidation(),
        ["editor-double-free"] = _ => EditorDoubleFree(),
        ["editor-from-bytes-overflow"] = _ => EditorFromBytesOverflow(),
        ["editor-page-edit-extremes"] = _ => EditorPageEditExtremes(),
        ["editor-merge-extract"] = _ => EditorMergeExtract(),
        ["editor-encrypt-roundtrip"] = _ => EditorEncryptRoundtrip(),
        ["certificate-invalid-input"] = _ => CertificateInvalidInput(),
        ["signature-list-empty"] = _ => SignatureListEmpty(),
        ["certificate-null-input"] = _ => CertificateNullInput(),
        ["signature-null-handle"] = _ => SignatureNullHandle(),
        ["builder-extension-validation"] = _ => BuilderExtensionValidation(),
        ["graphics-array-mismatch"] = _ => GraphicsArrayMismatch(),
        ["font-registration-invalid"] = _ => FontRegistrationInvalid(),
        ["editor-redaction"] = _ => EditorRedaction(),
        ["editor-concurrent-read"] = _ => EditorConcurrentRead(),
        ["editor-save-invalid-path"] = _ => EditorSaveInvalidPath(),
        ["builder-large-state"] = _ => BuilderLargeState(),
        ["forms-flatten-roundtrip"] = _ => FormsFlattenRoundtrip(),
        ["signing-roundtrip"] = _ => SigningRoundtrip(),
        ["editor-dispose-race"] = _ => EditorDisposeRace(),
        ["builder-concurrent-writes"] = _ => BuilderConcurrentWrites(),
        ["tsa-client-lifecycle"] = _ => TsaClientLifecycle(),
        ["tsa-client-double-free"] = _ => TsaClientDoubleFree(),
        ["builder-error-recovery"] = _ => BuilderErrorRecovery(),
        ["panic-hook"] = PanicHook,
    };

    internal static IEnumerable<string> Names => Registry.Keys;

    internal static bool TryGet(string name, out ScenarioRunner? runner) =>
        Registry.TryGetValue(name, out runner);

    // ---------------------------------------------------------------------
    // Graceful-contract scenarios
    // ---------------------------------------------------------------------

    private static unsafe ScenarioResult NullPathExtract()
    {
        byte* result = NativeMethods.pdf_extract_text(null);
        if (result != null)
        {
            NativeMethods.pdf_string_free(result);
            return ScenarioResult.Fail("expected a null pointer for a null path");
        }

        string? error = TakeLastError();
        return error is null
            ? ScenarioResult.Fail("expected an error message")
            : ScenarioResult.Ok(error);
    }

    private static unsafe ScenarioResult NullBuilderOps()
    {
        var failures = new List<string>();

        void Check(string name, int result)
        {
            if (result != -1)
            {
                failures.Add(name + "=" + result.ToString(CultureInfo.InvariantCulture));
            }
        }

        Check("set_metadata", NativeMethods.pdf_builder_set_metadata(null, null, null, null, null, null));
        Check("add_page", NativeMethods.pdf_builder_add_page(null, 0, 0f, 0f));
        Check("get_page_count", NativeMethods.pdf_builder_get_page_count(null));
        Check("add_text", NativeMethods.pdf_builder_add_text(null, 0, null, null, 0f));
        Check("add_text_at", NativeMethods.pdf_builder_add_text_at(null, 0, 0f, 0f, null, null, 0f));
        Check("add_heading", NativeMethods.pdf_builder_add_heading(null, 0, 1, null));
        Check("add_paragraph", NativeMethods.pdf_builder_add_paragraph(null, 0, null));
        Check("add_list", NativeMethods.pdf_builder_add_list(null, 0, null, 0, 0));
        Check("add_code_block", NativeMethods.pdf_builder_add_code_block(null, 0, null, null));
        Check("add_rich_paragraph", NativeMethods.pdf_builder_add_rich_paragraph(null, 0, null, null, null, 0));
        Check("add_space", NativeMethods.pdf_builder_add_space(null, 0, 0f));
        Check("add_horizontal_rule", NativeMethods.pdf_builder_add_horizontal_rule(null, 0));
        Check("add_columns", NativeMethods.pdf_builder_add_columns(null, 0, 1, 0f, null));
        Check("add_table", NativeMethods.pdf_builder_add_table(null, 0, null, 1, 1, 0, 0));
        Check("add_image_file", NativeMethods.pdf_builder_add_image_file(null, 0, null, 0f, 0f, 1f, 1f, null));
        Check("add_image_bytes", NativeMethods.pdf_builder_add_image_bytes(null, 0, null, 1, 0f, 0f, 1f, 1f, null));
        Check("save", NativeMethods.pdf_builder_save(null, null));

        byte* data = null;
        int length = 0;
        Check("build", NativeMethods.pdf_builder_build(null, &data, &length));

        return failures.Count == 0
            ? ScenarioResult.Ok("all builder operations rejected a null state")
            : ScenarioResult.Fail(string.Join(", ", failures));
    }

    private static unsafe ScenarioResult NullOutPointers()
    {
        int docx = NativeMethods.pdf_to_docx_bytes(null, null, null);
        int build = NativeMethods.pdf_builder_build(null, null, null);
        return docx == -1 && build == -1
            ? ScenarioResult.Ok("null output pointers rejected")
            : ScenarioResult.Fail($"docx={docx}, build={build}");
    }

    private static unsafe ScenarioResult FreeNull()
    {
        NativeMethods.pdf_string_free(null);
        NativeMethods.pdf_bytes_free(null, 0);
        NativeMethods.pdf_builder_bytes_free(null, 0);
        NativeMethods.pdf_builder_free(null);
        int length = NativeMethods.pdf_get_string_len(null);
        return length == 0
            ? ScenarioResult.Ok("null frees are no-ops")
            : ScenarioResult.Fail($"unexpected length {length}");
    }

    private static unsafe ScenarioResult ImagesNullElement()
    {
        string outputPath = TempPath("images");
        byte[] output = Utf8(outputPath);
        byte** paths = stackalloc byte*[1];
        paths[0] = null;

        int result;
        fixed (byte* pOutput = output)
        {
            result = NativeMethods.pdf_images_to_pdf(paths, 1, pOutput);
        }

        TryDelete(outputPath);
        string? error = TakeLastError();
        return result == -1 && error is not null
            ? ScenarioResult.Ok(error)
            : ScenarioResult.Fail($"expected -1, got {result}");
    }

    private static unsafe ScenarioResult TableNullCell()
    {
        PdfBuilderState* state = NativeMethods.pdf_builder_create();
        if (state == null)
        {
            return ScenarioResult.Fail("builder create returned null");
        }

        try
        {
            if (NativeMethods.pdf_builder_add_page(state, 0, 0f, 0f) != 0)
            {
                return ScenarioResult.Fail(TakeLastError() ?? "add_page failed");
            }

            byte[] first = Utf8("a");
            byte[] rest = Utf8("b");
            fixed (byte* pFirst = first)
            fixed (byte* pRest = rest)
            {
                byte** cells = stackalloc byte*[4];
                cells[0] = pFirst;
                cells[1] = null;
                cells[2] = pFirst;
                cells[3] = pRest;
                int result = NativeMethods.pdf_builder_add_table(state, 0, cells, 2, 2, 0, 0);
                string? error = TakeLastError();
                return result == -1 && error is not null
                    ? ScenarioResult.Ok(error)
                    : ScenarioResult.Fail($"expected -1, got {result}");
            }
        }
        finally
        {
            NativeMethods.pdf_builder_free(state);
        }
    }

    private static unsafe ScenarioResult PageIndexExtremes()
    {
        PdfBuilderState* state = NativeMethods.pdf_builder_create();
        if (state == null)
        {
            return ScenarioResult.Fail("builder create returned null");
        }

        try
        {
            if (NativeMethods.pdf_builder_add_page(state, 0, 0f, 0f) != 0)
            {
                return ScenarioResult.Fail(TakeLastError() ?? "add_page failed");
            }

            byte[] text = Utf8("x");
            fixed (byte* pText = text)
            {
                int minimum = NativeMethods.pdf_builder_add_text(state, int.MinValue, pText, null, 0f);
                int maximum = NativeMethods.pdf_builder_add_text(state, int.MaxValue, pText, null, 0f);
                int valid = NativeMethods.pdf_builder_add_text(state, 0, pText, null, 0f);
                return minimum == -1 && maximum == -1 && valid == 0
                    ? ScenarioResult.Ok("extremes rejected, valid index accepted")
                    : ScenarioResult.Fail($"minimum={minimum}, maximum={maximum}, valid={valid}");
            }
        }
        finally
        {
            NativeMethods.pdf_builder_free(state);
        }
    }

    private static unsafe ScenarioResult EmptyPdfBuild()
    {
        PdfBuilderState* state = NativeMethods.pdf_builder_create();
        if (state == null)
        {
            return ScenarioResult.Fail("builder create returned null");
        }

        try
        {
            byte* data = null;
            int length = 0;
            int result = NativeMethods.pdf_builder_build(state, &data, &length);
            if (result != 0)
            {
                return ScenarioResult.Fail(TakeLastError() ?? "empty build failed");
            }

            if (data == null || length <= 0)
            {
                return ScenarioResult.Fail("empty build produced no bytes");
            }

            NativeMethods.pdf_builder_bytes_free(data, length);
            return ScenarioResult.Ok($"empty build produced {length} bytes");
        }
        finally
        {
            NativeMethods.pdf_builder_free(state);
        }
    }

    private static unsafe ScenarioResult ReuseAfterSave()
    {
        string path = TempPath("reuse");
        PdfBuilderState* state = NativeMethods.pdf_builder_create();
        if (state == null)
        {
            return ScenarioResult.Fail("builder create returned null");
        }

        try
        {
            if (NativeMethods.pdf_builder_add_page(state, 0, 0f, 0f) != 0 ||
                AddText(state, 0, "first") != 0)
            {
                return ScenarioResult.Fail(TakeLastError() ?? "first page failed");
            }

            if (SaveBuilder(state, path) != 0)
            {
                return ScenarioResult.Fail(TakeLastError() ?? "first save failed");
            }

            (int firstStatus, int firstLength) = BuildAndFree(state);
            if (firstStatus != 0 || firstLength <= 0)
            {
                return ScenarioResult.Fail(TakeLastError() ?? "first build failed");
            }

            if (NativeMethods.pdf_builder_add_page(state, 1, 0f, 0f) != 0 ||
                AddText(state, 1, "second") != 0)
            {
                return ScenarioResult.Fail(TakeLastError() ?? "second page failed");
            }

            if (SaveBuilder(state, path) != 0)
            {
                return ScenarioResult.Fail(TakeLastError() ?? "second save failed");
            }

            (int secondStatus, int secondLength) = BuildAndFree(state);
            if (secondStatus != 0 || secondLength <= 0)
            {
                return ScenarioResult.Fail(TakeLastError() ?? "second build failed");
            }

            return File.Exists(path)
                ? ScenarioResult.Ok($"first={firstLength}, second={secondLength}")
                : ScenarioResult.Fail("saved file is missing");
        }
        finally
        {
            TryDelete(path);
            NativeMethods.pdf_builder_free(state);
        }
    }

    private static unsafe ScenarioResult PanicHook(IReadOnlyList<string> arguments)
    {
        int kind = 0;
        if (arguments.Count > 0)
        {
            _ = int.TryParse(arguments[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out kind);
        }

        try
        {
            TestHooks.pdfoxide_test_panic(kind);
        }
        catch (EntryPointNotFoundException)
        {
            return ScenarioResult.Ok("hook-unavailable (build Rust with --features ffi-test-hooks)");
        }

        return ScenarioResult.Fail("panic hook returned unexpectedly");
    }

    // ---------------------------------------------------------------------
    // Memory-safety / UB scenarios. These are allowed to kill the process.
    // ---------------------------------------------------------------------

    private static unsafe ScenarioResult DoubleFreeString()
    {
        _ = NativeMethods.pdf_extract_text(null);
        byte* pointer = NativeMethods.pdf_last_error_message();
        if (pointer == null)
        {
            return ScenarioResult.Fail("could not obtain an allocated error string");
        }

        NativeMethods.pdf_string_free(pointer);
        NativeMethods.pdf_string_free(pointer);
        return ScenarioResult.Ok("freed the same string twice");
    }

    private static unsafe ScenarioResult DoubleFreeBuilder()
    {
        PdfBuilderState* state = NativeMethods.pdf_builder_create();
        if (state == null)
        {
            return ScenarioResult.Fail("builder create returned null");
        }

        NativeMethods.pdf_builder_free(state);
        NativeMethods.pdf_builder_free(state);
        return ScenarioResult.Ok("freed the same builder twice");
    }

    private static unsafe ScenarioResult UseAfterFreeBuilder()
    {
        PdfBuilderState* state = NativeMethods.pdf_builder_create();
        if (state == null)
        {
            return ScenarioResult.Fail("builder create returned null");
        }

        NativeMethods.pdf_builder_free(state);
        byte[] text = Utf8("after free");
        fixed (byte* pText = text)
        {
            int result = NativeMethods.pdf_builder_add_heading(state, 0, 1, pText);
            return ScenarioResult.Ok($"used a freed builder, result={result}");
        }
    }

    private static unsafe ScenarioResult FreeWrongLength()
    {
        PdfBuilderState* state = NativeMethods.pdf_builder_create();
        if (state == null)
        {
            return ScenarioResult.Fail("builder create returned null");
        }

        int page = NativeMethods.pdf_builder_add_page(state, 0, 0f, 0f);
        int text = AddText(state, 0, "x");
        byte* data = null;
        int length = 0;
        int build = NativeMethods.pdf_builder_build(state, &data, &length);
        if (page != 0 || text != 0 || build != 0 || data == null || length <= 0)
        {
            return ScenarioResult.Fail(
                $"setup failed: page={page}, text={text}, build={build}, length={length}");
        }

        NativeMethods.pdf_builder_bytes_free(data, length + 1);
        return ScenarioResult.Ok($"freed {length + 1} bytes for a {length}-byte buffer");
    }

    private static unsafe ScenarioResult ImageLenOverflow()
    {
        PdfBuilderState* state = NativeMethods.pdf_builder_create();
        if (state == null)
        {
            return ScenarioResult.Fail("builder create returned null");
        }

        if (NativeMethods.pdf_builder_add_page(state, 0, 0f, 0f) != 0)
        {
            return ScenarioResult.Fail(TakeLastError() ?? "add_page failed");
        }

        byte[] image = new byte[16];
        fixed (byte* pImage = image)
        {
            int result = NativeMethods.pdf_builder_add_image_bytes(
                state, 0, pImage, int.MaxValue, 0f, 0f, 10f, 10f, null);
            return ScenarioResult.Ok($"add_image_bytes returned {result}");
        }
    }

    private static unsafe ScenarioResult TableShortCells()
    {
        PdfBuilderState* state = NativeMethods.pdf_builder_create();
        if (state == null)
        {
            return ScenarioResult.Fail("builder create returned null");
        }

        try
        {
            if (NativeMethods.pdf_builder_add_page(state, 0, 0f, 0f) != 0)
            {
                return ScenarioResult.Fail(TakeLastError() ?? "add_page failed");
            }

            byte[] first = Utf8("a");
            byte[] second = Utf8("b");
            fixed (byte* pFirst = first)
            fixed (byte* pSecond = second)
            {
                byte** cells = stackalloc byte*[2];
                cells[0] = pFirst;
                cells[1] = pSecond;
                int result = NativeMethods.pdf_builder_add_table(state, 0, cells, 2, 2, 0, 0);
                return ScenarioResult.Ok($"add_table with 2 cells for 2x2 returned {result}");
            }
        }
        finally
        {
            NativeMethods.pdf_builder_free(state);
        }
    }

    private static unsafe ScenarioResult RichShortColors()
    {
        PdfBuilderState* state = NativeMethods.pdf_builder_create();
        if (state == null)
        {
            return ScenarioResult.Fail("builder create returned null");
        }

        try
        {
            if (NativeMethods.pdf_builder_add_page(state, 0, 0f, 0f) != 0)
            {
                return ScenarioResult.Fail(TakeLastError() ?? "add_page failed");
            }

            byte[] first = Utf8("red");
            byte[] second = Utf8(" normal");
            fixed (byte* pFirst = first)
            fixed (byte* pSecond = second)
            {
                byte** texts = stackalloc byte*[2];
                texts[0] = pFirst;
                texts[1] = pSecond;

                int* styles = stackalloc int[2];
                styles[0] = 3;
                styles[1] = 0;

                // Two runs need six floats; provide only two.
                float* colors = stackalloc float[2];
                colors[0] = 1f;
                colors[1] = 0f;

                int result = NativeMethods.pdf_builder_add_rich_paragraph(
                    state, 0, texts, styles, colors, 2);
                return ScenarioResult.Ok($"add_rich_paragraph with a short color array returned {result}");
            }
        }
        finally
        {
            NativeMethods.pdf_builder_free(state);
        }
    }

    // ---------------------------------------------------------------------
    // Editor handle scenarios (K1-K3)
    // ---------------------------------------------------------------------

    private static unsafe ScenarioResult EditorErrorCodeContract()
    {
        string path = TempPath("editor");
        try
        {
            if (!CreateSamplePdf(path))
            {
                return ScenarioResult.Fail(TakeLastError() ?? "sample PDF creation failed");
            }

            byte[] pathBytes = Utf8(path);
            byte[] password = Utf8(string.Empty);
            fixed (byte* pPath = pathBytes)
            fixed (byte* pPassword = password)
            {
                var failures = new List<string>();
                void Expect(string name, bool condition, string detail)
                {
                    if (!condition)
                    {
                        failures.Add(name + ": " + detail);
                    }
                }

                int errorCode = 42;
                IntPtr handle = UpstreamNativeMethods.pdfoxide_editor_open_with_password(
                    pPath, pPassword, &errorCode);
                Expect("success-handle", handle != IntPtr.Zero, "handle was null");
                Expect("success-error", errorCode == 0, "errorCode=" + errorCode.ToString(CultureInfo.InvariantCulture));
                if (handle != IntPtr.Zero)
                {
                    UpstreamNativeMethods.document_editor_free(handle);
                }

                errorCode = 42;
                byte[] missing = Utf8(path + ".missing");
                fixed (byte* pMissing = missing)
                {
                    IntPtr missingHandle = UpstreamNativeMethods.pdfoxide_editor_open_with_password(
                        pMissing, pPassword, &errorCode);
                    Expect("missing-handle", missingHandle == IntPtr.Zero, "handle was not null");
                    Expect("missing-error", errorCode == -1, "errorCode=" + errorCode.ToString(CultureInfo.InvariantCulture));
                    if (missingHandle != IntPtr.Zero)
                    {
                        UpstreamNativeMethods.document_editor_free(missingHandle);
                    }
                }

                IntPtr nullErrorHandle = UpstreamNativeMethods.pdfoxide_editor_open_with_password(
                    pPath, pPassword, null);
                Expect("null-error-code-handle", nullErrorHandle != IntPtr.Zero, "handle was null");
                if (nullErrorHandle != IntPtr.Zero)
                {
                    UpstreamNativeMethods.document_editor_free(nullErrorHandle);
                }

                errorCode = 42;
                IntPtr nullPasswordHandle = UpstreamNativeMethods.pdfoxide_editor_open_with_password(
                    pPath, null, &errorCode);
                Expect("null-password-handle", nullPasswordHandle == IntPtr.Zero, "handle was not null");
                Expect("null-password-error", errorCode == -1, "errorCode=" + errorCode.ToString(CultureInfo.InvariantCulture));
                if (nullPasswordHandle != IntPtr.Zero)
                {
                    UpstreamNativeMethods.document_editor_free(nullPasswordHandle);
                }

                return failures.Count == 0
                    ? ScenarioResult.Ok("editor error_code and password contract verified")
                    : ScenarioResult.Fail(string.Join(", ", failures));
            }
        }
        finally
        {
            TryDelete(path);
        }
    }

    private static unsafe ScenarioResult EditorFromBytesValidation()
    {
        string path = TempPath("editor-bytes");
        try
        {
            if (!CreateSamplePdf(path))
            {
                return ScenarioResult.Fail(TakeLastError() ?? "sample PDF creation failed");
            }

            byte[] bytes = File.ReadAllBytes(path);
            byte[] password = Utf8(string.Empty);
            fixed (byte* pData = bytes)
            fixed (byte* pPassword = password)
            {
                var failures = new List<string>();
                void Expect(string name, bool condition, string detail)
                {
                    if (!condition)
                    {
                        failures.Add(name + ": " + detail);
                    }
                }

                int errorCode = 42;
                IntPtr exact = UpstreamNativeMethods.pdfoxide_editor_open_from_bytes_with_password(
                    pData, (nuint)bytes.Length, pPassword, &errorCode);
                Expect("exact-handle", exact != IntPtr.Zero, "handle was null");
                Expect("exact-error", errorCode == 0, "errorCode=" + errorCode.ToString(CultureInfo.InvariantCulture));
                if (exact != IntPtr.Zero)
                {
                    UpstreamNativeMethods.document_editor_free(exact);
                }

                errorCode = 42;
                IntPtr zero = UpstreamNativeMethods.pdfoxide_editor_open_from_bytes_with_password(
                    pData, 0, pPassword, &errorCode);
                Expect("zero-handle", zero == IntPtr.Zero, "handle was not null");
                Expect("zero-error", errorCode == -1, "errorCode=" + errorCode.ToString(CultureInfo.InvariantCulture));
                if (zero != IntPtr.Zero)
                {
                    UpstreamNativeMethods.document_editor_free(zero);
                }

                errorCode = 42;
                IntPtr nullData = UpstreamNativeMethods.pdfoxide_editor_open_from_bytes_with_password(
                    null, (nuint)bytes.Length, pPassword, &errorCode);
                Expect("null-handle", nullData == IntPtr.Zero, "handle was not null");
                Expect("null-error", errorCode == -1, "errorCode=" + errorCode.ToString(CultureInfo.InvariantCulture));
                if (nullData != IntPtr.Zero)
                {
                    UpstreamNativeMethods.document_editor_free(nullData);
                }

                return failures.Count == 0
                    ? ScenarioResult.Ok("editor-from-bytes validation is enforced")
                    : ScenarioResult.Fail(string.Join(", ", failures));
            }
        }
        finally
        {
            TryDelete(path);
        }
    }

    private static unsafe ScenarioResult EditorDoubleFree()
    {
        string path = TempPath("editor-double");
        try
        {
            if (!CreateSamplePdf(path))
            {
                return ScenarioResult.Fail(TakeLastError() ?? "sample PDF creation failed");
            }

            byte[] pathBytes = Utf8(path);
            byte[] password = Utf8(string.Empty);
            fixed (byte* pPath = pathBytes)
            fixed (byte* pPassword = password)
            {
                int errorCode = 0;
                IntPtr handle = UpstreamNativeMethods.pdfoxide_editor_open_with_password(
                    pPath, pPassword, &errorCode);
                if (handle == IntPtr.Zero)
                {
                    return ScenarioResult.Fail("could not open the sample editor");
                }

                UpstreamNativeMethods.document_editor_free(handle);
                UpstreamNativeMethods.document_editor_free(handle);
                return ScenarioResult.Ok("freed the same editor handle twice");
            }
        }
        finally
        {
            TryDelete(path);
        }
    }

    private static unsafe ScenarioResult EditorFromBytesOverflow()
    {
        string path = TempPath("editor-overflow");
        try
        {
            if (!CreateSamplePdf(path))
            {
                return ScenarioResult.Fail(TakeLastError() ?? "sample PDF creation failed");
            }

            byte[] bytes = File.ReadAllBytes(path);
            byte[] password = Utf8(string.Empty);
            fixed (byte* pData = bytes)
            fixed (byte* pPassword = password)
            {
                int errorCode = 0;
                IntPtr handle = UpstreamNativeMethods.pdfoxide_editor_open_from_bytes_with_password(
                    pData, (nuint)(bytes.Length + 1), pPassword, &errorCode);
                if (handle != IntPtr.Zero)
                {
                    UpstreamNativeMethods.document_editor_free(handle);
                }

                return ScenarioResult.Ok(
                    $"len+1 returned {(handle == IntPtr.Zero ? "error" : "handle")}, "
                    + "errorCode=" + errorCode.ToString(CultureInfo.InvariantCulture));
            }
        }
        finally
        {
            TryDelete(path);
        }
    }

    private static unsafe ScenarioResult EditorPageEditExtremes()
    {
        string path = TempPath("editor-pages");
        try
        {
            if (!CreateSamplePdf(path))
            {
                return ScenarioResult.Fail(TakeLastError() ?? "sample PDF creation failed");
            }

            if (!TryOpenEditor(path, string.Empty, out IntPtr editor, out int openError))
            {
                return ScenarioResult.Fail($"editor open failed, errorCode={openError}");
            }

            try
            {
                var failures = new List<string>();
                void Expect(string name, int actual, int expected)
                {
                    if (actual != expected)
                    {
                        failures.Add(name + "=" + actual.ToString(CultureInfo.InvariantCulture)
                            + " expected " + expected.ToString(CultureInfo.InvariantCulture));
                    }
                }

                int error = 0;
                Expect("delete-negative", UpstreamNativeMethods.document_editor_delete_page(editor, -1, &error), -1);
                Expect("delete-out-of-range", UpstreamNativeMethods.document_editor_delete_page(editor, 1, &error), -1);
                Expect("move-out-of-range", UpstreamNativeMethods.document_editor_move_page(editor, 0, 1, &error), -1);
                Expect("rotate-invalid-page", UpstreamNativeMethods.document_editor_set_page_rotation(editor, 1, 90, &error), -1);
                Expect("rotate-valid", UpstreamNativeMethods.document_editor_set_page_rotation(editor, 0, 90, &error), 0);
                Expect("crop-invalid-page", UpstreamNativeMethods.document_editor_set_page_crop_box(
                    editor, 1, 0, 0, 100, 100, &error), -1);
                Expect("count-still", UpstreamNativeMethods.document_editor_get_page_count(editor, &error), 1);

                nuint length = 0;
                byte* bytes = UpstreamNativeMethods.document_editor_save_to_bytes(editor, &length, &error);
                if (bytes == null || length == 0)
                {
                    failures.Add("save-to-bytes");
                }
                if (bytes != null)
                {
                    UpstreamNativeMethods.free_bytes(bytes);
                }

                return failures.Count == 0
                    ? ScenarioResult.Ok("editor page edits reject invalid indices")
                    : ScenarioResult.Fail(string.Join(", ", failures));
            }
            finally
            {
                UpstreamNativeMethods.document_editor_free(editor);
            }
        }
        finally
        {
            TryDelete(path);
        }
    }

    private static unsafe ScenarioResult EditorMergeExtract()
    {
        string path = TempPath("editor-merge");
        try
        {
            if (!CreateSamplePdf(path))
            {
                return ScenarioResult.Fail(TakeLastError() ?? "sample PDF creation failed");
            }

            if (!TryOpenEditor(path, string.Empty, out IntPtr editor, out int openError))
            {
                return ScenarioResult.Fail($"editor open failed, errorCode={openError}");
            }

            try
            {
                var failures = new List<string>();
                int error = 0;

                byte[] missing = Utf8(path + ".missing");
                fixed (byte* pMissing = missing)
                {
                    if (UpstreamNativeMethods.document_editor_merge_from(editor, pMissing, &error) != -1)
                    {
                        failures.Add("merge-missing");
                    }
                }

                int* pages = stackalloc int[1];
                pages[0] = 0;
                nuint length = 0;
                byte* extracted = UpstreamNativeMethods.document_editor_extract_pages_to_bytes(
                    editor, pages, 1, &length, &error);
                if (extracted == null || length == 0)
                {
                    failures.Add("extract-page-0");
                }
                if (extracted != null)
                {
                    UpstreamNativeMethods.free_bytes(extracted);
                }

                pages[0] = 5;
                length = 0;
                byte* outOfRange = UpstreamNativeMethods.document_editor_extract_pages_to_bytes(
                    editor, pages, 1, &length, &error);
                if (outOfRange != null)
                {
                    failures.Add("extract-out-of-range-succeeded");
                    UpstreamNativeMethods.free_bytes(outOfRange);
                }

                return failures.Count == 0
                    ? ScenarioResult.Ok("editor merge/extract handles invalid inputs")
                    : ScenarioResult.Fail(string.Join(", ", failures));
            }
            finally
            {
                UpstreamNativeMethods.document_editor_free(editor);
            }
        }
        finally
        {
            TryDelete(path);
        }
    }

    private static unsafe ScenarioResult EditorEncryptRoundtrip()
    {
        string path = TempPath("editor-plain");
        string encryptedPath = TempPath("editor-encrypted");
        try
        {
            if (!CreateSamplePdf(path))
            {
                return ScenarioResult.Fail(TakeLastError() ?? "sample PDF creation failed");
            }

            if (!TryOpenEditor(path, string.Empty, out IntPtr editor, out int openError))
            {
                return ScenarioResult.Fail($"editor open failed, errorCode={openError}");
            }

            try
            {
                byte[] outputPath = Utf8(encryptedPath);
                byte[] user = Utf8("user-pass");
                byte[] owner = Utf8("owner-pass");
                fixed (byte* pOutput = outputPath)
                fixed (byte* pUser = user)
                fixed (byte* pOwner = owner)
                {
                    int error = 0;
                    int save = UpstreamNativeMethods.document_editor_save_encrypted(
                        editor, pOutput, pUser, pOwner, &error);
                    if (save != 0)
                    {
                        return ScenarioResult.Fail($"encrypted save failed, rc={save}, errorCode={error}");
                    }
                }
            }
            finally
            {
                UpstreamNativeMethods.document_editor_free(editor);
            }

            if (!TryOpenEditor(encryptedPath, "user-pass", out IntPtr encrypted, out int correctError))
            {
                return ScenarioResult.Fail($"encrypted reopen failed, errorCode={correctError}");
            }
            UpstreamNativeMethods.document_editor_free(encrypted);

            if (TryOpenEditor(encryptedPath, "wrong-pass", out IntPtr wrong, out int wrongError))
            {
                UpstreamNativeMethods.document_editor_free(wrong);
                return ScenarioResult.Fail($"wrong password unexpectedly opened, errorCode={wrongError}");
            }

            return ScenarioResult.Ok("encrypted save and password round-trip verified");
        }
        finally
        {
            TryDelete(path);
            TryDelete(encryptedPath);
        }
    }

    private static unsafe ScenarioResult CertificateInvalidInput()
    {
        var failures = new List<string>();
        var observations = new List<string>();
        byte[] password = Utf8(string.Empty);

        byte[] garbage = new byte[32];
        fixed (byte* pGarbage = garbage)
        fixed (byte* pPassword = password)
        {
            int error = 42;
            IntPtr handle = UpstreamNativeMethods.pdf_certificate_load_from_bytes(
                pGarbage, garbage.Length, pPassword, &error);
            observations.Add("garbage-error=" + error.ToString(CultureInfo.InvariantCulture));
            if (handle != IntPtr.Zero)
            {
                failures.Add("garbage-handle");
                UpstreamNativeMethods.pdf_certificate_free(handle);
            }
            if (error == 0)
            {
                failures.Add("garbage-error=0");
            }

            error = 42;
            IntPtr empty = UpstreamNativeMethods.pdf_certificate_load_from_bytes(
                pGarbage, 0, pPassword, &error);
            observations.Add("empty-error=" + error.ToString(CultureInfo.InvariantCulture));
            if (empty != IntPtr.Zero)
            {
                failures.Add("empty-handle");
                UpstreamNativeMethods.pdf_certificate_free(empty);
            }
            if (error == 0)
            {
                failures.Add("empty-error=0");
            }
        }

        UpstreamNativeMethods.pdf_certificate_free(IntPtr.Zero);
        return failures.Count == 0
            ? ScenarioResult.Ok("certificate loaders reject invalid input: "
                + string.Join(", ", observations))
            : ScenarioResult.Fail(string.Join(", ", failures));
    }

    private static unsafe ScenarioResult SignatureListEmpty()
    {
        string path = TempPath("signature-list");
        try
        {
            if (!CreateSamplePdf(path))
            {
                return ScenarioResult.Fail(TakeLastError() ?? "sample PDF creation failed");
            }

            byte[] bytes = File.ReadAllBytes(path);
            fixed (byte* pData = bytes)
            {
                var failures = new List<string>();
                int error = 0;
                IntPtr document = UpstreamNativeMethods.pdf_document_open_from_bytes(
                    pData, (nuint)bytes.Length, &error);
                if (document == IntPtr.Zero)
                {
                    return ScenarioResult.Fail($"document open failed, errorCode={error}");
                }

                try
                {
                    int count = UpstreamNativeMethods.pdf_document_get_signature_count(document, &error);
                    if (count != 0)
                    {
                        failures.Add("count=" + count.ToString(CultureInfo.InvariantCulture));
                    }

                    IntPtr signature = UpstreamNativeMethods.pdf_document_get_signature(document, 0, &error);
                    if (signature != IntPtr.Zero)
                    {
                        failures.Add("signature-0-not-null");
                        UpstreamNativeMethods.pdf_signature_free(signature);
                    }

                    IntPtr negative = UpstreamNativeMethods.pdf_document_get_signature(document, -1, &error);
                    if (negative != IntPtr.Zero)
                    {
                        failures.Add("signature-negative-not-null");
                        UpstreamNativeMethods.pdf_signature_free(negative);
                    }
                }
                finally
                {
                    UpstreamNativeMethods.pdf_document_free(document);
                }

                return failures.Count == 0
                    ? ScenarioResult.Ok("unsigned document reports no signatures")
                    : ScenarioResult.Fail(string.Join(", ", failures));
            }
        }
        finally
        {
            TryDelete(path);
        }
    }

    private static unsafe ScenarioResult CertificateNullInput()
    {
        int error = 0;
        IntPtr handle = UpstreamNativeMethods.pdf_certificate_load_from_bytes(null, 0, null, &error);
        if (handle != IntPtr.Zero)
        {
            UpstreamNativeMethods.pdf_certificate_free(handle);
        }

        return ScenarioResult.Ok($"null certificate input returned {(handle == IntPtr.Zero ? "null" : "handle")}");
    }

    private static unsafe ScenarioResult SignatureNullHandle()
    {
        byte[] data = Utf8("not a pdf");
        fixed (byte* pData = data)
        {
            int error = 0;
            int result = UpstreamNativeMethods.pdf_signature_verify_detached(
                IntPtr.Zero, pData, (nuint)data.Length, &error);
            return ScenarioResult.Ok($"null signature verify returned {result}");
        }
    }

    private static unsafe bool TryOpenEditor(
        string path,
        string password,
        out IntPtr handle,
        out int errorCode)
    {
        byte[] pathBytes = Utf8(path);
        byte[] passwordBytes = Utf8(password);
        fixed (byte* pPath = pathBytes)
        fixed (byte* pPassword = passwordBytes)
        {
            int code = 0;
            handle = UpstreamNativeMethods.pdfoxide_editor_open_with_password(
                pPath, pPassword, &code);
            errorCode = code;
            return handle != IntPtr.Zero && code == 0;
        }
    }

    private static unsafe bool CreateSamplePdf(string path)
    {
        byte[] text = Utf8("hello");
        byte[] output = Utf8(path);
        fixed (byte* pText = text)
        fixed (byte* pOutput = output)
        {
            return NativeMethods.pdf_from_text(pText, pOutput) == 0 && File.Exists(path);
        }
    }

    // ---------------------------------------------------------------------
    // Remaining editor / signing / large-state scenarios (K8-K12, L2/L4, M3/M10)
    // ---------------------------------------------------------------------

    private static unsafe ScenarioResult EditorRedaction()
    {
        string path = TempPath("editor-redaction");
        try
        {
            if (!CreateSamplePdf(path))
            {
                return ScenarioResult.Fail(TakeLastError() ?? "sample PDF creation failed");
            }

            if (!TryOpenEditor(path, string.Empty, out IntPtr editor, out int openError))
            {
                return ScenarioResult.Fail($"editor open failed, errorCode={openError}");
            }

            try
            {
                var failures = new List<string>();
                void Expect(string name, int actual, int expected)
                {
                    if (actual != expected)
                    {
                        failures.Add(name + "=" + actual.ToString(CultureInfo.InvariantCulture)
                            + " expected " + expected.ToString(CultureInfo.InvariantCulture));
                    }
                }

                int error = 0;
                Expect("add", UpstreamNativeMethods.pdf_redaction_add(
                    editor, 0, 10, 10, 50, 20, 0, 0, 0, &error), 0);
                Expect("add-invalid-page", UpstreamNativeMethods.pdf_redaction_add(
                    editor, 5, 10, 10, 50, 20, 0, 0, 0, &error), -1);
                Expect("count", UpstreamNativeMethods.pdf_redaction_count(editor, 0, &error), 1);
                Expect("apply-page", UpstreamNativeMethods.document_editor_apply_page_redactions(
                    editor, 0, &error), 0);
                Expect("scrub", UpstreamNativeMethods.pdf_redaction_scrub_metadata(editor, &error), 0);

                nuint length = 0;
                byte* bytes = UpstreamNativeMethods.document_editor_save_to_bytes(editor, &length, &error);
                if (bytes == null || length == 0)
                {
                    failures.Add("save");
                }
                if (bytes != null)
                {
                    UpstreamNativeMethods.free_bytes(bytes);
                }

                return failures.Count == 0
                    ? ScenarioResult.Ok("redaction add/apply/scrub succeeded")
                    : ScenarioResult.Fail(string.Join(", ", failures));
            }
            finally
            {
                UpstreamNativeMethods.document_editor_free(editor);
            }
        }
        finally
        {
            TryDelete(path);
        }
    }

    private static unsafe ScenarioResult EditorConcurrentRead()
    {
        string path = TempPath("editor-concurrent");
        try
        {
            if (!CreateSamplePdf(path))
            {
                return ScenarioResult.Fail(TakeLastError() ?? "sample PDF creation failed");
            }

            var failures = new ConcurrentBag<string>();
            var threads = new Thread[4];
            for (int index = 0; index < threads.Length; index++)
            {
                threads[index] = new Thread(() =>
                {
                    if (!TryOpenEditor(path, string.Empty, out IntPtr editor, out int openError))
                    {
                        failures.Add("open=" + openError.ToString(CultureInfo.InvariantCulture));
                        return;
                    }

                    try
                    {
                        for (int iteration = 0; iteration < 50; iteration++)
                        {
                            int error = 0;
                            int count = UpstreamNativeMethods.document_editor_get_page_count(editor, &error);
                            if (count != 1 || error != 0)
                            {
                                failures.Add("count=" + count.ToString(CultureInfo.InvariantCulture)
                                    + ",error=" + error.ToString(CultureInfo.InvariantCulture));
                                return;
                            }
                        }
                    }
                    finally
                    {
                        UpstreamNativeMethods.document_editor_free(editor);
                    }
                });
                threads[index].Start();
            }

            foreach (Thread thread in threads)
            {
                thread.Join();
            }

            return failures.IsEmpty
                ? ScenarioResult.Ok("independent editors work in parallel")
                : ScenarioResult.Fail(string.Join(", ", failures));
        }
        finally
        {
            TryDelete(path);
        }
    }

    private static unsafe ScenarioResult EditorSaveInvalidPath()
    {
        string path = TempPath("editor-save-invalid");
        try
        {
            if (!CreateSamplePdf(path))
            {
                return ScenarioResult.Fail(TakeLastError() ?? "sample PDF creation failed");
            }

            if (!TryOpenEditor(path, string.Empty, out IntPtr editor, out int openError))
            {
                return ScenarioResult.Fail($"editor open failed, errorCode={openError}");
            }

            try
            {
                string invalid = Path.Combine(
                    Path.GetTempPath(),
                    "pdf_oxide_missing_" + Guid.NewGuid().ToString("N"),
                    "out.pdf");
                byte[] invalidPath = Utf8(invalid);
                fixed (byte* pInvalid = invalidPath)
                {
                    int error = 0;
                    int save = UpstreamNativeMethods.document_editor_save(editor, pInvalid, &error);
                    if (save == 0)
                    {
                        return ScenarioResult.Fail("save to a missing directory unexpectedly succeeded");
                    }
                    if (error == 0)
                    {
                        return ScenarioResult.Fail("failed save left errorCode at zero");
                    }
                }

                nuint length = 0;
                int bytesError = 0;
                byte* bytes = UpstreamNativeMethods.document_editor_save_to_bytes(editor, &length, &bytesError);
                if (bytes == null || length == 0)
                {
                    return ScenarioResult.Fail("save_to_bytes failed after a bad path save");
                }
                UpstreamNativeMethods.free_bytes(bytes);
                return ScenarioResult.Ok("invalid save path failed without poisoning the editor");
            }
            finally
            {
                UpstreamNativeMethods.document_editor_free(editor);
            }
        }
        finally
        {
            TryDelete(path);
        }
    }

    private static unsafe ScenarioResult BuilderLargeState()
    {
        const int pageCount = 100;
        PdfBuilderState* state = NativeMethods.pdf_builder_create();
        if (state == null)
        {
            return ScenarioResult.Fail("builder create returned null");
        }

        try
        {
            for (int index = 0; index < pageCount; index++)
            {
                if (NativeMethods.pdf_builder_add_page(state, 0, 0f, 0f) != 0)
                {
                    return ScenarioResult.Fail(TakeLastError() ?? "add_page failed");
                }
                if (AddText(state, index, "page " + index.ToString(CultureInfo.InvariantCulture)) != 0)
                {
                    return ScenarioResult.Fail(TakeLastError() ?? "add_text failed");
                }
            }

            int counted = NativeMethods.pdf_builder_get_page_count(state);
            if (counted != pageCount)
            {
                return ScenarioResult.Fail($"page count {counted} != {pageCount}");
            }

            (int status, int length) = BuildAndFree(state);
            return status == 0 && length > 0
                ? ScenarioResult.Ok($"built {pageCount} pages, {length} bytes")
                : ScenarioResult.Fail(TakeLastError() ?? "large build failed");
        }
        finally
        {
            NativeMethods.pdf_builder_free(state);
        }
    }

    private static unsafe ScenarioResult FormsFlattenRoundtrip()
    {
        string path = TempPath("forms-flatten");
        try
        {
            PdfBuilderState* state = NativeMethods.pdf_builder_create();
            if (state == null)
            {
                return ScenarioResult.Fail("builder create returned null");
            }

            try
            {
                if (NativeMethods.pdf_builder_add_page(state, 0, 0f, 0f) != 0)
                {
                    return ScenarioResult.Fail(TakeLastError() ?? "add_page failed");
                }

                byte[] name = Utf8("field");
                fixed (byte* pName = name)
                {
                    if (NativeMethods.pdf_builder_add_text_field(
                        state, 0, pName, 20f, 20f, 120f, 20f, pName) != 0)
                    {
                        return ScenarioResult.Fail(TakeLastError() ?? "add_text_field failed");
                    }
                }

                if (SaveBuilder(state, path) != 0)
                {
                    return ScenarioResult.Fail(TakeLastError() ?? "save failed");
                }
            }
            finally
            {
                NativeMethods.pdf_builder_free(state);
            }

            if (!TryOpenEditor(path, string.Empty, out IntPtr editor, out int openError))
            {
                return ScenarioResult.Fail($"editor open failed, errorCode={openError}");
            }

            try
            {
                int error = 0;
                int flatten = UpstreamNativeMethods.document_editor_flatten_forms(editor, &error);
                if (flatten != 0)
                {
                    return ScenarioResult.Fail($"flatten_forms failed, rc={flatten}, errorCode={error}");
                }

                _ = UpstreamNativeMethods.document_editor_flatten_all_annotations(editor, &error);

                nuint length = 0;
                byte* bytes = UpstreamNativeMethods.document_editor_save_to_bytes(editor, &length, &error);
                if (bytes == null || length == 0)
                {
                    return ScenarioResult.Fail("save_to_bytes failed after flattening");
                }
                UpstreamNativeMethods.free_bytes(bytes);
                return ScenarioResult.Ok("forms flattened and saved");
            }
            finally
            {
                UpstreamNativeMethods.document_editor_free(editor);
            }
        }
        finally
        {
            TryDelete(path);
        }
    }

    private static unsafe ScenarioResult SigningRoundtrip()
    {
        string path = TempPath("signing");
        try
        {
            if (!CreateSamplePdf(path))
            {
                return ScenarioResult.Fail(TakeLastError() ?? "sample PDF creation failed");
            }

            byte[] pdf = File.ReadAllBytes(path);
            byte[] pfx;
            const string password = "pdfoxide-test";
            using (var rsa = System.Security.Cryptography.RSA.Create(2048))
            {
                var request = new System.Security.Cryptography.X509Certificates.CertificateRequest(
                    "CN=PdfOxideTest",
                    rsa,
                    System.Security.Cryptography.HashAlgorithmName.SHA256,
                    System.Security.Cryptography.RSASignaturePadding.Pkcs1);
                using var certificate = request.CreateSelfSigned(
                    DateTimeOffset.UtcNow.AddDays(-1),
                    DateTimeOffset.UtcNow.AddDays(1));
                pfx = certificate.Export(
                    System.Security.Cryptography.X509Certificates.X509ContentType.Pfx,
                    password);
            }

            string? detail = null;
            byte[] passwordBytes = Utf8(password);
            byte[] reason = Utf8("test");
            byte[] location = Utf8("test");
            fixed (byte* pPfx = pfx)
            fixed (byte* pPassword = passwordBytes)
            fixed (byte* pPdf = pdf)
            fixed (byte* pReason = reason)
            fixed (byte* pLocation = location)
            {
                int error = 0;
                IntPtr certificate = UpstreamNativeMethods.pdf_certificate_load_from_bytes(
                    pPfx, pfx.Length, pPassword, &error);
                if (certificate == IntPtr.Zero)
                {
                    return ScenarioResult.Fail($"certificate load failed, errorCode={error}");
                }

                try
                {
                    nuint signedLength = 0;
                    byte* signedPdf = UpstreamNativeMethods.pdf_sign_bytes(
                        pPdf, (nuint)pdf.Length, certificate, pReason, pLocation, &signedLength, &error);
                    if (signedPdf == null || signedLength == 0)
                    {
                        return ScenarioResult.Fail($"sign failed, errorCode={error}");
                    }

                    try
                    {
                        IntPtr document = UpstreamNativeMethods.pdf_document_open_from_bytes(
                            signedPdf, signedLength, &error);
                        if (document == IntPtr.Zero)
                        {
                            return ScenarioResult.Fail($"signed document open failed, errorCode={error}");
                        }

                        try
                        {
                            int nativeCount = UpstreamNativeMethods.pdf_document_get_signature_count(
                                document, &error);
                            if (nativeCount < 0)
                            {
                                return ScenarioResult.Fail(
                                    $"native signature count failed, errorCode={error}");
                            }

                            var signedBytes = new byte[(int)signedLength];
                            Marshal.Copy((IntPtr)signedPdf, signedBytes, 0, signedBytes.Length);
                            int managedCount = PdfSigning.GetSignatureCount(signedBytes);
                            if (managedCount < 1)
                            {
                                return ScenarioResult.Fail(
                                    $"no signatures (native={nativeCount}, managed={managedCount})");
                            }

                            if (nativeCount > 0)
                            {
                                IntPtr signature = UpstreamNativeMethods.pdf_document_get_signature(
                                    document, 0, &error);
                                if (signature == IntPtr.Zero)
                                {
                                    return ScenarioResult.Fail($"signature 0 missing, errorCode={error}");
                                }

                                try
                                {
                                    int verify = UpstreamNativeMethods.pdf_signature_verify_detached(
                                        signature, signedPdf, signedLength, &error);
                                    if (verify != 0)
                                    {
                                        return ScenarioResult.Fail(
                                            $"verify failed, rc={verify}, errorCode={error}");
                                    }
                                }
                                finally
                                {
                                    UpstreamNativeMethods.pdf_signature_free(signature);
                                }
                            }

                            detail = $"native={nativeCount}, managed={managedCount}";
                        }
                        finally
                        {
                            UpstreamNativeMethods.pdf_document_free(document);
                        }
                    }
                    finally
                    {
                        UpstreamNativeMethods.free_bytes(signedPdf);
                    }
                }
                finally
                {
                    UpstreamNativeMethods.pdf_certificate_free(certificate);
                }
            }

            return ScenarioResult.Ok(detail ?? "self-signed sign/verify round-trip succeeded");
        }
        finally
        {
            TryDelete(path);
        }
    }

    private static unsafe ScenarioResult EditorDisposeRace()
    {
        string path = TempPath("editor-dispose-race");
        try
        {
            if (!CreateSamplePdf(path))
            {
                return ScenarioResult.Fail(TakeLastError() ?? "sample PDF creation failed");
            }

            if (!TryOpenEditor(path, string.Empty, out IntPtr editor, out int openError))
            {
                return ScenarioResult.Fail($"editor open failed, errorCode={openError}");
            }

            int stop = 0;
            var worker = new Thread(() =>
            {
                while (Volatile.Read(ref stop) == 0)
                {
                    _ = UpstreamNativeMethods.document_editor_is_modified(editor);
                }
            });
            worker.Start();
            Thread.Sleep(1);
            UpstreamNativeMethods.document_editor_free(editor);
            Volatile.Write(ref stop, 1);
            worker.Join();
            return ScenarioResult.Ok("disposed while a reader was active");
        }
        finally
        {
            TryDelete(path);
        }
    }

    private static unsafe ScenarioResult BuilderConcurrentWrites()
    {
        PdfBuilderState* state = NativeMethods.pdf_builder_create();
        if (state == null)
        {
            return ScenarioResult.Fail("builder create returned null");
        }

        try
        {
            if (NativeMethods.pdf_builder_add_page(state, 0, 0f, 0f) != 0)
            {
                return ScenarioResult.Fail(TakeLastError() ?? "add_page failed");
            }

            var threads = new Thread[4];
            for (int index = 0; index < threads.Length; index++)
            {
                int id = index;
                threads[index] = new Thread(() =>
                {
                    for (int iteration = 0; iteration < 50; iteration++)
                    {
                        byte[] text = Utf8(
                            "t" + id.ToString(CultureInfo.InvariantCulture)
                            + "-" + iteration.ToString(CultureInfo.InvariantCulture));
                        fixed (byte* pText = text)
                        {
                            _ = NativeMethods.pdf_builder_add_text(state, 0, pText, null, 10f);
                        }
                    }
                });
                threads[index].Start();
            }

            foreach (Thread thread in threads)
            {
                thread.Join();
            }

            return ScenarioResult.Ok("concurrent builder writes completed");
        }
        finally
        {
            NativeMethods.pdf_builder_free(state);
        }
    }

    // ---------------------------------------------------------------------
    // TSA lifecycle and builder error recovery (L6, M11)
    // ---------------------------------------------------------------------

    private static unsafe ScenarioResult TsaClientLifecycle()
    {
        var failures = new List<string>();
        var observations = new List<string>();
        byte[] url = Utf8("http://127.0.0.1:1");
        fixed (byte* pUrl = url)
        {
            int error = 0;
            IntPtr client = UpstreamNativeMethods.pdf_tsa_client_create(
                pUrl, null, null, 1, 0, 0, 0, &error);
            observations.Add("create-error=" + error.ToString(CultureInfo.InvariantCulture));
            if (client != IntPtr.Zero)
            {
                UpstreamNativeMethods.pdf_tsa_client_free(client);
            }

            error = 0;
            IntPtr nullUrl = UpstreamNativeMethods.pdf_tsa_client_create(
                null, null, null, 1, 0, 0, 0, &error);
            if (nullUrl != IntPtr.Zero)
            {
                failures.Add("null-url-handle");
                UpstreamNativeMethods.pdf_tsa_client_free(nullUrl);
            }
            if (error == 0)
            {
                failures.Add("null-url-error=0");
            }
        }

        UpstreamNativeMethods.pdf_tsa_client_free(IntPtr.Zero);
        return failures.Count == 0
            ? ScenarioResult.Ok(string.Join(", ", observations))
            : ScenarioResult.Fail(string.Join(", ", failures));
    }

    private static unsafe ScenarioResult TsaClientDoubleFree()
    {
        byte[] url = Utf8("http://127.0.0.1:1");
        fixed (byte* pUrl = url)
        {
            int error = 0;
            IntPtr client = UpstreamNativeMethods.pdf_tsa_client_create(
                pUrl, null, null, 1, 0, 0, 0, &error);
            if (client == IntPtr.Zero)
            {
                return ScenarioResult.Ok("client creation failed; nothing to free");
            }

            UpstreamNativeMethods.pdf_tsa_client_free(client);
            UpstreamNativeMethods.pdf_tsa_client_free(client);
            return ScenarioResult.Ok("freed the same TSA client twice");
        }
    }

    private static unsafe ScenarioResult BuilderErrorRecovery()
    {
        PdfBuilderState* state = NativeMethods.pdf_builder_create();
        if (state == null)
        {
            return ScenarioResult.Fail("builder create returned null");
        }

        try
        {
            if (NativeMethods.pdf_builder_add_page(state, 0, 0f, 0f) != 0)
            {
                return ScenarioResult.Fail(TakeLastError() ?? "add_page failed");
            }

            var failures = new List<string>();
            void Expect(string name, int actual, int expected)
            {
                if (actual != expected)
                {
                    failures.Add(name + "=" + actual.ToString(CultureInfo.InvariantCulture)
                        + " expected " + expected.ToString(CultureInfo.InvariantCulture));
                }
            }

            Expect("table-zero-rows", NativeMethods.pdf_builder_add_table(
                state, 0, null, 0, 1, 0, 0), -1);
            Expect("rect-invalid-page", NativeMethods.pdf_builder_add_rect(
                state, 5, 0f, 0f, 10f, 10f), -1);
            Expect("link-invalid-page", NativeMethods.pdf_builder_add_link_url(state, 5, null), -1);

            byte[] text = Utf8("recovered");
            fixed (byte* pText = text)
            {
                Expect("text-invalid-page", NativeMethods.pdf_builder_add_text(
                    state, 5, pText, null, 10f), -1);
                Expect("text-valid", NativeMethods.pdf_builder_add_text(
                    state, 0, pText, null, 10f), 0);
            }

            Expect("count", NativeMethods.pdf_builder_get_page_count(state), 1);
            (int status, int length) = BuildAndFree(state);
            Expect("build", status, 0);
            if (length <= 0)
            {
                failures.Add("build-length");
            }

            return failures.Count == 0
                ? ScenarioResult.Ok("builder recovered after invalid operations")
                : ScenarioResult.Fail(string.Join(", ", failures));
        }
        finally
        {
            NativeMethods.pdf_builder_free(state);
        }
    }

    // ---------------------------------------------------------------------
    // Builder extension scenarios (M2, M4-M9)
    // ---------------------------------------------------------------------

    private static unsafe ScenarioResult BuilderExtensionValidation()
    {
        PdfBuilderState* state = NativeMethods.pdf_builder_create();
        if (state == null)
        {
            return ScenarioResult.Fail("builder create returned null");
        }

        try
        {
            if (NativeMethods.pdf_builder_add_page(state, 0, 0f, 0f) != 0)
            {
                return ScenarioResult.Fail(TakeLastError() ?? "add_page failed");
            }

            var failures = new List<string>();
            var observations = new List<string>();
            void Expect(string name, int actual, int expected)
            {
                if (actual != expected)
                {
                    failures.Add(name + "=" + actual.ToString(CultureInfo.InvariantCulture)
                        + " expected " + expected.ToString(CultureInfo.InvariantCulture));
                }
            }

            byte[] empty = Utf8(string.Empty);
            byte[] url = Utf8("https://example.com");
            byte[] note = Utf8("note");
            byte[] watermark = Utf8("CONFIDENTIAL");
            byte[] reference = Utf8("[1]");
            byte[] noteText = Utf8("footnote text");
            byte[] fieldName = Utf8("field");
            byte[] options = Utf8("one\ntwo");
            byte[] selected = Utf8("one");
            byte[] stamp = Utf8("Approved");
            byte[] qrData = Utf8("hello");
            byte[] barcodeData = Utf8("1234567890");
            byte[] fontName = Utf8("BadFont");
            byte[] language = Utf8("en-US");
            byte[] customRole = Utf8("CustomRole");
            byte[] standardRole = Utf8("P");
            byte[] garbageFont = new byte[32];
            byte[] value0 = Utf8("a");
            byte[] value1 = Utf8("b");

            fixed (byte* pEmpty = empty)
            fixed (byte* pUrl = url)
            fixed (byte* pNote = note)
            fixed (byte* pWatermark = watermark)
            fixed (byte* pReference = reference)
            fixed (byte* pNoteText = noteText)
            fixed (byte* pField = fieldName)
            fixed (byte* pOptions = options)
            fixed (byte* pSelected = selected)
            fixed (byte* pStamp = stamp)
            fixed (byte* pQr = qrData)
            fixed (byte* pBarcode = barcodeData)
            fixed (byte* pFontName = fontName)
            fixed (byte* pLanguage = language)
            fixed (byte* pCustomRole = customRole)
            fixed (byte* pStandardRole = standardRole)
            fixed (byte* pGarbageFont = garbageFont)
            fixed (byte* pValue0 = value0)
            fixed (byte* pValue1 = value1)
            {
                Expect("link-invalid-page", NativeMethods.pdf_builder_add_link_url(state, 5, pUrl), -1);
                Expect("link-url", NativeMethods.pdf_builder_add_link_url(state, 0, pUrl), 0);
                Expect("link-page-negative", NativeMethods.pdf_builder_add_link_page(state, 0, -1), -1);
                Expect("link-page", NativeMethods.pdf_builder_add_link_page(state, 0, 0), 0);
                Expect("sticky-note", NativeMethods.pdf_builder_add_sticky_note(state, 0, pNote, 0), 0);
                Expect("watermark", NativeMethods.pdf_builder_add_watermark(state, 0, pWatermark), 0);
                Expect("footnote", NativeMethods.pdf_builder_add_footnote(state, 0, pReference, pNoteText), 0);
                Expect("rect", NativeMethods.pdf_builder_add_rect(state, 0, 10f, 10f, 50f, 20f), 0);
                Expect("line", NativeMethods.pdf_builder_add_line(state, 0, 0f, 0f, 10f, 10f), 0);
                Expect("circle", NativeMethods.pdf_builder_add_circle(
                    state, 0, 20f, 20f, 5f, 1, 0.5f, 0f, 0f, 0f,
                    null, 0, 0f, 1, 1f, 0f, 0f), 0);
                Expect("text-field-empty", NativeMethods.pdf_builder_add_text_field(
                    state, 0, pEmpty, 0f, 0f, 10f, 10f, pEmpty), -1);
                Expect("text-field", NativeMethods.pdf_builder_add_text_field(
                    state, 0, pField, 0f, 0f, 10f, 10f, pEmpty), 0);
                Expect("checkbox", NativeMethods.pdf_builder_add_checkbox(
                    state, 0, pField, 0f, 0f, 10f, 10f, 1), 0);
                Expect("push-button", NativeMethods.pdf_builder_add_push_button(
                    state, 0, pField, 0f, 0f, 10f, 10f, pEmpty), 0);
                Expect("signature-field", NativeMethods.pdf_builder_add_signature_field(
                    state, 0, pField, 0f, 0f, 10f, 10f), 0);
                Expect("tagged-on", NativeMethods.pdf_builder_set_tagged_pdf_ua1(state, 1), 0);
                Expect("tagged-off", NativeMethods.pdf_builder_set_tagged_pdf_ua1(state, 0), 0);
                Expect("language", NativeMethods.pdf_builder_set_language(state, pLanguage), 0);
                Expect("role-map", NativeMethods.pdf_builder_add_role_map(state, pCustomRole, pStandardRole), 0);

                observations.Add("stamp=" + NativeMethods.pdf_builder_add_stamp(state, 0, pStamp));
                observations.Add("qr=" + NativeMethods.pdf_builder_add_qr_code(state, 0, pQr, 10f, 10f, 40f));
                observations.Add("barcode=" + NativeMethods.pdf_builder_add_barcode(
                    state, 0, 0, pBarcode, 10f, 10f, 100f, 30f));
                observations.Add("combo=" + NativeMethods.pdf_builder_add_combo_box(
                    state, 0, pField, 0f, 0f, 10f, 10f, pOptions, pSelected));
                observations.Add("list=" + NativeMethods.pdf_builder_add_list_box(
                    state, 0, pField, 0f, 0f, 10f, 10f, pOptions, pSelected, 0));
                observations.Add("font-bytes=" + NativeMethods.pdf_builder_register_font_bytes(
                    state, pFontName, pGarbageFont, garbageFont.Length));

                byte** values = stackalloc byte*[2];
                values[0] = pValue0;
                values[1] = pValue1;
                float* xs = stackalloc float[2];
                xs[0] = 0f;
                xs[1] = 20f;
                float* ys = stackalloc float[2];
                ys[0] = 0f;
                ys[1] = 0f;
                float* ws = stackalloc float[2];
                ws[0] = 10f;
                ws[1] = 10f;
                float* hs = stackalloc float[2];
                hs[0] = 10f;
                hs[1] = 10f;
                observations.Add("radio=" + NativeMethods.pdf_builder_add_radio_group(
                    state, 0, pField, values, xs, ys, ws, hs, 2, pValue0));

                (int status, int length) = BuildAndFree(state);
                Expect("build", status, 0);
                if (length <= 0)
                {
                    failures.Add("build-length");
                }

                return failures.Count == 0
                    ? ScenarioResult.Ok(string.Join(", ", observations))
                    : ScenarioResult.Fail(string.Join(", ", failures));
            }
        }
        finally
        {
            NativeMethods.pdf_builder_free(state);
        }
    }

    private static unsafe ScenarioResult GraphicsArrayMismatch()
    {
        PdfBuilderState* state = NativeMethods.pdf_builder_create();
        if (state == null)
        {
            return ScenarioResult.Fail("builder create returned null");
        }

        try
        {
            if (NativeMethods.pdf_builder_add_page(state, 0, 0f, 0f) != 0)
            {
                return ScenarioResult.Fail(TakeLastError() ?? "add_page failed");
            }

            // Two dash values but a declared length of eight.
            float* dash = stackalloc float[2];
            dash[0] = 1f;
            dash[1] = 2f;

            int stroke = NativeMethods.pdf_builder_add_stroke_rect(
                state, 0, 0f, 0f, 10f, 10f, 1f, 0f, 0f, 0f, dash, 8, 0f);
            int circle = NativeMethods.pdf_builder_add_circle(
                state, 0, 10f, 10f, 5f, 1, 1f, 0f, 0f, 0f,
                dash, 8, 0f, 0, 0f, 0f, 0f);
            return ScenarioResult.Ok($"short dash arrays returned stroke={stroke}, circle={circle}");
        }
        finally
        {
            NativeMethods.pdf_builder_free(state);
        }
    }

    private static unsafe ScenarioResult FontRegistrationInvalid()
    {
        PdfBuilderState* state = NativeMethods.pdf_builder_create();
        if (state == null)
        {
            return ScenarioResult.Fail("builder create returned null");
        }

        try
        {
            if (NativeMethods.pdf_builder_add_page(state, 0, 0f, 0f) != 0)
            {
                return ScenarioResult.Fail(TakeLastError() ?? "add_page failed");
            }

            var failures = new List<string>();
            byte[] name = Utf8("BadFont");
            byte[] garbage = new byte[32];
            fixed (byte* pName = name)
            fixed (byte* pGarbage = garbage)
            {
                int registered = NativeMethods.pdf_builder_register_font_bytes(
                    state, pName, pGarbage, garbage.Length);
                if (registered != 0 && registered != -1)
                {
                    failures.Add("register=" + registered.ToString(CultureInfo.InvariantCulture));
                }

                int zero = NativeMethods.pdf_builder_register_font_bytes(state, pName, pGarbage, 0);
                if (zero != -1)
                {
                    failures.Add("zero-len=" + zero.ToString(CultureInfo.InvariantCulture));
                }

                int nullData = NativeMethods.pdf_builder_register_font_bytes(state, pName, null, 1);
                if (nullData != -1)
                {
                    failures.Add("null-data=" + nullData.ToString(CultureInfo.InvariantCulture));
                }
            }

            (int status, int length) = BuildAndFree(state);
            if (status != 0 || length <= 0)
            {
                failures.Add("build=" + status.ToString(CultureInfo.InvariantCulture));
            }

            return failures.Count == 0
                ? ScenarioResult.Ok("invalid font registration does not break the build")
                : ScenarioResult.Fail(string.Join(", ", failures));
        }
        finally
        {
            NativeMethods.pdf_builder_free(state);
        }
    }

    // ---------------------------------------------------------------------
    // Streaming table / same-size page scenarios (N1-N3, N8)
    // ---------------------------------------------------------------------

    private static unsafe ScenarioResult StreamingStateMachine()
    {
        PdfBuilderState* state = NativeMethods.pdf_builder_create();
        if (state == null)
        {
            return ScenarioResult.Fail("builder create returned null");
        }

        try
        {
            if (NativeMethods.pdf_builder_add_page(state, 0, 0f, 0f) != 0)
            {
                return ScenarioResult.Fail(TakeLastError() ?? "add_page failed");
            }

            var failures = new List<string>();
            void Expect(string name, int actual, int expected)
            {
                if (actual != expected)
                {
                    failures.Add(name + "=" + actual.ToString(CultureInfo.InvariantCulture)
                        + " expected " + expected.ToString(CultureInfo.InvariantCulture));
                }
            }

            Expect("end-without-begin", NativeMethods.pdf_builder_streaming_table_end(state), -1);
            Expect("row-without-begin", AddStreamingRow(state, "a", "b"), -1);
            Expect("begin", BeginStreamingTable(state, 0, "H1", "H2", 100f, 100f, 0, 1), 0);
            Expect("begin-twice", BeginStreamingTable(state, 0, "H1", "H2", 100f, 100f, 0, 1), -1);
            Expect("add-row", AddStreamingRow(state, "a", "b"), 0);
            Expect("end", NativeMethods.pdf_builder_streaming_table_end(state), 0);
            Expect("end-twice", NativeMethods.pdf_builder_streaming_table_end(state), -1);

            (int status, int length) = BuildAndFree(state);
            Expect("build-after-end", status, 0);
            if (length <= 0)
            {
                failures.Add("build-length");
            }

            return failures.Count == 0
                ? ScenarioResult.Ok("streaming state machine rejected invalid transitions")
                : ScenarioResult.Fail(string.Join(", ", failures));
        }
        finally
        {
            NativeMethods.pdf_builder_free(state);
        }
    }

    private static unsafe ScenarioResult StreamingAbandoned()
    {
        PdfBuilderState* state = NativeMethods.pdf_builder_create();
        if (state == null)
        {
            return ScenarioResult.Fail("builder create returned null");
        }

        try
        {
            if (NativeMethods.pdf_builder_add_page(state, 0, 0f, 0f) != 0)
            {
                return ScenarioResult.Fail(TakeLastError() ?? "add_page failed");
            }

            if (BeginStreamingTable(state, 0, "H1", "H2", 100f, 100f, 0, 0) != 0 ||
                AddStreamingRow(state, "a", "b") != 0)
            {
                return ScenarioResult.Fail(TakeLastError() ?? "streaming setup failed");
            }

            byte* data = null;
            int length = 0;
            int buildWhileOpen = NativeMethods.pdf_builder_build(state, &data, &length);
            if (buildWhileOpen != -1)
            {
                return ScenarioResult.Fail($"build should fail while open, got {buildWhileOpen}");
            }

            string? openError = TakeLastError();
            if (openError is null || !openError.Contains("streaming table", StringComparison.Ordinal))
            {
                return ScenarioResult.Fail("missing streaming-table error: " + (openError ?? "<null>"));
            }

            string path = TempPath("streaming");
            int saveWhileOpen = SaveBuilder(state, path);
            TryDelete(path);
            if (saveWhileOpen != -1)
            {
                return ScenarioResult.Fail($"save should fail while open, got {saveWhileOpen}");
            }

            if (NativeMethods.pdf_builder_streaming_table_end(state) != 0)
            {
                return ScenarioResult.Fail(TakeLastError() ?? "end failed");
            }

            (int status, int builtLength) = BuildAndFree(state);
            return status == 0 && builtLength > 0
                ? ScenarioResult.Ok($"blocked while open; built {builtLength} bytes after end")
                : ScenarioResult.Fail(TakeLastError() ?? "build after end failed");
        }
        finally
        {
            NativeMethods.pdf_builder_free(state);
        }
    }

    private static unsafe ScenarioResult StreamingColumnValidation()
    {
        PdfBuilderState* state = NativeMethods.pdf_builder_create();
        if (state == null)
        {
            return ScenarioResult.Fail("builder create returned null");
        }

        try
        {
            if (NativeMethods.pdf_builder_add_page(state, 0, 0f, 0f) != 0)
            {
                return ScenarioResult.Fail(TakeLastError() ?? "add_page failed");
            }

            var failures = new List<string>();
            void Expect(string name, int actual, int expected)
            {
                if (actual != expected)
                {
                    failures.Add(name + "=" + actual.ToString(CultureInfo.InvariantCulture)
                        + " expected " + expected.ToString(CultureInfo.InvariantCulture));
                }
            }

            byte[] header = Utf8("H");
            fixed (byte* pHeader = header)
            {
                float* widths = stackalloc float[1];
                widths[0] = 100f;
                int* aligns = stackalloc int[1];
                aligns[0] = 0;

                Expect("zero-columns", BeginStreamingTableRaw(state, 0, &pHeader, widths, aligns, 0), -1);
                Expect("negative-page", BeginStreamingTableRaw(state, -1, &pHeader, widths, aligns, 1), -1);
                Expect("out-of-range-page", BeginStreamingTableRaw(state, 1, &pHeader, widths, aligns, 1), -1);
                Expect("null-headers", BeginStreamingTableRaw(state, 0, null, widths, aligns, 1), -1);
                Expect("null-widths", BeginStreamingTableRaw(state, 0, &pHeader, null, aligns, 1), -1);
                Expect("null-aligns", BeginStreamingTableRaw(state, 0, &pHeader, widths, null, 1), -1);
            }

            Expect("nan-width", BeginStreamingTable(state, 0, "H1", "H2", float.NaN, 100f, 0, 0), -1);
            Expect("bad-align", BeginStreamingTable(state, 0, "H1", "H2", 100f, 100f, 99, 0), -1);
            Expect("begin", BeginStreamingTable(state, 0, "H1", "H2", 100f, 100f, 0, 1), 0);
            Expect("short-row", AddStreamingRowSingle(state, "only-one"), -1);
            Expect("valid-row", AddStreamingRow(state, "a", "b"), 0);
            Expect("end", NativeMethods.pdf_builder_streaming_table_end(state), 0);

            (int status, int length) = BuildAndFree(state);
            Expect("build", status, 0);
            if (length <= 0)
            {
                failures.Add("build-length");
            }

            return failures.Count == 0
                ? ScenarioResult.Ok("streaming column/row validation is enforced")
                : ScenarioResult.Fail(string.Join(", ", failures));
        }
        finally
        {
            NativeMethods.pdf_builder_free(state);
        }
    }

    private static unsafe ScenarioResult AddPageLikeExtremes()
    {
        PdfBuilderState* state = NativeMethods.pdf_builder_create();
        if (state == null)
        {
            return ScenarioResult.Fail("builder create returned null");
        }

        try
        {
            if (NativeMethods.pdf_builder_add_page(state, 0, 0f, 0f) != 0)
            {
                return ScenarioResult.Fail(TakeLastError() ?? "add_page failed");
            }

            var failures = new List<string>();
            void Expect(string name, int actual, int expected)
            {
                if (actual != expected)
                {
                    failures.Add(name + "=" + actual.ToString(CultureInfo.InvariantCulture)
                        + " expected " + expected.ToString(CultureInfo.InvariantCulture));
                }
            }

            Expect("negative", NativeMethods.pdf_builder_add_page_like(state, -1), -1);
            Expect("too-large", NativeMethods.pdf_builder_add_page_like(state, int.MaxValue), -1);
            Expect("valid", NativeMethods.pdf_builder_add_page_like(state, 0), 1);
            Expect("count", NativeMethods.pdf_builder_get_page_count(state), 2);

            return failures.Count == 0
                ? ScenarioResult.Ok("add_page_like rejects invalid references")
                : ScenarioResult.Fail(string.Join(", ", failures));
        }
        finally
        {
            NativeMethods.pdf_builder_free(state);
        }
    }

    private static unsafe int BeginStreamingTable(
        PdfBuilderState* state,
        int pageIndex,
        string firstHeader,
        string secondHeader,
        float firstWidth,
        float secondWidth,
        int firstAlign,
        int secondAlign)
    {
        byte[] h0 = Utf8(firstHeader);
        byte[] h1 = Utf8(secondHeader);
        fixed (byte* p0 = h0)
        fixed (byte* p1 = h1)
        {
            byte** headers = stackalloc byte*[2];
            headers[0] = p0;
            headers[1] = p1;

            float* widths = stackalloc float[2];
            widths[0] = firstWidth;
            widths[1] = secondWidth;

            int* aligns = stackalloc int[2];
            aligns[0] = firstAlign;
            aligns[1] = secondAlign;

            return NativeMethods.pdf_builder_streaming_table_begin(
                state, pageIndex, headers, widths, aligns, 2,
                1, 1, 0.9f, 0.9f, 0.9f,
                1, 0.8f, 0.8f, 0.8f, 0.5f,
                4f, 2f, 2f, 2);
        }
    }

    private static unsafe int BeginStreamingTableRaw(
        PdfBuilderState* state,
        int pageIndex,
        byte** headers,
        float* widths,
        int* aligns,
        int columnCount)
    {
        return NativeMethods.pdf_builder_streaming_table_begin(
            state, pageIndex, headers, widths, aligns, columnCount,
            0, 0, 0f, 0f, 0f,
            0, 0f, 0f, 0f, 0f,
            0f, 0f, 0f, 1);
    }

    private static unsafe int AddStreamingRow(PdfBuilderState* state, string first, string second)
    {
        byte[] c0 = Utf8(first);
        byte[] c1 = Utf8(second);
        fixed (byte* p0 = c0)
        fixed (byte* p1 = c1)
        {
            byte** cells = stackalloc byte*[2];
            cells[0] = p0;
            cells[1] = p1;

            int* rowspans = stackalloc int[2];
            rowspans[0] = 1;
            rowspans[1] = 1;

            return NativeMethods.pdf_builder_streaming_table_add_row(state, cells, rowspans, 2);
        }
    }

    private static unsafe int AddStreamingRowSingle(PdfBuilderState* state, string text)
    {
        byte[] encoded = Utf8(text);
        fixed (byte* pointer = encoded)
        {
            byte** cells = stackalloc byte*[1];
            cells[0] = pointer;
            return NativeMethods.pdf_builder_streaming_table_add_row(state, cells, null, 1);
        }
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private static unsafe int AddText(PdfBuilderState* state, int pageIndex, string text)
    {
        byte[] encoded = Utf8(text);
        fixed (byte* pointer = encoded)
        {
            return NativeMethods.pdf_builder_add_text(state, pageIndex, pointer, null, 0f);
        }
    }

    private static unsafe int SaveBuilder(PdfBuilderState* state, string path)
    {
        byte[] encoded = Utf8(path);
        fixed (byte* pointer = encoded)
        {
            return NativeMethods.pdf_builder_save(state, pointer);
        }
    }

    private static unsafe (int Status, int Length) BuildAndFree(PdfBuilderState* state)
    {
        byte* data = null;
        int length = 0;
        int status = NativeMethods.pdf_builder_build(state, &data, &length);
        if (status == 0 && data != null && length > 0)
        {
            NativeMethods.pdf_builder_bytes_free(data, length);
        }

        return (status, length);
    }

    private static unsafe string? TakeLastError()
    {
        byte* pointer = NativeMethods.pdf_last_error_message();
        if (pointer == null)
        {
            return null;
        }

        try
        {
            int length = NativeMethods.pdf_get_string_len(pointer);
            return length <= 0 ? string.Empty : Encoding.UTF8.GetString(pointer, length);
        }
        finally
        {
            NativeMethods.pdf_string_free(pointer);
        }
    }

    private static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value + "\0");

    private static string TempPath(string suffix) =>
        Path.Combine(Path.GetTempPath(), $"pdf_oxide_crash_{suffix}_{Guid.NewGuid():N}.pdf");

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static class TestHooks
    {
        [DllImport("pdfoxide_bindings", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern void pdfoxide_test_panic(int kind);
    }
}
