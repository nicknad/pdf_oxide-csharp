# FFI Test Scenarios (Rust cdylib <-> .NET)

Design catalogue for stress-testing the `pdfoxide_bindings` C ABI: fuzzing, partial
crashes, memory ownership, concurrency, resource exhaustion, and loader/ABI drift.

Each scenario group lives in its own file so it can be implemented and reviewed
independently. Re-evaluate this catalogue whenever trunk commits change the FFI
surface.

| Group | File | Focus |
| --- | --- | --- |
| A | [A-abi-symbol-loader.md](A-abi-symbol-loader.md) | ABI, exported symbols, loader/RID, version skew |
| B | [B-memory-ownership-lifetime.md](B-memory-ownership-lifetime.md) | Alloc/free ownership, double free, use-after-free |
| C | [C-input-validation-boundaries.md](C-input-validation-boundaries.md) | NULLs, empty strings, numeric extremes, length mismatches |
| D | [D-parser-fuzzing.md](D-parser-fuzzing.md) | Fuzzing the PDF parser through the FFI |
| E | [E-builder-sequence-fuzzing.md](E-builder-sequence-fuzzing.md) | Stateful builder operation sequences |
| F | [F-error-channel.md](F-error-channel.md) | Thread-local error and `error_code` channel semantics |
| G | [G-concurrency.md](G-concurrency.md) | Parallel documents, shared handles, dispose races |
| H | [H-resource-process-robustness.md](H-resource-process-robustness.md) | Leaks, OOM, panics, native faults |
| I | [I-crash-harness.md](I-crash-harness.md) | Process isolation and crash-oracle design |
| J | [J-instrumentation.md](J-instrumentation.md) | Sanitizers, fuzzers, and tooling matrix |
| K | [K-editor-encryption-redaction.md](K-editor-encryption-redaction.md) | Editor handles, page editing, encryption, redaction |
| L | [L-signing-timestamp.md](L-signing-timestamp.md) | Certificates, signatures, verification, TSA |
| M | [M-builder-forms-annotations-graphics.md](M-builder-forms-annotations-graphics.md) | Forms, annotations, links, watermarks, barcodes, graphics, fonts, tagged PDF |
| N | [N-streaming-builder.md](N-streaming-builder.md) | Streaming tables and same-size page builder |

## FFI surface under test

| Class | Functions | Contract |
| --- | --- | --- |
| String-returning | `pdf_extract_text*`, `pdf_to_markdown*`, `pdf_to_plain_text*`, `pdf_to_html*`, `pdf_extract_{images,files,fonts,tables,words,text_lines,chars,spans,paths}`, `pdf_get_*` | return a Rust `CString` or NULL; free with `pdf_string_free` |
| Status-returning | `pdf_get_page_count`, `pdf_from_markdown`, `pdf_from_text`, `pdf_images_to_pdf`, all builder ops | `0`/`>=0` = success, `-1` = error, message via `pdf_last_error_message` |
| Out-buffer (shim) | `pdf_to_{docx,pptx,xlsx}_bytes`, `pdf_builder_build` | writes `*mut u8` + `i32`; free with `pdf_bytes_free` / `pdf_builder_bytes_free` using the exact length |
| Out-buffer (upstream) | `pdf_save_to_bytes`, `document_editor_save*_to_bytes`, signing/merge outputs | writes `*mut u8` + `nuint`; free with upstream `free_bytes(ptr)` (no length) |
| Out error code | `pdfoxide_editor_open*_with_password` | writes `int* error_code` (`0`/`-1`) in addition to TLS |
| Opaque handle (shim) | `pdf_builder_*` | `PdfBuilderState*`, create/free, no synchronization |
| Opaque handle (upstream) | `document_editor_*`, `pdf_document_*`, certificate/signature/TSA/timestamp, annotation/form lists | free with the matching upstream `*_free`; editor access must be serialized |
| Builder extensions | forms, links, annotations, watermarks, stamps, footnotes, barcodes, vector graphics, font registration, tagged PDF | mostly `RC` with caller-owned arrays/counts and no separate length parameter |
| Streaming builder | `pdf_builder_streaming_table_begin/add_row/end`, `pdf_builder_add_page_like` | `RC`/index; one open table per builder; save/build refused until `end` |
| Support | `pdf_last_error_message`, `pdf_get_string_len`, `pdf_string_free` | thread-local error, byte length, deallocation |

## Implemented harness (current state)

- `csharp/FfiCrashHost` - process-isolated driver for crash/UB scenarios. Takes a
  scenario id and arguments, prints `result=ok` or `result=error ...`, and exits
  non-zero only on unexpected managed failures or native faults.
- `csharp/PdfOxideTests/FfiCrashHostTests.cs` - xUnit scenario stubs that spawn the
  host so a native access violation or abort cannot kill the test runner.
- `csharp/PdfOxideTests/NativeAbiTests.cs` - scenario A1: reflects over every
  `DllImport` entry point and verifies the shipped native library exports it.
- `rust/fuzz` - `cargo fuzz` targets for the document API and the builder API.

