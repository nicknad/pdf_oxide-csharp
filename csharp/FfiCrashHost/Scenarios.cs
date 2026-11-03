using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using PdfOxide;

namespace FfiCrashHost;

internal sealed record ScenarioResult(string Status, string? Error = null, string? Detail = null)
{
    internal static ScenarioResult Ok(string? detail = null) => new("ok", null, detail);

    internal static ScenarioResult Fail(string? error, string? detail = null) => new("error", error, detail);
}

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
