# I. Partial-crash test harness

A native access violation or abort cannot be caught in-process on .NET Core. All
memory-safety scenarios must run behind a process boundary.

## Design

1. `csharp/FfiCrashHost` is a small console app that takes a scenario id plus
   arguments, invokes raw `NativeMethods`, prints a machine-readable result, and
   exits.
2. xUnit tests start the host with time and memory limits (Windows Job Object,
   Linux `setrlimit`), then assert:
   - expected graceful case -> exit `0` and the expected `result=error` payload;
   - memory-safety case -> non-zero exit or a specific signal/exception code, with
     an optional saved dump.
3. A native C driver (`dlopen`/`LoadLibrary`) covers scenarios that C# cannot
   express safely: double free, out-of-bounds length, and short color arrays.
4. Run the whole suite under ASAN+UBSAN (Linux, Rust nightly), TSan for concurrency,
   and Windows PageHeap / Application Verifier.

## New surface routed through the host

The host currently covers core builder/document UB. Add these families as the new
handles land:

- editor handle: double-free, wrong free, use-after-free, password failures
- dual allocators: shim `pdf_bytes_free(len)` versus upstream `free_bytes(ptr)`
- signature/timestamp: certificate/TSA dispose races and the borrowed token
- unchecked arrays: radio group, dash/point/path, table cells, image/font lengths
- streaming table: abandoned open table, begin/end mismatch, short column/row arrays

`NativeAbiTests` covers scenario A1 (export presence) in the normal test run.

## Host protocol

```
dotnet FfiCrashHost.dll <scenario> [args...]
```

Output is line-oriented:

```
result=ok
error=<escaped message>
detail=<escaped detail>
```

Exit codes:

| Code | Meaning |
| --- | --- |
| `0` | Scenario completed; inspect `result=...` |
| `2` | Unexpected managed exception (host bug) |
| `3` | Usage error / unknown scenario |
| other | Native fault, abort, or timeout (platform-specific) |

## Crash-oracle rule for tests

For UB-by-design scenarios, a test asserts the host:

- terminates within the timeout (no hang),
- does not exit `2` (no unexpected managed failure),
- either reports a graceful error or dies with a native fault.

Under ASAN/PageHeap the same scenarios become hard failures when memory is
corrupted, which is the signal we want in CI.
