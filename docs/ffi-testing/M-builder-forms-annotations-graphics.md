# M. Builder extensions: forms, annotations, links, watermarks, stamps, footnotes, barcodes, graphics, fonts, and tagged PDF

Introduced by trunk commits `ca8061a`, `0bc9aaa`, and `7e8942e`. Roughly 45 new
exports were added to `rust/src/lib.rs`, and 13 builder functions changed signature
to take `page_index` (`rust/src/lib.rs:2388-2806`). Many new APIs take caller-owned
arrays and counts without a matching length parameter, so they are high-value ABI
fuzzing targets.

Status: M2-M10 are implemented in `FfiCrashHost` (builder extensions, short
graphics arrays, invalid font bytes, forms flattening, large builder state).
M11 and M12 are implemented (`builder-error-recovery` and the extended builder
fuzzer). M1 (version skew) remains design-only.

| # | Scenario | How to test | Why |
| --- | --- | --- | --- |
| M1 | Builder signature/ABI skew | Call the old and new builder signatures against the opposite native DLL version in a child process | `page_index` was inserted into 13 functions; `NativeAbiTests` checks symbol presence only |
| M2 | Form-field builders | Add text/checkbox/combo/list/radio/push/signature fields with empty, duplicate, very long, and NUL-containing names; select values not in options; short parallel arrays; huge counts | Form builders validate only non-empty/positive and trust `count` plus parallel arrays (`rust/src/lib.rs:3227-3475`) |
| M3 | Form flattening | Enumerate fields, flatten forms/annotations/all, free the field list and document in every order; run on non-form PDFs | `PdfEditor` opens a second document for enumeration and frees upstream handles (`PdfEditor.cs:931-1109`) |
| M4 | Links and annotation builders | URL/page/named/JS/rect links; empty URL/script; target page negative, equal to count, future, and `int.MaxValue`; markup/free-text/sticky notes | `pdf_builder_add_link_page` rejects only negative targets (`rust/src/lib.rs:2828-2842`) |
| M5 | Watermarks, stamps, footnotes | NaN/Inf/huge opacity and rotation, zero/negative/Inf rects, empty text, unknown stamp type, duplicate footnotes | Custom watermark validates only finiteness, not ranges (`rust/src/lib.rs:3117-3134`) |
| M6 | Barcodes and QR | Every barcode type with empty, invalid, oversized, and non-ASCII payloads; bad rects; huge QR data | Builders validate type code and rect, then delegate data validation to the encoder (`rust/src/lib.rs:3477-3527`) |
| M7 | Vector graphics arrays | Short dash arrays, mismatched `dash_len`, NaN/Inf coordinates, huge `point_count`, path `kinds`/`coords` mismatch, extreme arc radii/angles | `read_dash_pattern`, `read_points`, and `read_path_operations` trust caller counts (`rust/src/lib.rs:3529-3691`) |
| M8 | Font registration | Zero-length, null, truncated/garbage TTF, huge font, duplicate names; register file vs bytes, then build | `register_font_bytes` copies `len` bytes and invalid fonts are silently skipped during build (`rust/src/lib.rs:4213-4236`, `:470-475`) |
| M9 | Tagged PDF and language | Toggle UA-1 on/off, null/empty/very long language, duplicate/conflicting role maps; build and reopen | New tagged-PDF state must not corrupt the document or panic on invalid role maps (`rust/src/lib.rs:4260-4301`) |
| M10 | Large builder state | Many pages and ops, huge images/fonts, repeated save/build without freeing, output above `i32::MAX` | `PdfBuilderState` buffers everything until save/build with no caps (`rust/src/lib.rs:420-486`) |
| M11 | Error atomicity | Force a failure partway through table/radio/form creation, then build a valid document | Partially applied ops must not leave the builder unusable or half-written |
| M12 | Extend the builder fuzzer | Add form, link, watermark, stamp, footnote, barcode, graphics, font, and tagged-PDF operations to `rust/fuzz/fuzz_targets/builder.rs`; run under ASAN | The current fuzzer covers only the pre-`ca8061a` builder surface |
