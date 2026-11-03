# FFI Test Scenarios (Rust cdylib <-> .NET)

Design catalogue for stress-testing the `pdfoxide_bindings` C ABI: fuzzing, partial
crashes, memory ownership, concurrency, resource exhaustion, and loader/ABI drift.

Each scenario group lives in its own file so it can be implemented and reviewed
independently.

| Group | File | Focus |
| --- | --- | --- |
| A | [A-abi-symbol-loader.md](A-abi-symbol-loader.md) | ABI, exported symbols, loader/RID, version skew |
| B | [B-memory-ownership-lifetime.md](B-memory-ownership-lifetime.md) | Alloc/free ownership, double free, use-after-free |
| C | [C-input-validation-boundaries.md](C-input-validation-boundaries.md) | NULLs, empty strings, numeric extremes, length mismatches |
| D | [D-parser-fuzzing.md](D-parser-fuzzing.md) | Fuzzing the PDF parser through the FFI |
| E | [E-builder-sequence-fuzzing.md](E-builder-sequence-fuzzing.md) | Stateful `DocumentBuilder` operation sequences |
| F | [F-error-channel.md](F-error-channel.md) | Thread-local error channel semantics |
| G | [G-concurrency.md](G-concurrency.md) | Parallel documents, shared builder, dispose races |
| H | [H-resource-process-robustness.md](H-resource-process-robustness.md) | Leaks, OOM, panics, native faults |
| I | [I-crash-harness.md](I-crash-harness.md) | Process isolation and crash-oracle design |
| J | [J-instrumentation.md](J-instrumentation.md) | Sanitizers, fuzzers, and tooling matrix |

## FFI surface under test

| Class | Functions | Contract |
| --- | --- | --- |
| String-returning | `pdf_extract_text*`, `pdf_to_markdown*`, `pdf_to_plain_text*`, `pdf_to_html*`, `pdf_extract_{images,files,fonts,tables,words,text_lines,chars,spans,paths}`, `pdf_get_*` | return a Rust `CString` or NULL; free with `pdf_string_free` |
| Status-returning | `pdf_get_page_count`, `pdf_from_markdown`, `pdf_from_text`, `pdf_images_to_pdf`, all builder ops | `0`/`>=0` = success, `-1` = error, message via `pdf_last_error_message` |
| Out-buffer | `pdf_to_{docx,pptx,xlsx}_bytes`, `pdf_builder_build` | writes `*mut u8` + `i32`; free with `pdf_bytes_free` / `pdf_builder_bytes_free` |
| Opaque handle | `pdf_builder_*` | `PdfBuilderState*`, create/free, no synchronization |
| Support | `pdf_last_error_message`, `pdf_get_string_len`, `pdf_string_free` | thread-local error, byte length, deallocation |

## Implemented harness (current state)

- `csharp/FfiCrashHost` - process-isolated driver for crash/UB scenarios. Takes a
  scenario id and arguments, prints `result=ok` or `result=error ...`, and exits
  non-zero only on unexpected managed failures or native faults.
- `csharp/PdfOxideTests/FfiCrashHostTests.cs` - xUnit scenario stubs that spawn the
  host so a native access violation or abort cannot kill the test runner.
- `rust/fuzz` - `cargo fuzz` targets for the document API and the builder API.

See [I-crash-harness.md](I-crash-harness.md) for the isolation contract and exit
codes.

## High-value targets found while reading the code

1. `CString::new` on extracted text (`rust/src/lib.rs:378`, `:438`, `:446`, `:975`) -
   one NUL byte in decoded text fails the whole extraction.
2. `serde_json` + non-finite floats (`rust/src/lib.rs:539`) - NaN geometry in a PDF
   makes the whole JSON API return NULL.
3. `add_rich_paragraph` does not validate the `colors` array length
   (`rust/src/lib.rs:1767`) - out-of-bounds read for non-.NET callers.
4. `add_table` / `images_to_pdf` / `add_image_bytes` trust caller-supplied counts and
   lengths (`rust/src/lib.rs:1888`, `:1364`, `:1973`).
5. `pdf_get_page_count` casts `usize as i32` (`rust/src/lib.rs:463`) - wraps negative
   on overflow.
6. `LAST_ERROR` is never cleared (`rust/src/lib.rs:2136`) - stale errors.
7. No `catch_unwind` on any `extern "C"` function - any panic aborts the host process.
8. Recursive `OutlineItemDto` / XMP conversion (`rust/src/lib.rs:858`) - deep
   structures can stack-overflow.
9. `DocumentBuilder` finalizer plus `Dispose` (`csharp/PdfOxideBindings/DocumentBuilder.cs:450`)
   is a double-free risk if the native handle is not idempotent.
