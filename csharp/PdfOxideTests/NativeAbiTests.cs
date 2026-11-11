using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using PdfOxide;
using Xunit;

namespace PdfOxideTests;

/// <summary>
/// Scenario A1: every managed <see cref="DllImportAttribute"/> entry point must
/// exist in the shipped native library. This catches <c>export_name</c> versus
/// <c>no_mangle</c> mismatches before they become runtime failures.
/// </summary>
public class NativeAbiTests
{
    [Fact]
    public void TestEveryDllImportEntryPointIsExported()
    {
        string libraryPath = FindNativeLibrary();
        IntPtr handle = NativeLibrary.Load(libraryPath);

        try
        {
            var missing = new List<string>();
            HashSet<string> seen = [with(StringComparer.Ordinal)];
            int checkedCount = 0;

            foreach (MethodInfo method in typeof(NativeMethods).GetMethods(
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (method.DeclaringType != typeof(NativeMethods))
                {
                    continue;
                }

                DllImportAttribute? import = method.GetCustomAttribute<DllImportAttribute>();
                if (import is null)
                {
                    continue;
                }

                string entryPoint = string.IsNullOrEmpty(import.EntryPoint)
                    ? method.Name
                    : import.EntryPoint;

                if (!seen.Add(entryPoint))
                {
                    continue;
                }

                checkedCount++;
                if (!NativeLibrary.TryGetExport(handle, entryPoint, out _))
                {
                    missing.Add(method.Name + " -> " + entryPoint);
                }
            }

            Assert.True(checkedCount > 0, "No DllImport methods were discovered on NativeMethods.");
            Assert.True(
                missing.Count == 0,
                $"The native library is missing managed entry points: {string.Join(", ", missing)}");
        }
        finally
        {
            NativeLibrary.Free(handle);
        }
    }

    private static string FindNativeLibrary()
    {
        string fileName = OperatingSystem.IsWindows()
            ? "pdfoxide_bindings.dll"
            : OperatingSystem.IsMacOS()
                ? "libpdfoxide_bindings.dylib"
                : "libpdfoxide_bindings.so";

        string candidate = Path.Combine(AppContext.BaseDirectory, fileName);
        return File.Exists(candidate)
            ? candidate
            : throw new FileNotFoundException(
                $"Native library '{fileName}' was not copied next to the test assembly.",
                candidate);
    }
}
