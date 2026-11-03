# B. Memory ownership / lifetime scenarios

Highest-risk area: every pointer crossing the ABI has a single valid owner and a
single valid free function.

| # | Scenario | How to test | Why |
| --- | --- | --- | --- |
| B1 | Every returned string freed exactly once | Wrap each API, call 1M times, sample RSS/private bytes, run LeakSanitizer/ASAN | `CString::into_raw` is only reclaimed by `pdf_string_free` (`rust/src/lib.rs:4485-4491`) |
| B2 | Every returned byte buffer freed exactly once | Loop DOCX/PPTX/XLSX/`builder_build` and upstream `*_to_bytes`; free with the matching allocator | Shim buffers need `pdf_bytes_free(data, len)` / `pdf_builder_bytes_free(data, len)` (`rust/src/lib.rs:4357-4371`); upstream buffers need `free_bytes(ptr)` |
| B3 | Free with NULL is a no-op | `pdf_string_free(null)`, `pdf_bytes_free(null, n)`, `pdf_builder_free(null)` | Current code guards this; test prevents regressions |
| B4 | Double free / free after free | Native driver calls free twice or uses a pointer after free, in a child process | UB by contract; assert the process dies fast and predictably rather than corrupting silently |
| B5 | Wrong-length free | Return `len+1`, `len-1`, `0`, and negative lengths to `pdf_bytes_free` | Detects silent heap corruption under ASAN/PageHeap |
| B6 | C# never frees native memory through the wrong allocator | Static scan plus runtime assertion that only the matching free function is used in `finally` | Rust alloc/free must stay paired across the C ABI |
| B7 | Builder double-dispose / finalizer after dispose | Call `Dispose()` twice, then force `GC.Collect` and `WaitForPendingFinalizers` | `pdf_builder_free` calls `Box::from_raw` (`rust/src/lib.rs:2305-2309`); `DocumentBuilder` has both `Dispose` and a finalizer with no lock (`DocumentBuilder.cs:1186-1209`) |
| B8 | Use-after-free of the builder from native code | Child-process driver: `pdf_builder_free` then `pdf_builder_add_text` | Distinguish a graceful `-1` (desired) from an access violation |
| B9 | Pointer arguments are copied, never retained | Free/collect the managed buffer immediately after a call returns, then keep using the builder | `add_image_bytes` copies into a `Vec` (`rust/src/lib.rs:2770-2806`); verify no borrow escapes |
| B10 | Out-parameters untouched on failure | Pre-set `out_data`/`out_len` sentinels, force a failure, assert they are unchanged | Prevents C# from freeing garbage on the error path (`ReadBytes` throws before free) |
| B11 | Dual allocator confusion | Free a shim buffer with upstream `free_bytes`, and an upstream buffer with `pdf_bytes_free`; run in a child process under ASAN/PageHeap | Shim `ffi_bytes` returns `Box<[u8]>` (`rust/src/lib.rs:1151-1181`); upstream `free_bytes` is `libc::free` with no length (`UpstreamNativeMethods.cs:69-74`) |
| B12 | Editor handle ownership | Open via `pdfoxide_editor_open*`, free via `document_editor_free`; wrong-free, double-free, use-after-free | The password shim boxes an upstream `DocumentEditor` (`rust/src/lib.rs:4430-4449`) |
| B13 | Certificate / TSA / signature handles | Double-dispose, use-after-dispose, and finalizer races for `PdfSigningCertificate` and `PdfTimestampClient` | These wrappers have no lock around `Dispose` (`PdfSigning.cs:109-132`, `:1267-1276`) |
| B14 | Borrowed timestamp token | Free the timestamp, then use/free the token pointer in a child process | `pdf_timestamp_get_token` returns a borrowed pointer (`PdfSigning.cs:1242-1261`) |
| B15 | Concurrent dispose | Two threads dispose the same builder/editor/certificate/TSA handle; run under TSan | Managed `_disposed` checks are non-atomic and native free is not idempotent |
| B16 | Abandoned streaming table | Begin a table, add rows, never call end, then free the builder; sample memory and verify save/build refusal | Rows accumulate in `PendingStreamingTable.rows` until end or builder free (`rust/src/lib.rs:2887-2892`) |
