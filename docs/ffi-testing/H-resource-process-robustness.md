# H. Resource and process-level robustness scenarios

| # | Scenario | How to test | Why |
| --- | --- | --- | --- |
| H1 | Handle leak on success and error | Snapshot handle/FD counts before and after 10k opens, including corrupt files | `PdfDocument::open` must close files on partial parse |
| H2 | Memory growth | 1M mixed calls, sample RSS, assert bounded | Catches leaks the C# `finally` misses |
| H3 | OOM / allocation failure | `DOTNET_GCHeapHardLimit`, cgroup memory limit, `ulimit -v` | Verify a clean error or abort, never corruption |
| H4 | Panic across FFI | Inject a panic (test-only feature or crafted input), run in a child process | Unwinding across `extern "C"` is UB; the process should abort deterministically. Consider adding `catch_unwind` to each entrypoint |
| H5 | Access violation isolation | Child-process driver with known-bad pointers | Confirms a native fault kills only the child, with a usable exit code/stderr |
| H6 | Signal / SIGSEGV / SIGABRT observability | Linux core dump + gdb backtrace; Windows WER dump / `procdump` | Makes crashes actionable in CI |
| H7 | Library unload while a call is active | Collectible ALC unload test | Documented as unsupported, but an isolated test prevents surprises |
| H8 | New large-data paths | Register multi-hundred-MB fonts, sign with huge certificate chains, use giant barcode/QR payloads and role maps; monitor RSS, handles, and temp files | The editor, signing, and builder extension paths buffer in memory with no documented caps |
