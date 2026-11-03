# D. Parser fuzzing scenarios

| # | Scenario | How to test | Why |
| --- | --- | --- | --- |
| D1 | Fuzz `PdfDocument::open` and every extractor | `cargo fuzz`/libFuzzer target in Rust using temp files, run under ASAN+UBSAN | Parser bugs surface as crashes before they reach C# |
| D2 | Corpus mutation from real files | Seed with `test-assets/*.pdf`, mutate with radamsa/AFL++ (`afl-fuzz -i seeds -o out`) | Real structure plus mutation reaches deeper parser paths |
| D3 | Truncated/partial PDFs | Truncate a seed at every 1/K byte offset, run all APIs | Simulates interrupted downloads and exercises partial-parse error paths |
| D4 | Random, empty, and non-PDF bytes | 0-byte file, header-only, random binary, PDF without xref, PDF with a cyclic object graph | Ensures clean errors and no infinite recursion |
| D5 | Decompression and object bombs | Zip-bomb stream, huge `/Count`, recursive `/Pages`, deeply nested arrays | Tests memory/time limits and cyclic detection |
| D6 | Deep outline and XMP nesting | Generate 10k-deep outline children and nested metadata | `OutlineItemDto::from` is recursive (`rust/src/lib.rs:858`), so deep structures become a stack overflow and process abort |
| D7 | Differential fuzzing: Rust API vs FFI | Same input through `pdf_oxide` directly and through the FFI; compare outputs | Catches FFI-only truncation, encoding, and JSON conversion bugs |
| D8 | C-ABI fuzzer | AFL++ persistent-mode C driver: `dlopen` the cdylib, decode bytes into a (symbol, args) call | Fuzzes the ABI itself, not only the parser; catches out-of-bounds reads from mismatched lengths |
| D9 | Managed fuzzer | SharpFuzz or a random PDF feed through `PdfDocument` in subprocess workers | Verifies the exact consumer path, including JSON deserialization |
| D10 | Timeout oracle | Watchdog (for example 10 s) per input; kill and classify as a hang | Parser hangs are as bad as crashes in a library |
