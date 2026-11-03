# G. Concurrency scenarios

| # | Scenario | How to test | Why |
| --- | --- | --- | --- |
| G1 | Independent documents in parallel | `Parallel.For` over thousands of `PdfDocument`/`DocumentBuilder` instances, mixing successes and failures | Verifies the library is usable from thread pools |
| G2 | Shared builder across threads | Concurrent ops on one `PdfBuilderState*` under TSan | There is no locking; expect a data race/UB, so document as unsupported or add a lock |
| G3 | Dispose racing with an in-flight call | One thread calls `Dispose`, another calls an op; run under TSan or in a child process | Detects use-after-free races in `DocumentBuilder` |
| G4 | Concurrent free of returned strings | Multiple threads free their own pointers | Allocator thread-safety sanity |
| G5 | GC pressure during calls | A background thread allocates and forces GC while the main thread is inside a native call | Verifies `fixed`/`GCHandle` pinning keeps buffers alive |
| G6 | Pinning failure under fragmentation | Large pinned allocations for `ConvertImages` and rich paragraphs | `GCHandle.Alloc` failures must not leak handles |
| G7 | Editor wrapper lock vs raw calls | Concurrent operations through `PdfEditor` (serialized) and direct P/Invoke (unsynchronized) under TSan | The lock exists only in the managed wrapper (`PdfEditor.cs`) |
| G8 | Certificate/TSA dispose races | Concurrent `Dispose` and use of `PdfSigningCertificate` / `PdfTimestampClient` | No lock or `Interlocked` around handle release (`PdfSigning.cs:109-132`, `:1267-1276`) |
| G9 | Builder dispose plus finalizer race | Two threads dispose the same `DocumentBuilder`; force `GC.Collect`/finalizers | `_disposed` is a plain bool and the native free is not idempotent |
| G10 | Parallel signing and verification | Sign/verify independent documents in parallel; share one certificate handle | Confirms per-document isolation and exposes shared-handle races |
| G11 | Streaming table concurrency | Two threads call begin/add-row/end on one builder; dispose during a stream | Streaming state is a single unsynchronized `Option` in the builder |
