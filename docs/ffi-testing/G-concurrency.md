# G. Concurrency scenarios

| # | Scenario | How to test | Why |
| --- | --- | --- | --- |
| G1 | Independent documents in parallel | `Parallel.For` over thousands of `PdfDocument`/`DocumentBuilder` instances, mixing successes and failures | Verifies the library is usable from thread pools |
| G2 | Shared builder across threads | Concurrent ops on one `PdfBuilderState*` under TSan | There is no locking; expect a data race/UB, so document as unsupported or add a lock |
| G3 | Dispose racing with an in-flight call | One thread calls `Dispose`, another calls an op; run under TSan or in a child process | Detects use-after-free races in `DocumentBuilder` |
| G4 | Concurrent free of returned strings | Multiple threads free their own pointers | Allocator thread-safety sanity |
| G5 | GC pressure during calls | A background thread allocates and forces GC while the main thread is inside a native call | Verifies `fixed`/`GCHandle` pinning keeps buffers alive |
| G6 | Pinning failure under fragmentation | Large pinned allocations for `ConvertImages` and rich paragraphs | `GCHandle.Alloc` failures must not leak handles |
