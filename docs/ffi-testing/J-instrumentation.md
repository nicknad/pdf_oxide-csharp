# J. Instrumentation matrix

| Tool | Target | Finds |
| --- | --- | --- |
| ASan + UBSan | Rust cdylib and the C driver | Out-of-bounds reads (`colors`, `cells`, `len`), signed overflow |
| TSan | G1-G5 | Builder/state races |
| LeakSanitizer + RSS sampler | B1-B2, H1-H2 | Native leaks, file descriptor and handle leaks |
| libFuzzer / AFL++ | D1-D8 | Parser/ABI crashes and hangs |
| SharpFuzz / random-op generator | D9, E1-E6 | Managed-path failures |
| PageHeap / procdump / WER | Windows partial crashes | Heap corruption and native access violations |
| `cargo miri` (pure-Rust parts only) | Internal logic | UB not reachable through the FFI tests |
