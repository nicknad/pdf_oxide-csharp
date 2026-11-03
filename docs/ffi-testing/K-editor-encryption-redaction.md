# K. Editor, page editing, encryption, and redaction scenarios

Introduced by trunk commit `5fc0822`. `PdfEditor` wraps upstream `document_editor_*`
exports plus two shim opens in `rust/src/lib.rs:4430-4449`. Ownership is split: the
shim boxes an upstream `DocumentEditor`, and C# frees it with the upstream
`document_editor_free` (`csharp/PdfOxideBindings/PdfEditor.cs:1194`). The wrapper
serializes calls with a per-instance lock; raw native callers do not.

Status: K1-K3 are implemented in `FfiCrashHost` and covered by `FfiCrashHostTests`;
the remaining scenarios are design-only.

| # | Scenario | How to test | Why |
| --- | --- | --- | --- |
| K1 | Editor handle ownership across the password shim | Open with `pdfoxide_editor_open_with_password` / `..._from_bytes_with_password`, free with `document_editor_free`; run wrong-free and double-free in `FfiCrashHost` | The shim returns `Box<DocumentEditor>` as `*mut c_void` (`rust/src/lib.rs:4407-4449`); allocator/type mismatch corrupts the heap |
| K2 | `error_code` out-parameter contract | Pre-set `error_code` to a sentinel, cover success, failure, and `null`; assert `0`/`-1` and that TLS is set only on failure | `editor_handle_result` writes `0`/`-1` but never clears TLS (`rust/src/lib.rs:4407-4426`) |
| K3 | Editor-from-bytes length mismatch | Pass `len` larger/smaller than the buffer, zero, null data, and `usize::MAX`; run under ASAN in a child process | `editor_open_from_bytes_with_password_internal` trusts `len` (`rust/src/lib.rs:4440-4449`) |
| K4 | Password handling | Empty, wrong, very long, embedded-NUL, and non-UTF-8 passwords; wrong password must fail without leaking the handle | Password bytes are pinned only for the call and never zeroized (`PdfEditor.cs:1356-1359`) |
| K5 | Encrypt/decrypt round-trip and buffer ownership | Save encrypted to file and bytes; reopen with the password; free output with upstream `free_bytes(ptr)` (no length), never `pdf_bytes_free` | Two allocators coexist: upstream `libc::free` vs shim `Box<[u8]>` |
| K6 | Page-edit sequences | Delete/move/rotate/crop with negative, equal-to-count, and huge indices; edit a 0-page and 1-page document; repeat edits | Upstream editor calls must reject out-of-range pages without corrupting state |
| K7 | Extract/split/merge | Merge a document with itself, merge zero pages, merge after source disposal, extract out-of-range pages; free every returned buffer exactly once | `document_editor_merge_from*` and `..._extract_pages_to_bytes` cross handle and buffer ownership |
| K8 | Redaction | Add/count/apply/scrub with zero, overlapping, and out-of-page rects; verify the original vs redacted bytes; free annotation/redaction lists in different orders | Redaction mutates the document in place; partial failure must not leave a corrupt handle |
| K9 | Dispose races | Two threads call `Dispose`, and one disposes during save/to-bytes/redaction | `_disposed` is a plain bool (`PdfEditor.cs:1194`); the finalizer makes a double-free possible |
| K10 | Raw concurrent editor calls | Bypass `_sync` with direct P/Invoke calls from multiple threads under TSan | The lock is wrapper-only; upstream expects serialized editor access |
| K11 | Nested editor post-processing | Force failures in annotation-flatten temp-file handling (read-only temp, disk full, collision); verify cleanup | `PdfEditor` opens nested handles and temp files for annotation flattening (`PdfEditor.cs:1111-1178`) |
| K12 | Large edits and size casts | Huge merges, multi-page edits, very large encrypted output, and outputs above `int.MaxValue` | C# casts `nuint` to `int` with `checked(...)`; an exception after native allocation must still free the buffer (`PdfEditor.cs:1316-1326`) |
