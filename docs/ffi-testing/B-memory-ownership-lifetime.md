# B. Memory ownership / lifetime scenarios

Highest-risk area: every pointer crossing the ABI has a single valid owner and a
single valid free function.

| # | Scenario | How to test | Why |
| --- | --- | --- | --- |
| B1 | Every returned string freed exactly once | Wrap each API, call 1M times, sample RSS/private bytes, run LeakSanitizer/ASAN | `CString::into_raw` is only reclaimed by `pdf_string_free` (`rust/src/lib.rs:2092`) |
| B2 | Every returned byte buffer freed exactly once | Loop DOCX/PPTX/XLSX/`builder_build`, free with the matching function | `Box::from_raw(slice_from_raw_parts_mut(data, len))` depends on the exact returned `len` |
| B3 | Free with NULL is a no-op | `pdf_string_free(null)`, `pdf_bytes_free(null, n)`, `pdf_builder_free(null)` | Current code guards this; test prevents regressions |
| B4 | Double free / free after free | Native driver calls free twice or uses a pointer after free, in a child process | UB by contract; assert the process dies fast and predictably rather than corrupting silently |
| B5 | Wrong-length free | Return `len+1`, `len-1`, `0`, and negative lengths to `pdf_bytes_free` | Detects silent heap corruption under ASAN/PageHeap |
| B6 | C# never frees native memory through the wrong allocator | Static scan plus runtime assertion that only `pdf_*_free` is used in `finally` | Rust alloc/free must stay paired across the C ABI |
| B7 | Builder double-dispose / finalizer after dispose | Call `Dispose()` twice, then force `GC.Collect` and `WaitForPendingFinalizers` | `DocumentBuilder` has both `Dispose` and `~DocumentBuilder`; `Box::from_raw` twice is UAF (`rust/src/lib.rs:1484`) |
| B8 | Use-after-free of the builder from native code | Child-process driver: `pdf_builder_free` then `pdf_builder_add_text` | Distinguish a graceful `-1` (desired) from an access violation |
| B9 | Pointer arguments are copied, never retained | Free/collect the managed buffer immediately after a call returns, then keep using the builder | `add_image_bytes` copies into a `Vec` (`rust/src/lib.rs:1973`); verify no borrow escapes |
| B10 | Out-parameters untouched on failure | Pre-set `out_data`/`out_len` sentinels, force a failure, assert they are unchanged | Prevents C# from freeing garbage on the error path (`ReadBytes` throws before free) |
