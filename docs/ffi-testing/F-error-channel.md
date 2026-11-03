# F. Error-channel scenarios

| # | Scenario | How to test | Why |
| --- | --- | --- | --- |
| F1 | Error is thread-local | Two threads fail with different inputs; each reads its own message | `LAST_ERROR` is `thread_local!` (`rust/src/lib.rs:54-56`); cross-thread leakage would be a bug |
| F2 | Stale error after a success | Fail, then succeed, then call `pdf_last_error_message` | It is never cleared (`rust/src/lib.rs:4529-4533`); assert/document the stale behavior |
| F3 | No error means NULL | Fresh thread with no failing call, read the message | C# maps NULL to "Unknown error" |
| F4 | Error with non-ASCII, quotes, and newlines | Force an `IoError` containing a Unicode path | The C# UTF-8 decode and `CString` construction must succeed |
| F5 | Error with an interior NUL | Force an error string containing `\0` | `CString::new(...).unwrap_or(null)` (`rust/src/lib.rs:4464-4474`) silently loses the message |
| F6 | Message freshness | Call `pdf_last_error_message` twice, free one, then read the other | Each call should return an independent allocation |
| F7 | `pdf_get_string_len` correctness | Compare the returned length with `Encoding.UTF8.GetByteCount`, including emoji | An off-by-one or character-vs-byte bug truncates managed strings |
| F8 | `error_code` out-parameter | Pre-set sentinels; success writes `0`, failure writes `-1`; `null` pointer is safe | `editor_handle_result` writes `0`/`-1` but does not clear TLS (`rust/src/lib.rs:4407-4426`) |
| F9 | Mixed error channels | Force shim TLS failures and upstream `error_code` failures; assert wrappers surface both | `PdfConverter` mixes `GetLastError()` and `ThrowIfError` (`PdfConverter.cs:376-392`, `:506-513`) |
| F10 | Stale TLS after editor success | Fail a shim call, then succeed an editor open (`error_code == 0`), then read TLS | TLS is never cleared on success (`rust/src/lib.rs:4529-4533`) |
| F11 | Silent null on upstream failure | Force an upstream accessor to return null with a nonzero error code | `PdfSigning` can map a failed accessor to a null field without throwing (`PdfSigning.cs:399-423`) |
| F12 | Upstream codes are not `-1` | Load an invalid certificate and observe the `error_code` values (observed `1` and `3`) | Treat any nonzero code as failure; comparing only to `-1` misclassifies upstream errors |
