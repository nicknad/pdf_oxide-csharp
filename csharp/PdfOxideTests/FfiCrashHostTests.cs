using PdfOxideTests.Support;
using Xunit;

namespace PdfOxideTests;

/// <summary>
/// Process-isolated FFI scenarios. A native access violation or abort must not be
/// able to take down the test runner.
/// </summary>
public class FfiCrashHostTests
{
    [Fact]
    public void TestHostNoopSucceeds()
    {
        CrashHostResult result = CrashHostRunner.Run("noop");
        Assert.Equal(0, result.ExitCode);
        Assert.True(result.ReportedOk);
    }

    [Fact]
    public void TestNullPathReportsError()
    {
        CrashHostResult result = CrashHostRunner.Run("null-path-extract");
        Assert.Equal(0, result.ExitCode);
        Assert.True(result.ReportedOk);
        Assert.Contains("must not be null", result.StandardOutput, System.StringComparison.Ordinal);
    }

    [Fact]
    public void TestNullBuilderStateRejectsEveryOperation()
    {
        CrashHostResult result = CrashHostRunner.Run("null-builder-ops");
        Assert.Equal(0, result.ExitCode);
        Assert.True(result.ReportedOk);
    }

    [Fact]
    public void TestNullOutPointersAreRejected()
    {
        CrashHostResult result = CrashHostRunner.Run("null-out-pointers");
        Assert.Equal(0, result.ExitCode);
        Assert.True(result.ReportedOk);
    }

    [Fact]
    public void TestNullFreesAreNoOps()
    {
        CrashHostResult result = CrashHostRunner.Run("free-null");
        Assert.Equal(0, result.ExitCode);
        Assert.True(result.ReportedOk);
    }

    [Fact]
    public void TestImagesWithNullElementReportsError()
    {
        CrashHostResult result = CrashHostRunner.Run("images-null-element");
        Assert.Equal(0, result.ExitCode);
        Assert.True(result.ReportedOk);
    }

    [Fact]
    public void TestTableWithNullCellReportsError()
    {
        CrashHostResult result = CrashHostRunner.Run("table-null-cell");
        Assert.Equal(0, result.ExitCode);
        Assert.True(result.ReportedOk);
    }

    [Fact]
    public void TestPageIndexExtremesAreRejected()
    {
        CrashHostResult result = CrashHostRunner.Run("page-index-extremes");
        Assert.Equal(0, result.ExitCode);
        Assert.True(result.ReportedOk);
    }

    [Fact]
    public void TestEmptyBuilderBuildIsNotMalformed()
    {
        CrashHostResult result = CrashHostRunner.Run("empty-pdf-build");
        Assert.Equal(0, result.ExitCode);
        Assert.False(result.ReportedManagedException);
        Assert.True(result.ReportedOk || result.ReportedError);
    }

    [Fact]
    public void TestBuilderIsReusableAfterSave()
    {
        CrashHostResult result = CrashHostRunner.Run("reuse-after-save");
        Assert.Equal(0, result.ExitCode);
        Assert.True(result.ReportedOk);
    }

    [Fact]
    public void TestEditorErrorCodeContract()
    {
        CrashHostResult result = CrashHostRunner.Run("editor-error-code-contract");
        Assert.Equal(0, result.ExitCode);
        Assert.True(result.ReportedOk);
    }

    [Fact]
    public void TestEditorFromBytesValidation()
    {
        CrashHostResult result = CrashHostRunner.Run("editor-from-bytes-validation");
        Assert.Equal(0, result.ExitCode);
        Assert.True(result.ReportedOk);
    }

    [Fact]
    public void TestEditorPageEditExtremes()
    {
        CrashHostResult result = CrashHostRunner.Run("editor-page-edit-extremes");
        Assert.Equal(0, result.ExitCode);
        Assert.True(result.ReportedOk);
    }

    [Fact]
    public void TestEditorMergeExtract()
    {
        CrashHostResult result = CrashHostRunner.Run("editor-merge-extract");
        Assert.Equal(0, result.ExitCode);
        Assert.True(result.ReportedOk);
    }

