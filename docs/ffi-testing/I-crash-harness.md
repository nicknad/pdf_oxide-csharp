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
