# A. ABI / symbol / loader scenarios

| # | Scenario | How to test | Why |
| --- | --- | --- | --- |
| A1 | Every `DllImport` EntryPoint exists in the shipped native library | Reflect over `NativeMethods`, then call every exported name via `NativeLibrary.Load` + `GetExport` | `export_name` vs `no_mangle` mismatches (for example `pdfoxide_csharp_pdf_get_page_count` at `rust/src/lib.rs:460`) otherwise fail only at runtime |
| A2 | Missing library reports a clear load error | Delete or rename the `.dll`/`.so`, invoke a call, assert `DllNotFoundException` names the library | Packaging and RID regressions must fail loudly |
| A3 | Calling convention and width correctness | Call each category once with known inputs; also use a small C driver that `dlopen`/`LoadLibrary`s the cdylib | Verifies `Cdecl`, `f32`/`i32`/pointer marshalling independently of .NET |
| A4 | Old native binary with new managed package | Ship the previous `.so`/`.dll`, run the new test suite | New entrypoints or semantics must fail deterministically instead of calling into garbage |
| A5 | Wrong RID binary | Load the `linux-x64` asset on Windows (and vice versa) | Guards `runtimes/<rid>/native` packaging |
| A6 | Loader/unload races | Load in a collectible `AssemblyLoadContext`, unload while a call is running | Documented as unsupported, but must not silently corrupt |
| A7 | Argument ABI and signature skew | Build old and new native libraries and call the 13 `page_index` builder functions through the generated bindings in a child process | Inserting a parameter changes register/stack argument positions; A1 checks only that symbols exist |
