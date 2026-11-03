# N. Streaming table and same-size page builder scenarios

Introduced by trunk commit `4997b91`. `PdfBuilderState` now holds an optional
in-progress table (`streaming_table`, `rust/src/lib.rs:439`), and `build_bytes`
refuses to run while one is open (`rust/src/lib.rs:456-463`). Four new exports:
`pdf_builder_streaming_table_begin`, `..._add_row`, `..._end`, and
`pdf_builder_add_page_like` (`rust/src/lib.rs:2796-3006`). The managed
`PdfStreamingTable` has no `Dispose`/finalizer and marks itself finished before
calling the native end.

Status: N1-N3 and N8 are implemented in `FfiCrashHost` and covered by
`FfiCrashHostTests`; the remaining scenarios are design-only.

| # | Scenario | How to test | Why |
| --- | --- | --- | --- |
| N1 | Streaming state machine | Begin twice, end without begin, add a row without begin, end twice; then begin/add/end a valid table | There is one `Option<PendingStreamingTable>` per builder (`rust/src/lib.rs:439`, `:2820-2826`, `:2921-2923`, `:2957-2959`) |
| N2 | Abandoned table poisons save/build | Begin and add rows, never end; call save and build; assert the "finish the streaming table before saving" error; then end and retry | `build_bytes` rejects an open table (`rust/src/lib.rs:456-463`); `PdfStreamingTable` has no cleanup path |
| N3 | Finish failure and managed poisoning | Open a table, call native end out-of-band, then call `Finish()`; also force end to fail after rows were added | `Finish` sets `_finished = true` before calling end (`PdfStreamingTable.cs:107-117`); a failure leaves the object unusable and the native state open |
| N4 | Column array mismatches | Short `headers`/`widths`/`aligns`, null arrays, `column_count` zero/negative/`int.MaxValue`, NaN/Inf/non-positive widths, invalid align codes, null header strings | Begin reads `count` entries from three arrays and only validates widths/aligns (`rust/src/lib.rs:2849-2871`) |
| N5 | Row array mismatches | Short `cells`, `count != column_count`, null cells, huge/negative count, null/short `rowspans`, negative/zero/huge rowspans, rowspan above `max_rowspan` | `add_row` reads `count` cells and optional rowspans, clamps negatives to 1, and never compares against `max_rowspan` (`rust/src/lib.rs:2924-2947`) |
| N6 | Config numeric ranges | NaN/Inf/out-of-range header fill and grid colors, negative/NaN `grid_width`, NaN/negative padding, huge `max_rowspan` | C# validates these (`DocumentBuilder.cs:550-570`), but the raw ABI only converts max_rowspan (`rust/src/lib.rs:2873-2885`) |
| N7 | Page targeting | Begin on a negative/out-of-range page; begin on page 0, add pages, then end; add a page between begin and end; verify the committed op lands on the intended page | Begin checks the page index once and end reuses the stored index (`rust/src/lib.rs:2841-2847`, `:2960-2966`) |
| N8 | Add page like | Call `pdf_builder_add_page_like` with negative, out-of-range, and huge reference indices; verify the returned index and C# page count | Returns the new page index and updates `_pageCount` with `Math.Max` (`rust/src/lib.rs:2973-3006`, `DocumentBuilder.cs:686-699`) |
| N9 | Large streaming table | 100k rows, many columns, huge cell strings, header repetition across many pages; monitor memory/time and reopen the output | Every row is buffered in `PendingStreamingTable.rows` until end (`rust/src/lib.rs:2887-2892`) |
| N10 | Concurrency | Begin/add/end from multiple threads on one builder; dispose the builder during streaming; run under TSan | Builder state is a raw `&mut` with no synchronization |
| N11 | Fuzz streaming operations | Extend `rust/fuzz/fuzz_targets/builder.rs` with begin/add/end sequences, mismatched arrays, and abandoned tables | The current fuzzer has no streaming state-machine coverage |
| N12 | Export and signature skew | Rely on A1 for export presence; additionally call the four new entry points against old/new native DLLs | A1 checks only that symbols exist, not their argument shapes |
