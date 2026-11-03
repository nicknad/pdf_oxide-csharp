# L. Signing, verification, and timestamp scenarios

Signing and timestamp APIs are upstream `pdf_oxide` exports declared in
`csharp/PdfOxideBindings/UpstreamNativeMethods.cs:278-422`, not in `rust/src/lib.rs`.
`PdfSigning` also contains a managed fallback parser for PDFs with zero native
signatures (`csharp/PdfOxideBindings/PdfSigning.cs:526-1046`). Handles use upstream
`*_free` functions; upstream byte buffers use `free_bytes(ptr)` with no length.

| # | Scenario | How to test | Why |
| --- | --- | --- | --- |
| L1 | Certificate handle ownership | Load bytes/PEM, then dispose; double-dispose; use-after-dispose; force `GC.Collect` plus finalizer; wrong free function | `PdfSigningCertificate` has `Dispose` and a finalizer with no lock (`PdfSigning.cs:109-132`) |
| L2 | Sign and PAdES output buffers | Sign valid/invalid/empty/large payloads; free output with upstream `free_bytes` exactly once; tamper the result | `pdf_sign_bytes` / `pdf_sign_bytes_pades` return upstream buffers (`PdfSigning.cs:180-316`) |
| L3 | Signature list lifetime | Get count, list, accessors, then free list/signature/document in several orders; induce a partial failure | Multiple handles are created and freed in `finally` blocks; one missed free leaks or double-frees |
| L4 | Detached verification | Verify a good signature, a tampered document, a truncated signature, and an unsupported algorithm | `pdf_signature_verify_detached` must report failure without crashing |
| L5 | Borrowed timestamp token | Copy the token, free the timestamp, then use/free the old pointer in a child process | `pdf_timestamp_get_token` returns a borrowed pointer (`PdfSigning.cs:1242-1261`) |
| L6 | TSA client lifecycle and failures | Create/request/free; dispose twice; concurrent dispose plus request; network failure/timeout; huge token | `PdfTimestampClient` has no lock around `Dispose` (`PdfSigning.cs:1267-1276`) |
| L7 | Managed fallback parser hardening | Feed malicious `/ByteRange`, huge `/Contents` hex, truncated/oversized CMS, and deeply nested DER; fuzz the parser | The C# fallback parses untrusted PDF bytes and allocates from attacker-controlled ranges (`PdfSigning.cs:541-1046`) |
| L8 | Version/ABI skew | Mix old/new native DLLs with the managed package; verify every signing symbol resolves and fails deterministically on mismatch | Signing symbols are upstream, and `NativeAbiTests` only checks presence, not signatures |
| L9 | Mixed error channels | Force `error_code` failures, TLS-only failures, and null-returning accessors; assert none are silently swallowed | `PdfSigning` mixes upstream error codes and shim TLS (`PdfSigning.cs:399-423`) |
| L10 | Large certificate/signature data | Large cert chains, CRL/OCSP arrays, multi-megabyte signatures, and `nuint` lengths above `int.MaxValue` | Upstream lengths are `nuint`; C# uses `checked((int)length)` and must free after overflow |