    [Fact]
    public void TestEditorEncryptRoundtrip()
    {
        CrashHostResult result = CrashHostRunner.Run("editor-encrypt-roundtrip");
        Assert.Equal(0, result.ExitCode);
        Assert.True(result.ReportedOk);
    }

    [Fact]
    public void TestCertificateInvalidInput()
    {
        CrashHostResult result = CrashHostRunner.Run("certificate-invalid-input");
        Assert.Equal(0, result.ExitCode);
        Assert.True(result.ReportedOk);
    }

    [Fact]
    public void TestSignatureListEmpty()
    {
        CrashHostResult result = CrashHostRunner.Run("signature-list-empty");
        Assert.Equal(0, result.ExitCode);
        Assert.True(result.ReportedOk);
    }

    [Fact]
    public void TestBuilderExtensionValidation()
    {
        CrashHostResult result = CrashHostRunner.Run("builder-extension-validation");
        Assert.Equal(0, result.ExitCode);
        Assert.True(result.ReportedOk);
    }

    [Fact]
    public void TestFontRegistrationInvalid()
    {
        CrashHostResult result = CrashHostRunner.Run("font-registration-invalid");
        Assert.Equal(0, result.ExitCode);
        Assert.True(result.ReportedOk);
    }

    [Fact]
    public void TestStreamingStateMachine()
    {
        CrashHostResult result = CrashHostRunner.Run("streaming-state-machine");
        Assert.Equal(0, result.ExitCode);
        Assert.True(result.ReportedOk);
    }

    [Fact]
    public void TestAbandonedStreamingTableBlocksSaveAndBuild()
    {
        CrashHostResult result = CrashHostRunner.Run("streaming-abandoned");
        Assert.Equal(0, result.ExitCode);
        Assert.True(result.ReportedOk);
    }

    [Fact]
    public void TestStreamingColumnValidation()
    {
        CrashHostResult result = CrashHostRunner.Run("streaming-column-validation");
        Assert.Equal(0, result.ExitCode);
        Assert.True(result.ReportedOk);
    }

    [Fact]
    public void TestAddPageLikeExtremes()
    {
        CrashHostResult result = CrashHostRunner.Run("add-page-like-extremes");
        Assert.Equal(0, result.ExitCode);
        Assert.True(result.ReportedOk);
    }

    [Fact]
    public void TestPanicHookTerminatesTheProcess()
    {
        CrashHostResult result = CrashHostRunner.Run("panic-hook", "0");
        if (result.StandardOutput.Contains("hook-unavailable", System.StringComparison.Ordinal))
        {
            Assert.Equal(0, result.ExitCode);
            return;
        }

        Assert.NotEqual(0, result.ExitCode);
        Assert.NotEqual(2, result.ExitCode);
    }

    /// <summary>
    /// These scenarios intentionally exercise undefined behavior (double free,
    /// use-after-free, out-of-bounds reads). They may terminate the process
    /// gracefully or with a native fault, but they must never surface as an
    /// unexpected managed exception. Under ASAN/PageHeap they become hard failures.
    /// </summary>
    [Theory]
    [InlineData("double-free-string")]
    [InlineData("double-free-builder")]
    [InlineData("use-after-free-builder")]
    [InlineData("free-wrong-length")]
    [InlineData("image-len-overflow")]
    [InlineData("table-short-cells")]
    [InlineData("rich-short-colors")]
    [InlineData("editor-double-free")]
    [InlineData("editor-from-bytes-overflow")]
    [InlineData("certificate-null-input")]
    [InlineData("signature-null-handle")]
    [InlineData("graphics-array-mismatch")]
    public void TestMemorySafetyScenarioTerminatesWithoutManagedFailure(string scenario)
    {
        CrashHostResult result = CrashHostRunner.Run(scenario);
        Assert.False(result.ReportedManagedException, result.StandardOutput + result.StandardError);
        Assert.NotEqual(2, result.ExitCode);
    }
}