Groups K-N are largely design-only; `FfiCrashHost` now covers editor handle
contracts (K1-K3), streaming state (N1-N3), and `add_page_like` extremes (N8). See
[I-crash-harness.md](I-crash-harness.md) for the isolation contract and exit codes.

## Re-evaluation checklist (when trunk advances)

1. Fetch and diff against the last recorded revision:
   `git log --oneline <revision>..main -- rust/src/lib.rs csharp/PdfOxideBindings dotnet/PdfOxideBindings.cs`
2. Enumerate new or changed `pub extern "C"` exports and new `DllImport` declarations.
3. Re-run `NativeAbiTests` (A1) so new symbols are covered automatically.
4. Add rows to the relevant group: new handles usually belong in B/G/I, new builders in C/E/M/N.
5. Record the revision and commits under "Catalogue revisions".
6. Validate with `dotnet test` and `cargo check --manifest-path rust/fuzz/Cargo.toml`.

## Catalogue revisions

- 2026-11-03: initial A-J catalogue for the `c55f221` FFI surface.
- 2026-11-03: re-evaluated trunk commits `5fc0822`, `ca8061a`, `0bc9aaa`, `7e8942e`
  (editor, encryption, redaction, signing, annotations, forms, barcodes, graphics,
  fonts, tagged PDF). Added K-M and new cross-cutting rows. Implemented A1 in
  `NativeAbiTests.cs`.
- 2026-11-03: re-evaluated `4997b91` (streaming tables and same-size page builder).
  Added N and streaming rows to B/C/D/E/G/I.

## High-value targets found while reading the code

1. Two incompatible byte-buffer allocators: shim `ffi_bytes` / `pdf_builder_build`
   return `Box<[u8]>` freed with `pdf_bytes_free(data, len)` /
   `pdf_builder_bytes_free(data, len)` (`rust/src/lib.rs:1151-1181`, `:4317-4371`),
   while upstream buffers use `free_bytes(ptr)` with no length
   (`csharp/PdfOxideBindings/UpstreamNativeMethods.cs:69-74`). Wrong pairing or a
   wrong length corrupts the heap.
2. `pdf_builder_free` is not idempotent (`rust/src/lib.rs:2305-2309`), and
   `DocumentBuilder` has both `Dispose` and a finalizer with no lock
   (`DocumentBuilder.cs:1186-1209`); concurrent disposal can double-free.
3. The editor password shim boxes an upstream `DocumentEditor` and C# frees it with
   upstream `document_editor_free` (`rust/src/lib.rs:4430-4449`,
   `PdfEditor.cs:1194`); any version skew breaks allocator/type assumptions.
4. `pdf_builder_add_rich_paragraph` reads `colors[index*3..+2]` without a color
   length parameter (`rust/src/lib.rs:2588-2592`).
5. `pdf_builder_add_table` trusts `rows*cols` and the cell-array length
   (`rust/src/lib.rs:2709-2727`).
6. `pdf_builder_add_radio_group` trusts `count` and five parallel arrays
   (`rust/src/lib.rs:3380-3403`).
7. Graphics helpers trust caller-supplied dash/point/path counts
   (`rust/src/lib.rs:3529-3691`).
8. `add_image_bytes` and `register_font_bytes` trust `len`
   (`rust/src/lib.rs:2770-2806`, `:4213-4236`).
9. `pdf_images_to_pdf` validates the top-level array but not elements or an upper
   bound (`rust/src/lib.rs:1996-2012`).
10. `LAST_ERROR` is never cleared on success (`rust/src/lib.rs:54-56`,
    `:4529-4533`), and shim TLS is mixed with upstream `error_code` channels.
11. Certificate/signature/TSA handles lack locks, and `pdf_timestamp_get_token`
    returns a borrowed pointer (`PdfSigning.cs:109-132`, `:1242-1276`).
12. The managed signature fallback parses untrusted `/ByteRange` and DER
    (`PdfSigning.cs:541-1046`).
13. `OutlineItemDto::from` is recursive (`rust/src/lib.rs:1503-1516`); deep
    outlines can stack-overflow.
14. 13 builder functions changed signature to add `page_index`
    (`rust/src/lib.rs:2388-2806`); `NativeAbiTests` checks symbol presence only,
    so version-skew ABIs still need a dedicated test.
15. No `catch_unwind` on any `extern "C"` export; the new barcode/font/tagged
    paths can panic or abort the host process.
16. `CString::new` on extracted text and `serde_json` non-finite floats still fail
    the whole extraction (see C13/C14).
17. An open streaming table blocks save/build (`rust/src/lib.rs:456-463`);
    `PdfStreamingTable` has no `Dispose`/finalizer and marks itself finished before
    calling native end (`PdfStreamingTable.cs:107-117`), so a failure can poison
    both sides.
18. Streaming begin/add-row trust caller counts and do not validate header/grid
    colors, grid width, padding, or row span against `max_rowspan`
    (`rust/src/lib.rs:2849-2885`, `:2924-2947`).
19. `pdf_builder_add_page_like` returns the new page index and C# updates
    `_pageCount` with `Math.Max` (`rust/src/lib.rs:2973-3006`,
    `DocumentBuilder.cs:686-699`).
