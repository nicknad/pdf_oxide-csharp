# F. Error-channel scenarios

| # | Scenario | How to test | Why |
| --- | --- | --- | --- |
| F1 | Error is thread-local | Two threads fail with different inputs; each reads its own message | `LAST_ERROR` is `thread_local!` (`rust/src/lib.rs:47`); cross-thread leakage would be a bug |
| F2 | Stale error after a success | Fail, then succeed, then call `pdf_last_error_message` | It is never cleared (`rust/src/lib.rs:2136`); assert/document the stale behavior |
| F3 | No error means NULL | Fresh thread with no failing call, read the message | C# maps NULL to "Unknown error" |
| F4 | Error with non-ASCII, quotes, and newlines | Force an `IoError` containing a Unicode path | The C# UTF-8 decode and `CString` construction must succeed |
| F5 | Error with an interior NUL | Force an error string containing `\0` | `CString::new(...).unwrap_or(null)` (`rust/src/lib.rs:2074`) silently loses the message |
| F6 | Message freshness | Call `pdf_last_error_message` twice, free one, then read the other | Each call should return an independent allocation |
| F7 | `pdf_get_string_len` correctness | Compare the returned length with `Encoding.UTF8.GetByteCount`, including emoji | An off-by-one or character-vs-byte bug truncates managed strings |
